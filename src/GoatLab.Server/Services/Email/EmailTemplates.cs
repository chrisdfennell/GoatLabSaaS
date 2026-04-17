namespace GoatLab.Server.Services.Email;

// Minimal transactional email templates. Plain inline HTML so we don't need
// a templating engine; replace with Razor or MJML when we have more than three.
public static class EmailTemplates
{
    private const string Brand = "GoatLab";

    public static (string Subject, string Html, string Text) ConfirmEmail(string displayName, string confirmationUrl) =>
    (
        Subject: $"Confirm your {Brand} account",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Confirm your email</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
  <p>Welcome to {Brand}. Click the button below to confirm your email so you can start using your farm.</p>
  <p style=""margin:32px 0;"">
    <a href=""{confirmationUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Confirm email</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">If the button doesn't work, paste this link into your browser:<br/>
    <span style=""word-break:break-all;"">{confirmationUrl}</span>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">If you didn't create a {Brand} account, you can ignore this email.</p>
</div>",
        Text: $"Hi {displayName},\n\nConfirm your {Brand} email by opening this link:\n{confirmationUrl}\n\nIf you didn't sign up, ignore this email."
    );

    public static (string Subject, string Html, string Text) TeamInvitation(string inviterName, string farmName, string role, string acceptUrl) =>
    (
        Subject: $"{System.Net.WebUtility.HtmlEncode(inviterName)} invited you to join {farmName} on {Brand}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">You've been invited</h2>
  <p><strong>{System.Net.WebUtility.HtmlEncode(inviterName)}</strong> invited you to join <strong>{System.Net.WebUtility.HtmlEncode(farmName)}</strong> on {Brand} as a <strong>{role}</strong>.</p>
  <p style=""margin:32px 0;"">
    <a href=""{acceptUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Accept invite</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">The link expires in 7 days. You'll need a {Brand} account with this email address to accept.</p>
  <p style=""font-size:13px;color:#6b7a70;"">If you weren't expecting this, you can safely ignore the email — nothing happens unless you click the link.</p>
</div>",
        Text: $"{inviterName} invited you to join {farmName} on {Brand} as a {role}.\nAccept the invite here:\n{acceptUrl}\n\nThe link expires in 7 days."
    );

    public static (string Subject, string Html, string Text) TrialEnding(string displayName, string planName, int daysRemaining, string billingUrl) =>
    (
        Subject: $"Your {Brand} {planName} trial ends in {daysRemaining} day{(daysRemaining == 1 ? "" : "s")}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Your trial ends soon</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
  <p>Your <strong>{System.Net.WebUtility.HtmlEncode(planName)}</strong> trial ends in <strong>{daysRemaining} day{(daysRemaining == 1 ? "" : "s")}</strong>.</p>
  <p>Add your billing details now to keep your herd records, production data, and reports flowing without interruption.</p>
  <p style=""margin:32px 0;"">
    <a href=""{billingUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Add billing details</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">If you'd rather downgrade to the free Homestead plan, open the billing portal from inside the app — you'll keep read-only access to all your data.</p>
</div>",
        Text: $"Hi {displayName},\n\nYour {Brand} {planName} trial ends in {daysRemaining} day(s).\nAdd billing details at:\n{billingUrl}"
    );

    public static (string Subject, string Html, string Text) AlertDigest(
        string displayName, string farmName, IReadOnlyList<(string Title, string? Body, string Severity)> alerts, string alertsUrl)
    {
        // Group by severity so the Error rows surface visually first.
        var ordered = alerts
            .OrderBy(a => a.Severity == "Error" ? 0 : a.Severity == "Warning" ? 1 : 2)
            .ToList();

        string color(string sev) => sev switch
        {
            "Error" => "#c62828",
            "Warning" => "#ef6c00",
            _ => "#1565c0",
        };

        var rows = string.Join("\n", ordered.Select(a => $@"
    <tr>
      <td style=""padding:10px 12px;border-bottom:1px solid #eee;width:8px;background:{color(a.Severity)};""></td>
      <td style=""padding:10px 12px;border-bottom:1px solid #eee;"">
        <div style=""font-weight:600;color:#1a2421;"">{System.Net.WebUtility.HtmlEncode(a.Title)}</div>
        {(string.IsNullOrEmpty(a.Body) ? "" : $@"<div style=""font-size:13px;color:#6b7a70;margin-top:2px;"">{System.Net.WebUtility.HtmlEncode(a.Body)}</div>")}
      </td>
    </tr>"));

        var textRows = string.Join("\n", ordered.Select(a =>
            $"- [{a.Severity}] {a.Title}{(string.IsNullOrEmpty(a.Body) ? "" : $" — {a.Body}")}"));

        var noun = alerts.Count == 1 ? "alert" : "alerts";
        return (
            Subject: $"{Brand}: {alerts.Count} new {noun} for {farmName}",
            Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">{alerts.Count} new {noun}</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
  <p>Here's what came up on <strong>{System.Net.WebUtility.HtmlEncode(farmName)}</strong> in the last 24 hours.</p>
  <table style=""width:100%;border-collapse:collapse;margin:20px 0;background:#fff;border:1px solid #eee;border-radius:8px;overflow:hidden;"">
    <tbody>{rows}
    </tbody>
  </table>
  <p style=""margin:24px 0;"">
    <a href=""{alertsUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:10px 20px;border-radius:8px;font-weight:600;"">View in {Brand}</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">You're getting this because alert emails are enabled for {System.Net.WebUtility.HtmlEncode(farmName)}. Turn them off in Farm settings.</p>
</div>",
            Text: $"Hi {displayName},\n\n{alerts.Count} new {noun} on {farmName} in the last 24 hours:\n\n{textRows}\n\nView in {Brand}: {alertsUrl}"
        );
    }

    public static (string Subject, string Html, string Text) PasswordReset(string displayName, string resetUrl) =>
    (
        Subject: $"Reset your {Brand} password",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Reset your password</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
  <p>Click the button below to set a new password. The link expires in a few hours.</p>
  <p style=""margin:32px 0;"">
    <a href=""{resetUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Reset password</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">If the button doesn't work, paste this link into your browser:<br/>
    <span style=""word-break:break-all;"">{resetUrl}</span>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">If you didn't request a password reset, you can safely ignore this email — your password won't change.</p>
</div>",
        Text: $"Hi {displayName},\n\nReset your {Brand} password by opening this link:\n{resetUrl}\n\nIf you didn't request this, ignore the email."
    );
}
