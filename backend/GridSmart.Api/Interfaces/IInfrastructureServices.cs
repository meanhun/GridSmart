namespace GridSmart.Api.Interfaces;

// Cac interface cho dich vu ha tang: CSDL khong gian, luu anh, AI, JWT, SignalR, hang doi nen.

/// <summary>Mot quyen kem pham vi. UnitPath = null nghia la toan he thong.</summary>
public sealed record PermissionScope(string Permission, string? UnitPath)
{
    public bool IsGlobal => UnitPath is null;

    /// <summary>Dang claim trong JWT: "incident.view@/1/2/" hoac "sla.manage@*".</summary>
    public string ToClaim() => $"{Permission}@{UnitPath ?? "*"}";

    public static PermissionScope? FromClaim(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0) return null;
        var path = value[(at + 1)..];
        return new PermissionScope(value[..at], path == "*" ? null : path);
    }
}

/// <summary>Nguoi dung cua request hien tai (doc tu JWT).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    IReadOnlyList<PermissionScope> Scopes { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}

/// <summary>Hang doi su co cho pipeline nen.</summary>
public interface IIncidentQueue
{
    void Enqueue(long incidentId);
    IAsyncEnumerable<long> DequeueAllAsync(CancellationToken cancellationToken);
}

/// <summary>Phat su kien realtime (cai dat bang SignalR o tang Api).</summary>
public interface IRealtimePublisher
{
    /// <summary>Phat toi nhom cua cac don vi (va nhom toan he thong).</summary>
    Task PublishToUnitsAsync(string eventName, object payload, IEnumerable<int> unitIds, CancellationToken ct = default);
    Task PublishToUserAsync(int userId, string eventName, object payload, CancellationToken ct = default);
    Task PublishToTrackingAsync(string trackingCode, string eventName, object payload, CancellationToken ct = default);
}

/// <summary>Ket qua luu anh.</summary>
public sealed record StoredImage(string Url, string ThumbnailUrl, double? ExifLatitude, double? ExifLongitude);

/// <summary>Kiem tra, xu ly va luu anh hien truong.</summary>
public interface IImageStore
{
    Task<StoredImage> SaveAsync(Stream content, long length, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string url, CancellationToken ct = default);
}

public sealed record ClassificationResult(string? CategoryCode, string? Label, double Confidence);

/// <summary>AI phan loai su co (Mock / OpenAI / Http).</summary>
public interface IImageClassifier
{
    Task<ClassificationResult> ClassifyAsync(byte[]? image, string? description, CancellationToken ct = default);
}

public sealed record GridMatch(int GridId, string GridCode, string Name, int AdminUnitId, int? AssignedOfficerId);
public sealed record AdminUnitMatch(int Id, string UnitCode, string Name, string LevelCode, string UnitPath);
public sealed record EscalationTarget(int UserId, string FullName, int ScopeUnitId, string ScopeUnitName, string LevelCode, int LevelOrder);
public sealed record UnitSummaryRow(int UnitId, string UnitCode, string Name, int TotalIncidents, int OpenIncidents, int ResolvedIncidents, int OverdueIncidents);

/// <summary>Repository goi cac thu tuc va ham khong gian trong SQL Server.</summary>
public interface ISpatialRepository
{
    Task<GridMatch?> FindGridByPointAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<AdminUnitMatch?> FindAdminUnitByPointAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<long?> FindParentIncidentAsync(double latitude, double longitude, int categoryId, double radiusMeters, int windowHours, long? beforeId = null, CancellationToken ct = default);
    Task<bool?> IsPointInIncidentGridAsync(long incidentId, double latitude, double longitude, double toleranceMeters, CancellationToken ct = default);
    Task<IReadOnlyList<EscalationTarget>> FindEscalationTargetsAsync(long incidentId, string? levelCode, CancellationToken ct = default);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Sinh JWT va refresh token.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(int userId, string fullName, Guid securityStamp, IEnumerable<string> roles, IEnumerable<PermissionScope> scopes);
    string GenerateRefreshToken();
    string Hash(string rawToken);
}

/// <summary>Xuat file Excel.</summary>
public interface IExcelExporter
{
    byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<object?[]> rows);
}

/// <summary>Su kien realtime (tai lieu backend muc 8.2).</summary>
public static class RealtimeEvents
{
    public const string IncidentCreated = "incident.created";
    public const string IncidentAssigned = "incident.assigned";
    public const string IncidentUpdated = "incident.updated";
    public const string IncidentEscalated = "incident.escalated";
    public const string IncidentOverdue = "incident.overdue";
    public const string IncidentResolved = "incident.resolved";
    public const string GridStatusChanged = "grid.status-changed";
    public const string NotificationCreated = "notification.created";
}
