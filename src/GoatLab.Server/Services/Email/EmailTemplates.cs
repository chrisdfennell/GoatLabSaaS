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

    public static (string Subject, string Html, string Text) BuyerPortal(string customerName, string farmName, string portalUrl, DateTime expiresAt) =>
    (
        Subject: $"Your reservation at {farmName}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Track your reservation</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(customerName)},</p>
  <p><strong>{System.Net.WebUtility.HtmlEncode(farmName)}</strong> has shared a live view of your reservation with you. Check it anytime — no account or password needed.</p>
  <p style=""margin:32px 0;"">
    <a href=""{portalUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Open your reservation</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">This link works until <strong>{expiresAt:MMMM d, yyyy}</strong>. Keep it private — anyone with the link can see your reservation details.</p>
  <p style=""font-size:13px;color:#6b7a70;"">If the button doesn't work, paste this link into your browser:<br/>
    <span style=""word-break:break-all;"">{portalUrl}</span>
  </p>
</div>",
        Text: $"Hi {customerName},\n\n{farmName} has shared a live view of your reservation with you. Open:\n{portalUrl}\n\nThe link works until {expiresAt:MMMM d, yyyy}."
    );

    public static (string Subject, string Html, string Text) GoatTransferInvite(
        string sellerFarm,
        string goatName,
        string acceptUrl,
        DateTime expiresAt,
        string? message) =>
    (
        Subject: $"{System.Net.WebUtility.HtmlEncode(sellerFarm)} is transferring {System.Net.WebUtility.HtmlEncode(goatName)} to you on {Brand}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">A goat is being transferred to you</h2>
  <p><strong>{System.Net.WebUtility.HtmlEncode(sellerFarm)}</strong> wants to hand <strong>{System.Net.WebUtility.HtmlEncode(goatName)}</strong> over to your herd on {Brand}.</p>
  <p>When you accept, the goat's full record — pedigree links, medical history, weights, photos — moves straight into one of your farms. No re-typing, nothing lost.</p>
  {(string.IsNullOrWhiteSpace(message) ? "" : $@"<blockquote style=""border-left:3px solid #2e7d32;margin:16px 0;padding:8px 16px;color:#444;background:#f6fbf6;border-radius:0 6px 6px 0;"">{System.Net.WebUtility.HtmlEncode(message)}</blockquote>")}
  <p style=""margin:32px 0;"">
    <a href=""{acceptUrl}""
       style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:600;"">Review transfer</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">This link expires on <strong>{expiresAt:MMMM d, yyyy}</strong>. You'll need a {Brand} account to accept — sign up for free if you don't have one yet.</p>
  <p style=""font-size:13px;color:#6b7a70;"">If the button doesn't work, paste this link into your browser:<br/>
    <span style=""word-break:break-all;"">{acceptUrl}</span>
  </p>
</div>",
        Text: $"{sellerFarm} is transferring {goatName} to you on {Brand}. Open the link to review:\n{acceptUrl}\n\nExpires {expiresAt:MMMM d, yyyy}."
    );

    public static (string Subject, string Html, string Text) GoatTransferAccepted(
        string buyerFarm,
        string goatName) =>
    (
        Subject: $"{System.Net.WebUtility.HtmlEncode(buyerFarm)} accepted the transfer of {System.Net.WebUtility.HtmlEncode(goatName)}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Transfer complete</h2>
  <p><strong>{System.Net.WebUtility.HtmlEncode(buyerFarm)}</strong> has accepted the transfer of <strong>{System.Net.WebUtility.HtmlEncode(goatName)}</strong>. The goat's record (with its health, weight, and milk history) is now part of their herd on {Brand}.</p>
  <p style=""font-size:13px;color:#6b7a70;"">Your sales, finance entries, and breeding records referencing this goat remain on your side untouched — only the live record moved.</p>
</div>",
        Text: $"{buyerFarm} accepted the transfer of {goatName}. The record now lives in their herd."
    );

    public static (string Subject, string Html, string Text) GoatTransferDeclined(
        string goatName,
        string? reason) =>
    (
        Subject: $"Transfer of {System.Net.WebUtility.HtmlEncode(goatName)} was declined",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#c62828;margin-bottom:8px;"">Transfer declined</h2>
  <p>The buyer declined the transfer of <strong>{System.Net.WebUtility.HtmlEncode(goatName)}</strong>. The goat stays in your herd as before.</p>
  {(string.IsNullOrWhiteSpace(reason) ? "" : $@"<blockquote style=""border-left:3px solid #c62828;margin:16px 0;padding:8px 16px;color:#444;background:#fdf3f3;border-radius:0 6px 6px 0;"">{System.Net.WebUtility.HtmlEncode(reason)}</blockquote>")}
</div>",
        Text: $"Transfer of {goatName} was declined.{(string.IsNullOrWhiteSpace(reason) ? "" : $"\nReason: {reason}")}"
    );

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

    /// <summary>
    /// Optional invite when the operator types a vet's email at link-creation
    /// time. The plaintext token lives only in the URL of this email; the
    /// server stores the SHA-256 hash.
    /// </summary>
    public static (string Subject, string Html, string Text) VetShareInvite(
        string vetName, string url, DateTime expiresAt) =>
    (
        Subject: $"Goat health record share — {Brand}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Health record shared with you</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(vetName)},</p>
  <p>A {Brand} farmer shared a goat's medical history with you. Click the button below to review records and (optionally) leave a note that lands back on the farm's timeline. No login required.</p>
  <p style=""margin:28px 0;"">
    <a href=""{url}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">Open record</a>
  </p>
  <p style=""font-size:13px;color:#6b7a70;"">Link expires {expiresAt:MMM d, yyyy}. After that, ask the farmer for a fresh link.</p>
</div>",
        Text: $"A {Brand} farmer shared a goat health record with you.\nOpen: {url}\nExpires: {expiresAt:MMM d, yyyy}"
    );

    /// <summary>
    /// Sent when a buyer subscribes to a saved search at /breeds/{slug}.
    /// Single-CTA: "View this listing." Each match notification carries its
    /// own unsubscribe link tied to that buyer's saved search.
    /// </summary>
    public static (string Subject, string Html, string Text) MarketplaceAlertMatch(
        string criteriaLabel, string farmName, string goatName, string? breed,
        string? gender, int? priceCents, string? primaryPhotoUrl, string listingUrl,
        string unsubscribeUrl)
    {
        var priceLine = priceCents.HasValue
            ? $"<p style=\"font-size:24px;color:#2e7d32;font-weight:700;margin:12px 0 4px 0;\">${(priceCents.Value / 100m):N0}</p>"
            : "";
        var photoLine = string.IsNullOrEmpty(primaryPhotoUrl)
            ? ""
            : $"<a href=\"{listingUrl}\"><img src=\"{primaryPhotoUrl}\" alt=\"{System.Net.WebUtility.HtmlEncode(goatName)}\" style=\"width:100%;max-width:480px;border-radius:8px;display:block;margin:16px 0;\" /></a>";
        var detailsLine = string.Join(" · ", new[] { breed, gender, $"at {farmName}" }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var subject = priceCents.HasValue
            ? $"New match: {goatName} — ${(priceCents.Value / 100m):N0}"
            : $"New match: {goatName} at {farmName}";

        var html = $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <p style=""font-size:13px;color:#6b7a70;margin:0;"">Saved search match · {Brand}</p>
  <h2 style=""color:#2e7d32;margin:4px 0 6px 0;"">{System.Net.WebUtility.HtmlEncode(goatName)}</h2>
  <p style=""color:#6b7a70;font-size:13px;margin:0 0 8px 0;"">Matches your search: <em>{System.Net.WebUtility.HtmlEncode(criteriaLabel)}</em></p>
  {photoLine}
  {priceLine}
  <p style=""color:#4a5a51;margin:0 0 16px 0;"">{System.Net.WebUtility.HtmlEncode(detailsLine)}</p>
  <p style=""margin:24px 0;"">
    <a href=""{listingUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">View this listing</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">Set up too many alerts? <a href=""{unsubscribeUrl}"">Unsubscribe from this saved search</a> in one click.</p>
</div>";
        var text = $"New match for your search ({criteriaLabel}):\n{goatName} · {detailsLine}\n{(priceCents.HasValue ? $"${(priceCents.Value / 100m):N0}\n" : "")}View: {listingUrl}\n\nUnsubscribe: {unsubscribeUrl}";
        return (subject, html, text);
    }

    /// <summary>
    /// Sent when a buyer registers a saved search. Confirms the criteria and
    /// includes the unsubscribe link.
    /// </summary>
    public static (string Subject, string Html, string Text) MarketplaceAlertConfirmation(
        string criteriaLabel, string browseUrl, string unsubscribeUrl) =>
    (
        Subject: $"Saved search confirmed — {Brand}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">Your search is saved</h2>
  <p>You'll get an email from {Brand} when a new listing matches:</p>
  <p style=""background:#f1f8e9;border-left:4px solid #2e7d32;padding:10px 14px;margin:14px 0;border-radius:4px;"">
    <strong>{System.Net.WebUtility.HtmlEncode(criteriaLabel)}</strong>
  </p>
  <p>One email per match. No daily digests, no marketing.</p>
  <p style=""margin:24px 0;"">
    <a href=""{browseUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">Browse current listings</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">Changed your mind? <a href=""{unsubscribeUrl}"">Unsubscribe</a> in one click.</p>
</div>",
        Text: $"Saved search on {Brand}: {criteriaLabel}\nYou'll get one email per match.\nBrowse: {browseUrl}\nUnsubscribe: {unsubscribeUrl}"
    );

    /// <summary>
    /// Sent when an anonymous follower of a farm subscribes via the
    /// "Follow this farm" form on /pub/{slug}. Confirms the subscription
    /// and includes the unsubscribe link inline.
    /// </summary>
    public static (string Subject, string Html, string Text) FarmFollowConfirmation(
        string farmName, string farmUrl, string unsubscribeUrl) =>
    (
        Subject: $"You're following {farmName} on {Brand}",
        Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:8px;"">You're now following {System.Net.WebUtility.HtmlEncode(farmName)}</h2>
  <p>You'll get one email from {Brand} when {System.Net.WebUtility.HtmlEncode(farmName)} lists a new goat for sale. Nothing else — no marketing, no daily digests.</p>
  <p style=""margin:24px 0;"">
    <a href=""{farmUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">Visit the farm</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">Don't want these notifications? <a href=""{unsubscribeUrl}"">Unsubscribe with one click</a> — no login required.</p>
</div>",
        Text: $"You're now following {farmName} on {Brand}.\nYou'll get one email when they list a new goat. Visit: {farmUrl}\nUnsubscribe: {unsubscribeUrl}"
    );

    /// <summary>
    /// Sent to every active follower of a farm when one of its goats gets
    /// flipped to IsListedForSale=true. Single-CTA: "View this listing".
    /// </summary>
    public static (string Subject, string Html, string Text) NewListingNotification(
        string farmName, string goatName, string? breed, string? gender, int? priceCents,
        string? primaryPhotoUrl, string listingUrl, string unsubscribeUrl)
    {
        var priceLine = priceCents.HasValue
            ? $"<p style=\"font-size:24px;color:#2e7d32;font-weight:700;margin:12px 0 4px 0;\">${(priceCents.Value / 100m):N0}</p>"
            : "";
        var photoLine = string.IsNullOrEmpty(primaryPhotoUrl)
            ? ""
            : $"<a href=\"{listingUrl}\"><img src=\"{primaryPhotoUrl}\" alt=\"{System.Net.WebUtility.HtmlEncode(goatName)}\" style=\"width:100%;max-width:480px;border-radius:8px;display:block;margin:16px 0;\" /></a>";
        var detailsLine = string.Join(" · ", new[] { breed, gender }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var subject = priceCents.HasValue
            ? $"{farmName} listed {goatName} — ${(priceCents.Value / 100m):N0}"
            : $"{farmName} listed {goatName}";

        var html = $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <p style=""font-size:13px;color:#6b7a70;margin:0;"">{Brand} marketplace</p>
  <h2 style=""color:#2e7d32;margin:4px 0 8px 0;"">{System.Net.WebUtility.HtmlEncode(farmName)} listed {System.Net.WebUtility.HtmlEncode(goatName)} for sale</h2>
  {photoLine}
  {priceLine}
  <p style=""color:#4a5a51;margin:0 0 16px 0;"">{System.Net.WebUtility.HtmlEncode(detailsLine)}</p>
  <p style=""margin:24px 0;"">
    <a href=""{listingUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">View this listing</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">You're getting this because you're following {System.Net.WebUtility.HtmlEncode(farmName)} on {Brand}. <a href=""{unsubscribeUrl}"">Unsubscribe</a> any time.</p>
</div>";
        var text = $"{farmName} listed {goatName} for sale.\n{detailsLine}\n{(priceCents.HasValue ? $"${(priceCents.Value / 100m):N0}\n" : "")}View: {listingUrl}\n\nUnsubscribe: {unsubscribeUrl}";
        return (subject, html, text);
    }

    /// <summary>
    /// Branded chrome for super-admin bulk announcements ("we're moving servers
    /// Sunday at 2am"). Wraps the operator's message in a header bar, greeting,
    /// and a footer that points back to GoatLab. Operator types either prose
    /// (newlines auto-converted to &lt;br&gt;) or raw HTML; the template detects
    /// which and passes through accordingly.
    /// </summary>
    /// <param name="displayName">Recipient's display name. Empty/null falls back to "there".</param>
    /// <param name="subject">Subject line — also rendered as the email's H1 inside the body.</param>
    /// <param name="messageBody">Operator-supplied content. Auto-formatted if it contains no HTML tags.</param>
    /// <param name="preheader">Optional short string shown by mail clients next to the subject in inbox preview. Falls back to a generic line.</param>
    public static (string Subject, string Html, string Text) BulkAnnouncement(
        string displayName,
        string subject,
        string messageBody,
        string? preheader = null)
    {
        var greeting = string.IsNullOrWhiteSpace(displayName)
            ? "Hi there,"
            : $"Hi {System.Net.WebUtility.HtmlEncode(displayName.Trim())},";

        // If the body has no obvious tags, treat it as prose: encode it and turn
        // double newlines into paragraphs / single newlines into <br>. Otherwise
        // pass it through (operator deliberately wrote HTML).
        var renderedBody = LooksLikeHtml(messageBody)
            ? messageBody
            : ProseToHtml(messageBody);

        var preheaderText = string.IsNullOrWhiteSpace(preheader)
            ? "An update from GoatLab."
            : preheader.Trim();
        var preheaderHtml = System.Net.WebUtility.HtmlEncode(preheaderText);
        var encodedSubject = System.Net.WebUtility.HtmlEncode(subject);

        var html = $@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#f3f5f3;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1a2421;"">
  <span style=""display:none;font-size:0;line-height:0;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;"">{preheaderHtml}</span>
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#f3f5f3;padding:24px 0;"">
    <tr><td align=""center"">
      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;width:100%;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.06);"">
        <tr><td style=""background:#1b5e20;padding:18px 28px;"">
          <a href=""https://goatlab.app"" style=""text-decoration:none;color:#fff;font-weight:700;font-size:18px;letter-spacing:0.3px;"">
            🐐 {Brand}
          </a>
        </td></tr>
        <tr><td style=""padding:28px;"">
          <h1 style=""font-size:22px;color:#102a1a;margin:0 0 16px 0;line-height:1.25;"">{encodedSubject}</h1>
          <p style=""margin:0 0 16px 0;"">{greeting}</p>
          <div style=""font-size:15px;line-height:1.6;color:#1a2421;"">
            {renderedBody}
          </div>
          <p style=""margin:24px 0 0 0;font-size:14px;color:#4a5a51;"">— The {Brand} team</p>
        </td></tr>
        <tr><td style=""background:#f7f8f7;padding:18px 28px;border-top:1px solid #e3e8e4;font-size:12px;color:#6b7a70;"">
          You're receiving this because you own a {Brand} farm. Service-critical messages
          (security, billing, maintenance) cannot be opted out of.<br/>
          <a href=""https://goatlab.app"" style=""color:#2e7d32;text-decoration:none;"">goatlab.app</a> ·
          <a href=""https://goatlab.app/account/settings"" style=""color:#2e7d32;text-decoration:none;"">Account settings</a> ·
          <a href=""https://goatlab.app/privacy"" style=""color:#2e7d32;text-decoration:none;"">Privacy</a>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";

        var plainGreeting = string.IsNullOrWhiteSpace(displayName) ? "Hi there," : $"Hi {displayName.Trim()},";
        var text = $"{plainGreeting}\n\n{HtmlToPlainText(renderedBody)}\n\n— The {Brand} team\n\ngoatlab.app";

        return (subject, html, text);
    }

    /// <summary>Sent to the seller when a buyer sends an inquiry on a listing.</summary>
    public static (string Subject, string Html, string Text) BuyerInquiryNew(
        string buyerName, string buyerEmail, string? buyerPhone, string goatName, string farmName,
        string messageBody, string inboxUrl)
    {
        var phoneLine = string.IsNullOrEmpty(buyerPhone) ? "" :
            $@"<p style=""margin:4px 0;color:#4a5a51;""><strong>Phone:</strong> {System.Net.WebUtility.HtmlEncode(buyerPhone)}</p>";
        return (
            Subject: $"New inquiry on {goatName} from {buyerName}",
            Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:4px;"">New buyer inquiry</h2>
  <p style=""margin:0 0 16px 0;color:#6b7a70;"">on <strong>{System.Net.WebUtility.HtmlEncode(goatName)}</strong></p>
  <div style=""background:#f5f5f5;padding:14px 16px;border-radius:8px;margin-bottom:16px;"">
    <p style=""margin:4px 0;""><strong>{System.Net.WebUtility.HtmlEncode(buyerName)}</strong></p>
    <p style=""margin:4px 0;color:#4a5a51;""><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(buyerEmail)}</p>
    {phoneLine}
  </div>
  <p style=""margin:16px 0 8px 0;font-weight:600;"">Their message:</p>
  <blockquote style=""margin:0;padding:12px 16px;background:#fff;border-left:4px solid #2e7d32;color:#1a2421;white-space:pre-wrap;"">{System.Net.WebUtility.HtmlEncode(messageBody)}</blockquote>
  <p style=""margin:24px 0;"">
    <a href=""{inboxUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">Reply on {Brand}</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">{System.Net.WebUtility.HtmlEncode(farmName)} · Replies sent from your inbox go directly to {System.Net.WebUtility.HtmlEncode(buyerEmail)}.</p>
</div>",
            Text: $"New inquiry on {goatName} from {buyerName} ({buyerEmail}{(string.IsNullOrEmpty(buyerPhone) ? "" : ", " + buyerPhone)}):\n\n{messageBody}\n\nReply at: {inboxUrl}"
        );
    }

    /// <summary>Sent to the buyer when the seller replies inside the app.</summary>
    public static (string Subject, string Html, string Text) BuyerInquiryReply(
        string buyerName, string sellerName, string farmName, string goatName, string replyBody,
        string listingUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(buyerName) ? "Hi there," : $"Hi {buyerName.Trim()},";
        return (
            Subject: $"{farmName} replied about {goatName}",
            Html: $@"<div style=""font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:540px;margin:0 auto;padding:24px;color:#1a2421;"">
  <h2 style=""color:#2e7d32;margin-bottom:4px;"">{System.Net.WebUtility.HtmlEncode(sellerName)} replied</h2>
  <p style=""margin:0 0 16px 0;color:#6b7a70;"">about <strong>{System.Net.WebUtility.HtmlEncode(goatName)}</strong> at {System.Net.WebUtility.HtmlEncode(farmName)}</p>
  <p>{System.Net.WebUtility.HtmlEncode(greeting)}</p>
  <blockquote style=""margin:16px 0;padding:12px 16px;background:#f5f5f5;border-left:4px solid #2e7d32;color:#1a2421;white-space:pre-wrap;"">{System.Net.WebUtility.HtmlEncode(replyBody)}</blockquote>
  <p style=""margin:24px 0;"">
    <a href=""{listingUrl}"" style=""display:inline-block;background:#2e7d32;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:600;"">View the listing</a>
  </p>
  <p style=""font-size:12px;color:#6b7a70;"">Reply to this email to continue the conversation. Your reply will reach {System.Net.WebUtility.HtmlEncode(farmName)} directly.</p>
</div>",
            Text: $"{greeting}\n\n{sellerName} from {farmName} replied about {goatName}:\n\n{replyBody}\n\nView the listing: {listingUrl}\n\nReply to this email to continue the conversation."
        );
    }

    private static bool LooksLikeHtml(string? s) =>
        !string.IsNullOrWhiteSpace(s) &&
        (s!.Contains('<') && s.Contains('>'));

    private static string ProseToHtml(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var encoded = System.Net.WebUtility.HtmlEncode(s.Trim());
        // Paragraph on blank lines, soft-break otherwise.
        var paragraphs = encoded.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
        return string.Join("\n",
            paragraphs.Select(p => "<p style=\"margin:0 0 14px 0;\">"
                + p.Replace("\r\n", "<br/>").Replace("\n", "<br/>")
                + "</p>"));
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        var collapsed = System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ").Trim();
        return System.Net.WebUtility.HtmlDecode(collapsed);
    }
}
