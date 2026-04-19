namespace GoatLab.Shared.Models;

// Canonical string event names for outbound webhooks. Stored in
// Webhook.Events (comma-separated). New events can be added without a
// migration — existing subscribers just won't fire for them.
public static class WebhookEventTypes
{
    public const string GoatCreated = "goat.created";
    public const string GoatUpdated = "goat.updated";
    public const string GoatDeleted = "goat.deleted";

    public const string SaleCreated = "sale.created";
    public const string SaleUpdated = "sale.updated";

    public const string KiddingRecorded = "kidding.recorded";
    public const string MedicalRecorded = "medical.recorded";

    public const string GoatTransferInitiated = "goat.transfer.initiated";
    public const string GoatTransferAccepted = "goat.transfer.accepted";
    public const string GoatTransferDeclined = "goat.transfer.declined";

    public const string Ping = "ping"; // sent by the "Test" button on the UI

    public static readonly IReadOnlyList<string> All = new[]
    {
        GoatCreated, GoatUpdated, GoatDeleted,
        SaleCreated, SaleUpdated,
        KiddingRecorded, MedicalRecorded,
        GoatTransferInitiated, GoatTransferAccepted, GoatTransferDeclined,
    };
}
