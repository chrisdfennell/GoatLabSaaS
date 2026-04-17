namespace GoatLab.Shared.DTOs;

public record PushSubscribeRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent
);

public record PushSubscriptionDto(
    int Id,
    string Endpoint,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime? LastUsedAt
);

public record VapidPublicKeyResponse(string PublicKey);
