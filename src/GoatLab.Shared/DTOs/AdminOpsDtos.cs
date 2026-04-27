namespace GoatLab.Shared.DTOs;

// --- /admin/email-log ---

public record EmailLogRowDto(
    int Id,
    DateTime At,
    string ToAddress,
    string? Subject,
    string Status,
    string? Error,
    int? TenantId,
    string? Sender,
    int? BodyBytes);

public record EmailLogPageDto(
    IReadOnlyList<EmailLogRowDto> Rows,
    int Total);


// --- /admin/search ---

public record AdminSearchRequest(
    string Query,
    int Limit = 20);

public record AdminSearchHit(
    string Type,
    string Title,
    string Subtitle,
    string DeepLink,
    string? TenantSlug,
    string? TenantName);

public record AdminSearchResponse(
    string Query,
    IReadOnlyList<AdminSearchHit> Hits,
    int TotalReturned,
    bool Truncated);


// --- /admin/billing/sync/{tenantId} ---

public record StripeSyncResultDto(
    int TenantId,
    string TenantSlug,
    bool Found,
    string? Message,
    IReadOnlyList<string> Changes);


// --- /admin/billing/replay/{eventId} ---

public record StripeReplayResultDto(
    string EventId,
    string EventType,
    bool Handled,
    string? Message);


// --- /admin/bulk-email ---

public record BulkEmailRequest(
    string Audience,           // "all-owners" | "active-paid" | "trial" | "past-due"
    string Subject,
    string HtmlBody,
    bool DryRun);

public record BulkEmailResultDto(
    string Audience,
    int RecipientCount,
    bool DryRun,
    int SentCount,
    int FailedCount,
    IReadOnlyList<string> SampleRecipients);
