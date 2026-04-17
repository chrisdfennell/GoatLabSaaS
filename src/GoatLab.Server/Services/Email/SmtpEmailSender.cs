using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GoatLab.Server.Services.Email;

public class SmtpEmailSender : IAppEmailSender
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> opts, ILogger<SmtpEmailSender> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        msg.To.Add(MailboxAddress.Parse(toAddress));
        msg.Subject = subject;

        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (!string.IsNullOrWhiteSpace(plainTextBody))
            body.TextBody = plainTextBody;
        msg.Body = body.ToMessageBody();

        var security = _opts.UseSsl
            ? (_opts.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
            : SecureSocketOptions.None;

        using var client = new SmtpClient();
        if (_opts.AllowInvalidCertificate)
        {
            // Dev-only — see SmtpOptions.AllowInvalidCertificate.
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
            _logger.LogWarning("SMTP cert validation disabled — Smtp:AllowInvalidCertificate=true");
        }
        await client.ConnectAsync(_opts.Host, _opts.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(_opts.Username))
            await client.AuthenticateAsync(_opts.Username, _opts.Password ?? string.Empty, cancellationToken);
        await client.SendAsync(msg, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation("Sent email to {To} with subject {Subject}", toAddress, subject);
    }
}
