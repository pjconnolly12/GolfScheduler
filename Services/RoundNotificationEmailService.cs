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
    Task SendRoundCreatedNotificationAsync(
        ApplicationUser organizer,
        Round round,
        IReadOnlyCollection<string> recipients,
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

    public async Task SendRoundCreatedNotificationAsync(
        ApplicationUser organizer,
        Round round,
        IReadOnlyCollection<string> recipients,
        string siteUrl,
        CancellationToken cancellationToken = default)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.CredentialsFilePath) || !File.Exists(_options.CredentialsFilePath))
        {
            _logger.LogWarning(
                "Round notification email skipped because Gmail credentials file was not found at {CredentialsFilePath}.",
                _options.CredentialsFilePath);
            return;
        }

        try
        {
            var service = await CreateGmailServiceAsync(cancellationToken);
            var organizerName = organizer.UserName ?? organizer.Email ?? "A golfer";
            var subject = $"{organizerName} added a new golf round";
            var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
            var body =
$"""{organizerName} just added a golf round.

Course: {round.Course}
Date & time: {formattedDate}

View details: {siteUrl}
""";

            var rawMessage = BuildRawMessage(recipients, subject, body, _options.FromAddress);
            var gmailMessage = new Message { Raw = rawMessage };

            cancellationToken.ThrowIfCancellationRequested();
            await service.Users.Messages.Send(gmailMessage, _options.SenderUserId).ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send round notification through Gmail API.");
        }
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
            .TrimEnd('=');
    }
}
