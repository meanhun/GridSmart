namespace GridSmart.Api.Dtos;

/// <summary>Ket qua phan trang chuan: { items, page, pageSize, totalCount }.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Mot nut trong duong dan don vi hanh chinh (Tinh > Phuong > Khu pho).</summary>
public sealed record UnitRef(int Id, string Code, string Name, string LevelCode);

/// <summary>Id cua ban ghi vua tao.</summary>
public sealed record CreatedId(int Id);

public sealed record NotificationDto(long Id, string Type, string Title, string? Content, long? IncidentId, bool IsRead, DateTime CreatedAt);
public sealed record UnreadCount(int Count);

public sealed record SlaRunResult(int Escalated, int Overdue);
