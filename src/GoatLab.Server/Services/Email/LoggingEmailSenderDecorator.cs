using GoatLab.Server.Data;
using GoatLab.Shared.Models;

namespace GoatLab.Server.Services.Email;

// Wraps the underlying IAppEmailSender (SmtpEmailSender / NullEmailSender)
// and writes one EmailLog row per send attempt. Singleton lifetime to match
// the inner sender; the DbContext write happens inside a per-call scope so
// we don't leak a scoped DbContext into a singleton.
//
// This is the single answer to "did the email actually send?" — every send
// path (password reset, invitation, alert digest, trial reminder, transfer
// notification, bulk email) routes through this decorator.
public class LoggingEmailSenderDecorator : IAppEmailSender
{
    private readonly IAppEmailSender _inner;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoggingEmailSenderDecorator> _logger;

    public LoggingEmailSenderDecorator(
        IAppEmailSender inner,
        IServiceScopeFactory scopeFactory,
        ILogger<LoggingEmailSenderDecorator> logger)
    {
        _inner = inner;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new EmailLog
        {
            At = DateTime.UtcNow,
            ToAddress = toAddress,
            Subject = string.IsNullOrEmpty(subject) ? null : (subject.Length > 300 ? subject[..300] : subject),
            Sender = _inner.GetType().Name,
            BodyBytes = (htmlBody?.Length ?? 0) + (plainTextBody?.Length ?? 0),
            Status = "sent",
        };

        try
        {
            await _inner.SendAsync(toAddress, subject, htmlBody, plainTextBody, cancellationToken);
            // NullEmailSender records "skipped" so the admin page can
            // distinguish a no-op send from a real one.
            if (_inner is NullEmailSender) entry.Status = "skipped";
        }
        catch (Exception ex)
        {
            entry.Status = "failed";
            entry.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await PersistAsync(entry);
            throw;
        }

        await PersistAsync(entry);
    }

    private async Task PersistAsync(EmailLog entry)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GoatLabDbContext>();
            db.EmailLogs.Add(entry);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let log persistence failure break a send. The send already
            // happened (or already failed); we only lose visibility.
            _logger.LogWarning(ex, "Failed to write EmailLog for {To} / {Subject}", entry.ToAddress, entry.Subject);
        }
    }
}
