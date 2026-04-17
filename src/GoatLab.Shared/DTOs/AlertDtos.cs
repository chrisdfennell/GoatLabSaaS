using GoatLab.Shared.Models;

namespace GoatLab.Shared.DTOs;

public record AlertDto(
    int Id,
    AlertType Type,
    AlertSeverity Severity,
    string Title,
    string? Body,
    string? EntityType,
    int? EntityId,
    string? DeepLink,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? DismissedAt
);
