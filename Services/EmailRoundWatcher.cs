using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    public EmailRoundWatcher(ILogger<EmailRoundWatcher> logger, IServiceProvider services)
    {
      _logger = logger;
      _services = services;
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
      // TODO: Replace with real email API (Gmail, Graph, etc.)
      var emails = await FetchNewEmailsAsync();

      using var scope = _services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

      foreach (var email in emails)
      {
        if (IsTeeTimeConfirmation(email.Subject))
        {
          var round = ParseRoundFromEmail(email.Subject, email.Body);
          if (round != null)
          {
            // Avoid duplicates by checking date & course
            bool exists = await db.Rounds.AnyAsync(r =>
                r.Course == round.Course &&
                r.Date == round.Date);

            if (!exists)
            {
              db.Rounds.Add(round);
              await db.SaveChangesAsync();
              _logger.LogInformation($"Created round: {round.Course} - {round.Date}");
            }
          }
        }
      }
    }

    private async Task<List<(string Subject, string Body)>> FetchNewEmailsAsync()
    {
      string[] Scopes = { GmailService.Scope.GmailReadonly };
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

    private Round? ParseRoundFromEmail(string subject, string body)
    {
      // Only process confirmation emails
      if (!subject.Contains("CONFIRMED", StringComparison.OrdinalIgnoreCase))
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

        // Ensure date/time are valid before creating a round
        if (date == null || teeTime == null)
          return null;

        return new Round
        {
          Course = courseName,
          Date = date.Value.Date + teeTime.Value,
          Golfers = playerCount
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
