namespace GridSmart.Api.Dtos;

// ---------- Nguoi dung, vai tro, quyen (BE-08) ----------
public sealed record UserRoleDto(int Id, int RoleId, string RoleCode, string RoleName, int? AdminUnitId, string? AdminUnitName,
    string? UnitPath, int? AssignedByUserId, string? AssignedByName, DateTime AssignedAt, DateTime? ExpiresAt, bool IsActive);
public sealed record UserListItem(int Id, string? Username, string FullName, string? Phone, string? Email, bool IsActive, DateTime? LockedUntil,
    DateTime? LastLoginAt, IReadOnlyList<string> RoleSummary);
public sealed record UserDetail(int Id, string? Username, string FullName, string? Phone, string? Email, bool IsActive, DateTime? LockedUntil,
    DateTime? LastLoginAt, DateTime CreatedAt, IReadOnlyList<UserRoleDto> Roles);
public sealed record UserCreateRequest(string Username, string FullName, string? Phone, string? Email, string Password);
public sealed record UserUpdateRequest(string FullName, string? Phone, string? Email, bool? IsActive);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record AssignRoleRequest(int RoleId, int? AdminUnitId, DateTime? ExpiresAt);

public sealed record PermissionDto(int Id, string Code, string Name, string Module);
public sealed record RoleDto(int Id, string RoleCode, string RoleName, string? Description, bool IsSystem, bool IsActive, IReadOnlyList<string> Permissions);
public sealed record RoleUpsertRequest(string RoleCode, string RoleName, string? Description, IReadOnlyList<string>? Permissions);

// ---------- Danh muc va SLA (BE-26) ----------
public sealed record CategoryUpsertRequest(string Code, string Name, int DefaultPriority, bool? IsActive);
public sealed record SlaPolicyDto(int Id, int Priority, int SlaLevel, int SlaHours, int EscalationPercent, string? Description);
public sealed record SlaPolicyUpdate(int Priority, int SlaHours, int EscalationPercent, string? Description);

// ---------- Dashboard (BE-27) ----------
public sealed record DashboardSummary(
    int OpenIncidents, int OverdueIncidents, int PendingReview, int ResolvedToday, int CreatedToday,
    double? AvgDispatchMinutes, double? AvgResolutionHours, double DuplicateRate, double? OnTimeRate);
public sealed record HeatPoint(double Lat, double Lon, int Weight);
public sealed record TrendPoint(DateOnly Date, int Created, int Resolved);

// ---------- Bao cao (BE-28) ----------
public sealed record UnitReportRow(int UnitId, string UnitCode, string Name, int TotalIncidents, int OpenIncidents, int ResolvedIncidents, int OverdueIncidents);
public sealed record CategoryReportRow(int? CategoryId, string CategoryName, int Total, int Open, int Resolved, int Overdue);
public sealed record OfficerKpiRow(int OfficerId, string FullName, int Assigned, int Resolved, double? OnTimeRate,
    double? AvgAcceptMinutes, double? AvgResolveHours, double? AvgRating, int EscalatedCount, int OpenNow);
public sealed record ExportFile(byte[] Content, string ContentType, string FileName);
