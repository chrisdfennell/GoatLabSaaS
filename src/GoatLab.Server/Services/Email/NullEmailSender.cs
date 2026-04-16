namespace GoatLab.Server.Services.Email;

// Registered when Smtp:Host is blank. Logs the attempt and swallows the send
// so upstream features don't fail just because the mail server isn't stood
// up yet. Swap to SmtpEmailSender by setting Smtp:Host in config.
public class NullEmailSender : IAppEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email to {To} with subject \"{Subject}\" was dropped — Smtp:Host is not configured.",
            toAddress, subject);
        return Task.CompletedTask;
    }
}
