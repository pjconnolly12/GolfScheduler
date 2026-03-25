using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using MyApp.Data;
using MyApp.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.IO;
using System.Globalization;

namespace MyApp.Services
{
  public class EmailRoundWatcher : BackgroundService
  {
    private readonly ILogger<EmailRoundWatcher> _logger;
    private readonly IServiceProvider _services;
    private readonly RoundOperationsOptions _roundOperationsOptions;

    public EmailRoundWatcher(
      ILogger<EmailRoundWatcher> logger,
      IServiceProvider services,
      IOptions<RoundOperationsOptions> roundOperationsOptions)
    {
      _logger = logger;
      _services = services;
      _roundOperationsOptions = roundOperationsOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("EmailRoundWatcher started.");

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await CheckInboxAsync();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error checking inbox");
        }

        // Wait 5 minutes before next check (tune this as needed)
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
      }
    }

    private async Task CheckInboxAsync()
    {
      var emails = await FetchNewEmailsAsync();
      _logger.LogInformation($"Fetched {emails.Count} new emails.");

      using var scope = _services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

      await SendUpcomingRoundRemindersAsync(scope.ServiceProvider, db, default);
      await SendMaybeEntryExpirationRemindersAsync(scope.ServiceProvider, db, default);

      foreach (var email in emails)
      {
        if (IsTeeTimeConfirmation(email.Subject))
        {
          _logger.LogInformation("Processing tee time confirmation email.");
          var round = ParseRoundFromEmail(email.Subject, email.Body);
          if (round != null)
          {
            // Avoid duplicates by checking date & course
            bool exists = await db.Rounds.AnyAsync(r =>
                r.Course == round.Course &&
                r.Date == round.Date);
            _logger.LogInformation($"Found round from email: {round.Course} on {round.Date}");

            if (!exists)
            {
              db.Rounds.Add(round);
              await db.SaveChangesAsync();
              await AddOrganizerAsFirstGolferAsync(scope.ServiceProvider, db, round, stoppingToken: default);
              _logger.LogInformation($"Created round: {round.Course} - {round.Date}");
              await SendRoundCreatedNotificationAsync(scope.ServiceProvider, db, round, stoppingToken: default);
            }
          }
        }
      }
    }

    private async Task AddOrganizerAsFirstGolferAsync(
      IServiceProvider serviceProvider,
      ApplicationDbContext db,
      Round round,
      CancellationToken stoppingToken)
    {
      var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

      var organizer = await ResolveUserByEmailOrRoleAsync(
        userManager,
        _roundOperationsOptions.NotificationOwnerEmail,
        _roundOperationsOptions.RoundOrganizerRole);
      if (organizer == null)
      {
        _logger.LogWarning(
          "Round created but no organizer was resolved from RoundOperations:NotificationOwnerEmail or RoundOperations:RoundOrganizerRole for auto-enrollment.");
        return;
      }

      var player = await db.Players
        .FirstOrDefaultAsync(p =>
          p.UserId == organizer.Id
          || (!string.IsNullOrEmpty(organizer.PlayerId) && p.Id == organizer.PlayerId)
          || (!string.IsNullOrEmpty(organizer.Email) && p.Email == organizer.Email),
          stoppingToken);

      if (player == null)
      {
        player = new Player
        {
          UserId = organizer.Id,
          Name = organizer.UserName ?? organizer.Email ?? "Unknown",
          Email = organizer.Email ?? string.Empty
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(stoppingToken);

        if (string.IsNullOrEmpty(organizer.PlayerId))
        {
          organizer.PlayerId = player.Id;
          await userManager.UpdateAsync(organizer);
        }
      }

      var alreadyJoined = await db.Entries
        .AnyAsync(e => e.RoundId == round.Id && e.PlayerId == player.Id, stoppingToken);

      if (alreadyJoined)
      {
        return;
      }

      db.Entries.Add(new Entry
      {
        RoundId = round.Id,
        PlayerId = player.Id,
        Status = "Confirmed",
        Guests = 0,
        CreatedAt = DateTime.UtcNow
      });

      round.Golfers = Math.Max(round.Golfers, 1);
      db.Rounds.Update(round);
      await db.SaveChangesAsync(stoppingToken);
    }

    private async Task SendMaybeEntryExpirationRemindersAsync(
      IServiceProvider serviceProvider,
      ApplicationDbContext db,
      CancellationToken stoppingToken)
    {
      var nowUtc = DateTime.UtcNow;

      var entriesToRemind = await db.Entries
        .Include(e => e.Player)
        .Include(e => e.Round)
        .Where(e =>
          e.MaybeReminderSentAtUtc == null &&
          e.Status == "Maybe" &&
          e.ExpiresAt.HasValue &&
          e.ExpiresAt > nowUtc &&
          e.ExpiresAt.Value.AddHours(-12) <= nowUtc)
        .ToListAsync(stoppingToken);

      if (entriesToRemind.Count == 0)
      {
        return;
      }

      var roundNotificationEmailService = serviceProvider.GetRequiredService<IRoundNotificationEmailService>();
      var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RoundNotificationEmailOptions>>().Value;

      foreach (var entry in entriesToRemind)
      {
        var recipientEmail = entry.Player?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
          _logger.LogInformation("Skipping Maybe expiration reminder for entry {EntryId} because the player email is missing.", entry.Id);
          entry.MaybeReminderSentAtUtc = nowUtc;
          continue;
        }

        var reminderSent = await roundNotificationEmailService.SendMaybeEntryExpirationReminderAsync(
          entry.Round,
          recipientEmail,
          options.SiteUrl,
          stoppingToken);

        if (reminderSent)
        {
          entry.MaybeReminderSentAtUtc = nowUtc;
        }
      }

      await db.SaveChangesAsync(stoppingToken);
    }


    private async Task SendRoundCreatedNotificationAsync(
      IServiceProvider serviceProvider,
      ApplicationDbContext db,
      Round round,
      CancellationToken stoppingToken)
    {
      var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var roundNotificationEmailService = serviceProvider.GetRequiredService<IRoundNotificationEmailService>();
      var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RoundNotificationEmailOptions>>().Value;

      var organizer = await ResolveUserByEmailOrRoleAsync(
        userManager,
        _roundOperationsOptions.NotificationOwnerEmail,
        _roundOperationsOptions.NotificationOwnerRole);
      if (organizer == null)
      {
        _logger.LogWarning(
          "Round created but no notification owner was resolved from RoundOperations:NotificationOwnerEmail or RoundOperations:NotificationOwnerRole.");
        return;
      }

      var recipientEmails = await db.DistributionListMembers
        .Where(m => m.OwnerUserId == organizer.Id)
        .Join(
          userManager.Users,
          member => member.MemberUserId,
          appUser => appUser.Id,
          (member, appUser) => appUser.Email)
        .Where(email => !string.IsNullOrWhiteSpace(email))
        .Select(email => email!)
        .Distinct()
        .ToListAsync(stoppingToken);

      if (recipientEmails.Count == 0)
      {
        _logger.LogInformation("Round created but no distribution list recipients found for notification owner {OwnerUserId}.", organizer.Id);
        return;
      }

      await roundNotificationEmailService.SendRoundCreatedNotificationAsync(
        organizer,
        round,
        recipientEmails,
        options.SiteUrl,
        stoppingToken);
    }

    private async Task SendUpcomingRoundRemindersAsync(
      IServiceProvider serviceProvider,
      ApplicationDbContext db,
      CancellationToken stoppingToken)
    {
      var nowLocal = DateTime.Now;
      var reminderCutoffLocal = nowLocal.AddHours(48);
      var reminderSentAtUtc = DateTime.UtcNow;

      var roundsToRemind = await db.Rounds
        .Include(r => r.Entries)
          .ThenInclude(e => e.Player)
        .Where(r => r.ReminderSentAtUtc == null && r.Date > nowLocal && r.Date <= reminderCutoffLocal)
        .ToListAsync(stoppingToken);

      if (roundsToRemind.Count == 0)
      {
        return;
      }

      var roundNotificationEmailService = serviceProvider.GetRequiredService<IRoundNotificationEmailService>();
      var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RoundNotificationEmailOptions>>().Value;

      foreach (var round in roundsToRemind)
      {
        var confirmedEntries = round.Entries
          .Where(e => e.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
          .OrderBy(e => e.CreatedAt)
          .ToList();

        var recipientEmails = confirmedEntries
          .Select(e => e.Player?.Email)
          .Where(email => !string.IsNullOrWhiteSpace(email))
          .Select(email => email!)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

        if (recipientEmails.Count == 0)
        {
          _logger.LogInformation("Skipping reminder for round {RoundId} because there are no confirmed recipients.", round.Id);
          round.ReminderSentAtUtc = reminderSentAtUtc;
          continue;
        }

        var confirmedNames = confirmedEntries
          .Select(FormatEntryDisplay)
          .ToList();

        var waitlistNames = round.Entries
          .Where(e => e.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
          .OrderBy(e => e.CreatedAt)
          .Select(FormatEntryDisplay)
          .ToList();

        var reminderSent = await roundNotificationEmailService.SendRoundReminderAsync(
          round,
          recipientEmails,
          confirmedNames,
          waitlistNames,
          options.SiteUrl,
          stoppingToken);

        if (reminderSent)
        {
          round.ReminderSentAtUtc = reminderSentAtUtc;
        }
      }

      await db.SaveChangesAsync(stoppingToken);
    }

    private static string FormatEntryDisplay(Entry entry)
    {
      var playerName = entry.Player?.Name;
      if (string.IsNullOrWhiteSpace(playerName))
      {
        playerName = entry.Player?.Email ?? "Unknown player";
      }

      var guests = entry.Guests ?? 0;
      return guests > 0
        ? $"{playerName} (+{guests} guest{(guests == 1 ? string.Empty : "s")})"
        : playerName;
    }

    private async Task<List<(string Subject, string Body)>> FetchNewEmailsAsync()
    {
      string[] Scopes = { GmailService.Scope.GmailModify };
      string ApplicationName = "Golf Scheduler";

      UserCredential credential;
      using (var stream = new FileStream("Secrets/credentials.json", FileMode.Open, FileAccess.Read))
      {
        string credPath = "token.json";
        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(credPath, true));
      }

      // Create Gmail API service
      var service = new GmailService(new BaseClientService.Initializer()
      {
        HttpClientInitializer = credential,
        ApplicationName = ApplicationName,
      });

      // Get unread messages
      var request = service.Users.Messages.List("me");
      request.LabelIds = "INBOX";
      request.Q = "is:unread subject:(Tee Time Confirmation)";
      var response = await request.ExecuteAsync();

      var emails = new List<(string Subject, string Body)>();

      if (response.Messages == null) return emails;

      foreach (var messageItem in response.Messages)
      {
        var msg = await service.Users.Messages.Get("me", messageItem.Id).ExecuteAsync();

        string subject = msg.Payload.Headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(no subject)";
        string body = GetPlainTextFromMessage(msg);

        emails.Add((subject, body));

        // Mark message as read (optional)
        await service.Users.Messages.Modify(new ModifyMessageRequest
        {
          RemoveLabelIds = new[] { "UNREAD" }
        }, "me", messageItem.Id).ExecuteAsync();
      }

      return emails;
    }

    // Helper to extract plain text from message parts
    private static string GetPlainTextFromMessage(Message msg)
    {
      if (msg.Payload.Parts == null)
        return Base64UrlDecode(msg.Payload.Body?.Data ?? "");

      foreach (var part in msg.Payload.Parts)
      {
        if (part.MimeType == "text/plain")
          return Base64UrlDecode(part.Body?.Data ?? "");
      }

      return "";
    }

    private static string Base64UrlDecode(string input)
    {
      if (string.IsNullOrEmpty(input)) return "";
      input = input.Replace('-', '+').Replace('_', '/');
      switch (input.Length % 4)
      {
        case 2: input += "=="; break;
        case 3: input += "="; break;
      }
      var bytes = Convert.FromBase64String(input);
      return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private bool IsTeeTimeConfirmation(string subject)
    {
      return subject.Contains("Tee Time Confirmation", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ApplicationUser?> ResolveUserByEmailOrRoleAsync(
      UserManager<ApplicationUser> userManager,
      string? preferredEmail,
      string? roleName)
    {
      if (!string.IsNullOrWhiteSpace(preferredEmail))
      {
        var userByEmail = await userManager.FindByEmailAsync(preferredEmail);
        if (userByEmail != null)
        {
          return userByEmail;
        }
      }

      if (string.IsNullOrWhiteSpace(roleName))
      {
        return null;
      }

      var roleUsers = await userManager.GetUsersInRoleAsync(roleName);
      if (roleUsers.Count == 0)
      {
        return null;
      }

      if (!string.IsNullOrWhiteSpace(preferredEmail))
      {
        var matchingRoleUser = roleUsers.FirstOrDefault(u =>
          string.Equals(u.Email, preferredEmail, StringComparison.OrdinalIgnoreCase));
        if (matchingRoleUser != null)
        {
          return matchingRoleUser;
        }
      }

      return roleUsers
        .OrderBy(u => u.Email ?? u.UserName ?? u.Id, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
    }

    private Round? ParseRoundFromEmail(string subject, string body)
    {
      // Only process confirmation emails
      if (!subject.Contains("Confirmation", StringComparison.OrdinalIgnoreCase))
        return null;

      // Example email:
      // Breakfast Hill Golf Club
      // Monday, November 3, 2025
      // 11:09 am
      // 4 Player(s)

      try
      {
        // Match course name (first line that contains 'Golf Club')
        var courseMatch = Regex.Match(body, @"([A-Za-z\s]+Golf Club)", RegexOptions.IgnoreCase);
        string courseName = courseMatch.Success ? courseMatch.Groups[1].Value.Trim() : "Unknown Course";

        // Match date line (e.g. Monday, November 3, 2025)
        var dateMatch = Regex.Match(body, @"([A-Za-z]+,\s+[A-Za-z]+\s+\d{1,2},\s+\d{4})", RegexOptions.IgnoreCase);
        DateTime? date = null;
        if (dateMatch.Success)
        {
          DateTime.TryParseExact(
              dateMatch.Groups[1].Value,
              "dddd, MMMM d, yyyy",
              CultureInfo.InvariantCulture,
              DateTimeStyles.None,
              out DateTime parsedDate
          );
          date = parsedDate;
        }

        // Match time (e.g. 11:09 am)
        var timeMatch = Regex.Match(body, @"(\d{1,2}:\d{2}\s*[ap]m)", RegexOptions.IgnoreCase);
        TimeSpan? teeTime = null;
        if (timeMatch.Success)
        {
          if (DateTime.TryParseExact(timeMatch.Groups[1].Value, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
            teeTime = parsedTime.TimeOfDay;
        }

        // Match player count
        var playerMatch = Regex.Match(body, @"(\d+)\s*Player", RegexOptions.IgnoreCase);
        int playerCount = playerMatch.Success ? int.Parse(playerMatch.Groups[1].Value) : 0;

        if (playerCount == 1)
        {
          _logger.LogInformation("Ignoring tee time confirmation email because the player count was 1.");
          return null;
        }

        // Match notes-based hole count (e.g. Notes: 9 Holes)
        var holesMatch = Regex.Match(body, @"Notes:\s*(\d+)\s*Holes?", RegexOptions.IgnoreCase);
        int? holes = holesMatch.Success ? int.Parse(holesMatch.Groups[1].Value) : null;

        // Ensure date/time are valid before creating a round
        if (date == null || teeTime == null)
          return null;

        return new Round
        {
          Course = courseName,
          Date = date.Value.Date + teeTime.Value,
          Holes = holes,
          Golfers = 0,
          PlayerLimit = playerCount > 0 ? playerCount : 4
        };
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error parsing email: {ex.Message}");
        return null;
      }
    }
  }
}
