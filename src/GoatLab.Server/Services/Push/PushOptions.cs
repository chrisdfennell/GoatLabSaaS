namespace GoatLab.Server.Services.Push;

public class PushOptions
{
    public const string SectionName = "Push";

    // VAPID keypair. Generate once via `npx web-push generate-vapid-keys` and
    // configure as Push:VapidPublicKey / Push:VapidPrivateKey (or env vars
    // PUSH__VAPIDPUBLICKEY / PUSH__VAPIDPRIVATEKEY). PushService is a no-op
    // when either is blank, so the rest of the app stays usable.
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }

    // Mailto: URI sent to push services as the contact for delivery issues.
    // Required by RFC 8292 even though most browsers ignore it.
    public string? Subject { get; set; }
}
