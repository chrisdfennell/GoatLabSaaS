namespace GoatLab.Server.Services.Email;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    // Leave Host blank to disable outbound email — the app registers a no-op
    // sender in that case so features that would normally email (password
    // reset, email confirmation) still complete without errors.
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "GoatLab";
    // STARTTLS on 587 or implicit TLS on 465 when true. Set false only for
    // plain SMTP to a trusted internal relay.
    public bool UseSsl { get; set; } = true;

    // Dev-only escape hatch: when true, accept any TLS cert (skip chain
    // validation). Use ONLY to work around a TLS-intercepting AV on the dev
    // machine (Norton/Kaspersky/Zscaler etc) whose root CA isn't in the
    // container trust store. Must stay false in production.
    public bool AllowInvalidCertificate { get; set; }
}
