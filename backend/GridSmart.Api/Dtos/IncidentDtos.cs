using System.Text.Json;

namespace GridSmart.Api.Dtos;

/// <summary>Du lieu gui phan anh (multipart). Anh doc tu Stream.</summary>
public sealed record CreateIncidentCommand(
    Stream Image, long ImageLength,
    double? Latitude, double? Longitude,
    string? Description, int? CategoryId, string? ReporterPhone, int? ReporterUserId, string? AddressText);

public sealed record CreateIncidentResult(string TrackingCode);

public sealed record CategoryDto(int Id, string Code, string Name, int DefaultPriority, bool IsActive);
public sealed record CategorySuggestion(int CategoryId, string Code, string Name, double Confidence);
public sealed record LocateResult(string? HamletName, string? CommuneName, string? ProvinceName, bool InsideGrid);

public sealed record ImageDto(long Id, string Type, string Url, string ThumbnailUrl, double? Latitude, double? Longitude, DateTime UploadedAt);
public sealed record TimelineItem(string Code, string Message, DateTime At);

/// <summary>Ket qua tra cuu cong khai (khong lo ten can bo, ghi chu noi bo).</summary>
public sealed record TrackResult(
    string TrackingCode, string Status, string StatusText, string? CategoryName, string? AddressText,
    double Latitude, double Longitude, DateTime CreatedAt, DateTime? ResolvedAt,
    bool IsMerged, string? MergedIntoTrackingCode,
    IReadOnlyList<ImageDto> BeforeImages, IReadOnlyList<ImageDto> AfterImages,
    IReadOnlyList<TimelineItem> Timeline, bool CanRate, int? Rating, string? RatingFeedback,
    IReadOnlyList<StaffNote> StaffNotes);

/// <summary>Ly do cán bộ đã nhập khi hủy, trả lại hoặc giao lại (không lộ tên cán bộ).</summary>
public sealed record StaffNote(string Action, string Reason, DateTime At);

public sealed record RateRequest(string? Phone, byte Rating, string? Feedback);

public sealed record MyIncidentItem(string TrackingCode, string Status, string? CategoryName, string? AddressText, DateTime CreatedAt, DateTime? ResolvedAt);

public sealed record IncidentListQuery(
    string? Status, int? UnitId, int? GridId, int? CategoryId, int? Priority,
    bool? IsOverdue, bool? IsManualReview, bool? AssignedToMe, DateTime? From, DateTime? To,
    string? Q, bool? IncludeChildren, int? Page, int? PageSize);

public sealed record IncidentListItem(
    long Id, string TrackingCode, string Status, int Priority, string? CategoryName, int? CategoryId,
    string? AddressText, double Latitude, double Longitude,
    int? GridId, string? GridCode, int? AdminUnitId, string? AdminUnitName,
    int? AssignedOfficerId, string? AssignedOfficerName,
    DateTime? SlaDeadline, long? SlaRemainingSeconds, bool IsOverdue, bool IsManualReview, string? ReviewReason,
    int DuplicateCount, long? ParentIncidentId, DateTime CreatedAt, string? ThumbnailUrl);

public sealed record IncidentDetail(
    long Id, string TrackingCode, string Status, int Priority, int? CategoryId, string? CategoryName,
    string? Description, string? AiLabel, decimal? AiConfidence, bool IsManualReview, string? ReviewReason,
    double Latitude, double Longitude, string? AddressText,
    int? GridId, string? GridCode, string? GridName, JsonElement? GridBoundary,
    int? AdminUnitId, IReadOnlyList<UnitRef> AdminUnitPath,
    int? AssignedOfficerId, string? AssignedOfficerName,
    string? ReporterPhone,
    int? SlaPolicyId, DateTime? SlaDeadline, long? SlaRemainingSeconds, DateTime? AcceptedAt,
    bool IsOverdue, DateTime? EscalatedAt, DateTime? ResolvedAt,
    long? ParentIncidentId, string? ParentTrackingCode, int DuplicateCount, int ChildCount,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? ProcessedAt,
    IReadOnlyList<ImageDto> Images, int? Rating, string? RatingFeedback);

public sealed record IncidentLogDto(long Id, string ActionType, string ActionNote, string? OldStatus, string? NewStatus, int? ActionByUserId, string? ActionByName, DateTime CreatedAt);

public sealed record ResolveCommand(Stream Image, long ImageLength, double? Latitude, double? Longitude, string? Note);
public sealed record ReasonRequest(string Reason);
public sealed record ClassifyRequest(int CategoryId, int? Priority);
public sealed record DispatchRequest(int? GridId, int? OfficerId);
public sealed record ReassignRequest(int OfficerId, string Reason);

public sealed record OfficerDto(int Id, string FullName, string? Phone, int OpenIncidentCount, IReadOnlyList<int> GridIds);
