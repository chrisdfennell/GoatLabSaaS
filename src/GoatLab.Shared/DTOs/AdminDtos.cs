namespace GoatLab.Shared.DTOs;

public record AdminTenantRow(
    int Id,
    string Name,
    string Slug,
    string? Location,
    DateTime CreatedAt,
    int MemberCount,
    int GoatCount,
    DateTime? LastActivityAt
);

public record AdminUserRow(
    string Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt,
    bool IsSuperAdmin,
    int MembershipCount
);

public record AdminMetrics(
    int TenantCount,
    int UserCount,
    int GoatCount,
    int ActiveTenantsLast30Days,
    int SignupsLast7Days,
    int SignupsLast30Days
);

public record AdminTimeseriesPoint(DateTime Date, int Tenants, int Users, int Goats);
public record AdminTimeseries(IReadOnlyList<AdminTimeseriesPoint> Points);

public record AdminTenantMemberRow(
    string UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTime JoinedAt
);

public record AdminTenantDetail(
    int Id,
    string Name,
    string Slug,
    string? Location,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int GoatCount,
    int MilkLogCount,
    int MedicalRecordCount,
    DateTime? LastActivityAt,
    IReadOnlyList<AdminTenantMemberRow> Members,
    DateTime? SuspendedAt,
    string? SuspensionReason,
    DateTime? DeletedAt,
    string? Notes,
    string? Tag,
    IReadOnlyDictionary<string, bool> FeatureFlags
);

public record AdminUserMembershipRow(
    int TenantId,
    string TenantName,
    string TenantSlug,
    string Role,
    DateTime JoinedAt
);

public record AdminUserDetail(
    string Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt,
    bool IsSuperAdmin,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    DateTime? DeletedAt,
    IReadOnlyList<AdminUserMembershipRow> Memberships
);

public record AdminRenameTenantRequest(string Name);
public record AdminToggleSuperAdminRequest(bool IsSuperAdmin);
public record AdminResetPasswordResponse(string NewPassword);

public record AdminSuspendTenantRequest(string? Reason);
public record AdminTenantNotesRequest(string? Notes);
public record AdminTenantTagRequest(string? Tag);
public record AdminTenantFlagsRequest(IReadOnlyDictionary<string, bool> Flags);
public record AdminLockUserRequest(int? DurationHours);
public record AdminMaintenanceStatus(bool Enabled, DateTime? EnabledAt);
public record AdminMaintenanceRequest(bool Enabled);

public record AdminAnnouncementRow(
    int Id,
    string Title,
    string Body,
    string Severity,
    string? TargetTag,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    DateTime CreatedAt
);

public record AdminAnnouncementUpsert(
    string Title,
    string Body,
    string Severity,
    string? TargetTag,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive
);

public record ActiveAnnouncement(
    int Id,
    string Title,
    string Body,
    string Severity
);

public record AdminAuditRow(
    int Id,
    DateTime At,
    string ActorEmail,
    string Action,
    string? TargetType,
    string? TargetId,
    string? Detail
);

public record AdminAuditPage(
    IReadOnlyList<AdminAuditRow> Rows,
    int Page,
    int PageSize,
    int TotalRows
);

public record ImpersonationState(
    int TenantId,
    string TenantName,
    int OriginalTenantId,
    string OriginalTenantName
);
