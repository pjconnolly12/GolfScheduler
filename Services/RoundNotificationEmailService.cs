using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using MyApp.Models;

namespace MyApp.Services;

public interface IRoundNotificationEmailService
{
    Task<bool> SendRoundCreatedNotificationAsync(
        ApplicationUser organizer,
        Round round,
        IReadOnlyCollection<string> recipients,
        string siteUrl,
        CancellationToken cancellationToken = default);

    Task<bool> SendRoundReminderAsync(
        Round round,
        IReadOnlyCollection<string> recipients,
        IReadOnlyCollection<string> confirmedEntries,
        IReadOnlyCollection<string> waitlistEntries,
        string siteUrl,
        CancellationToken cancellationToken = default);

    Task<bool> SendMaybeEntryExpirationReminderAsync(
        Round round,
        string recipient,
        string siteUrl,
        CancellationToken cancellationToken = default);

    Task<bool> SendWaitlistPromotionNotificationAsync(
        Round round,
        string recipient,
        string promotedPlayerName,
        IReadOnlyCollection<string> otherMembers,
        string siteUrl,
        CancellationToken cancellationToken = default);
}

public class RoundNotificationEmailOptions
{
    public string CredentialsFilePath { get; set; } = "Secrets/credentials.json";
    public string TokenDirectoryPath { get; set; } = "token-round-notifications";
    public string SenderUserId { get; set; } = "me";
    public string? FromAddress { get; set; }
    public string ApplicationName { get; set; } = "Golf Scheduler";
    public string SiteUrl { get; set; } = "https://localhost:5001";
}

public class RoundNotificationEmailService : IRoundNotificationEmailService
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailSend];
    private readonly RoundNotificationEmailOptions _options;
    private readonly ILogger<RoundNotificationEmailService> _logger;

    public RoundNotificationEmailService(
        IOptions<RoundNotificationEmailOptions> options,
        ILogger<RoundNotificationEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendRoundCreatedNotificationAsync(
        ApplicationUser organizer,
        Round round,
        IReadOnlyCollection<string> recipients,
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        if (!CanSendEmail(recipients))
        {
            return false;
        }

        try
        {
            var service = await CreateGmailServiceAsync(cancellationToken);
            var organizerName = organizer.UserName ?? organizer.Email ?? "A golfer";
            var subject = $"{organizerName} added a new golf round";
            var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
            var body = $"""
            {organizerName} just added a golf round.

            Course: {round.Course}
            Date & time: {formattedDate}

            View details: {siteUrl}
            """;

            var rawMessage = BuildRawMessage(recipients, subject, body, _options.FromAddress);
            var gmailMessage = new Message { Raw = rawMessage };

            cancellationToken.ThrowIfCancellationRequested();
            await service.Users.Messages.Send(gmailMessage, _options.SenderUserId).ExecuteAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send round notification through Gmail API.");
            return false;
        }
    }

    public async Task<bool> SendRoundReminderAsync(
        Round round,
        IReadOnlyCollection<string> recipients,
        IReadOnlyCollection<string> confirmedEntries,
        IReadOnlyCollection<string> waitlistEntries,
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        if (!CanSendEmail(recipients))
        {
            return false;
        }

        try
        {
            var service = await CreateGmailServiceAsync(cancellationToken);
            var subject = $"Reminder: golf round in 48 hours at {round.Course}";
            var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
            var confirmedSection = FormatList("Current entries in the round", confirmedEntries);
            var waitlistSection = FormatList("Waitlist for the round", waitlistEntries);
            var body = $"""
            This is your 48-hour reminder for the upcoming golf round.

            Round details
            - Round Date/Time: {formattedDate}
            - Location: {round.Course}

            {confirmedSection}

            {waitlistSection}

            If you cannot make the round please remove yourself so a new person can join.

            Link to the app: {siteUrl}
            """;

            var rawMessage = BuildRawMessage(recipients, subject, body, _options.FromAddress);
            var gmailMessage = new Message { Raw = rawMessage };

            cancellationToken.ThrowIfCancellationRequested();
            await service.Users.Messages.Send(gmailMessage, _options.SenderUserId).ExecuteAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send round reminder through Gmail API.");
            return false;
        }
    }

    public async Task<bool> SendMaybeEntryExpirationReminderAsync(
        Round round,
        string recipient,
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        var recipients = new[] { recipient };
        if (!CanSendEmail(recipients))
        {
            return false;
        }

        try
        {
            var service = await CreateGmailServiceAsync(cancellationToken);
            var subject = $"Reminder: your Maybe entry for {round.Course} expires in 12 hours";
            var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
            var body = $"""
            Round details
            - Round Date/Time: {formattedDate}

            Your entry is currently still listed as Maybe, this will expire in 12 hours and your saved spot will be removed. Please review and confirm your entry or remove your entry.

            Link to the app: {siteUrl}
            """;

            var rawMessage = BuildRawMessage(recipients, subject, body, _options.FromAddress);
            var gmailMessage = new Message { Raw = rawMessage };

            cancellationToken.ThrowIfCancellationRequested();
            await service.Users.Messages.Send(gmailMessage, _options.SenderUserId).ExecuteAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Maybe entry expiration reminder through Gmail API.");
            return false;
        }
    }

    public async Task<bool> SendWaitlistPromotionNotificationAsync(
        Round round,
        string recipient,
        string promotedPlayerName,
        IReadOnlyCollection<string> otherMembers,
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        var recipients = new[] { recipient };
        if (!CanSendEmail(recipients))
        {
            return false;
        }

        try
        {
            var service = await CreateGmailServiceAsync(cancellationToken);
            var subject = $"You're in the golf group for {round.Course}";
            var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
            var otherMembersSection = FormatList("Other members in your group", otherMembers);
            var body = $"""
            Hi {promotedPlayerName},

            You are now in the golf group.

            Group details
            - Date & time: {formattedDate}
            - Course: {round.Course}

            {otherMembersSection}

            Link to the app: {siteUrl}

            If you'd like to update this entry please use the app to do so.
            """;

            var rawMessage = BuildRawMessage(recipients, subject, body, _options.FromAddress);
            var gmailMessage = new Message { Raw = rawMessage };

            cancellationToken.ThrowIfCancellationRequested();
            await service.Users.Messages.Send(gmailMessage, _options.SenderUserId).ExecuteAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send waitlist promotion notification through Gmail API.");
            return false;
        }
    }

    private bool CanSendEmail(IReadOnlyCollection<string> recipients)
    {
        if (recipients.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.CredentialsFilePath) || !File.Exists(_options.CredentialsFilePath))
        {
            _logger.LogWarning(
                "Round notification email skipped because Gmail credentials file was not found at {CredentialsFilePath}.",
                _options.CredentialsFilePath);
            return false;
        }

        return true;
    }

    private static string FormatList(string heading, IReadOnlyCollection<string> items)
    {
        if (items.Count == 0)
        {
            return $"{heading}: None";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{heading}:");

        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }

        return builder.ToString().TrimEnd();
    }

    private async Task<GmailService> CreateGmailServiceAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(_options.CredentialsFilePath, FileMode.Open, FileAccess.Read);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "round-notification-sender",
            cancellationToken,
            new FileDataStore(_options.TokenDirectoryPath, true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }

    private static string BuildRawMessage(
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        string? fromAddress)
    {
        var toLine = string.Join(",", recipients);
        var messageBuilder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            messageBuilder.AppendLine($"From: {fromAddress}");
        }

        messageBuilder.AppendLine($"To: {toLine}");
        messageBuilder.AppendLine($"Subject: {subject}");
        messageBuilder.AppendLine("Content-Type: text/plain; charset=utf-8");
        messageBuilder.AppendLine();
        messageBuilder.AppendLine(body);

        var bytes = Encoding.UTF8.GetBytes(messageBuilder.ToString());
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", string.Empty);
    }
}
