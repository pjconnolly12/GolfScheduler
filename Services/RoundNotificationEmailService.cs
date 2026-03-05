using System.Net;
using System.Net.Mail;
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
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class RoundNotificationEmailService : IRoundNotificationEmailService
{
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

        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning("Round notification email skipped because SMTP configuration is incomplete.");
            return;
        }

        var organizerName = organizer.UserName ?? organizer.Email ?? "A golfer";
        var subject = $"{organizerName} added a new golf round";
        var formattedDate = round.Date.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");
        var body =
$"""{organizerName} just added a golf round.

Course: {round.Course}
Date & time: {formattedDate}

View details: {siteUrl}
""";

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress),
            Subject = subject,
            Body = body
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }
}
