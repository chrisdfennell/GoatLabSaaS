namespace GoatLab.Server.Services.Email;

public interface IAppEmailSender
{
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default);
}
