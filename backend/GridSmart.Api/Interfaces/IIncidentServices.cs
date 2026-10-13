using GridSmart.Api.Data;

namespace GridSmart.Api.Interfaces;

/// <summary>Nghiep vu cong khai cho cong dan: UC-01, UC-02, UC-03.</summary>
public interface IPublicIncidentService
{
    Task<CreateIncidentResult> CreateAsync(CreateIncidentCommand command, CancellationToken ct);
    Task<IReadOnlyList<CategorySuggestion>> ClassifyPreviewAsync(Stream image, long length, string? description, CancellationToken ct);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct);
    Task<LocateResult> LocateAsync(double latitude, double longitude, CancellationToken ct);
    Task<TrackResult> TrackAsync(string code, string? phone, int? currentUserId, CancellationToken ct);
    Task<bool> VerifyTrackingAsync(string code, string? phone, CancellationToken ct);
    Task RateAsync(string code, RateRequest request, int? currentUserId, CancellationToken ct);
    Task<IReadOnlyList<MyIncidentItem>> GetMyIncidentsAsync(int userId, CancellationToken ct);
}

/// <summary>Truy van su co cho can bo, dieu phoi vien, lanh dao (BE-17).</summary>
public interface IIncidentQueryService
{
    Task<PagedResult<IncidentListItem>> ListAsync(IncidentListQuery query, CancellationToken ct);
    Task<IncidentDetail> GetAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<IncidentLogDto>> GetLogsAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<IncidentListItem>> GetChildrenAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<IncidentListItem>> GetMyAssignmentsAsync(string? tab, CancellationToken ct);
    Task<IReadOnlyList<OfficerDto>> ListOfficersAsync(int? unitId, int? gridId, CancellationToken ct);
}

/// <summary>Cac thao tac doi trang thai su co cua can bo va dieu phoi vien (BE-18, BE-19).</summary>
public interface IIncidentWorkflowService
{
    Task AcceptAsync(long id, CancellationToken ct);
    Task ResolveAsync(long id, ResolveCommand command, CancellationToken ct);
    Task ReturnAsync(long id, string reason, CancellationToken ct);
    Task ClassifyAsync(long id, ClassifyRequest request, CancellationToken ct);
    Task DispatchAsync(long id, DispatchRequest request, CancellationToken ct);
    Task ReassignAsync(long id, ReassignRequest request, CancellationToken ct);
    Task CancelAsync(long id, string reason, CancellationToken ct);
    /// <summary>Xoa han phan anh, khong can ly do.</summary>
    Task DeleteAsync(long id, CancellationToken ct);
}

/// <summary>Dieu phoi su co cho can bo va dat han SLA (UC-06, BE-16).</summary>
public interface IDispatchService
{
    /// <summary>
    /// Giao su co. officerId = null thi tu chon can bo cua o luoi.
    /// Tra ve false neu khong co can bo (su co chuyen vao hang cho duyet).
    /// </summary>
    Task<bool> DispatchAsync(Row incident, int? officerId, int? actorUserId, CancellationToken ct);

    Task NotifyManualReviewAsync(Row incident, string reason, CancellationToken ct);
}

/// <summary>Pipeline nen cho moi su co moi (BE-12): o luoi -> AI -> gom trung -> dieu phoi.</summary>
public interface IIncidentPipeline
{
    Task ProcessAsync(long incidentId, CancellationToken ct);
}

/// <summary>Quet SLA: leo thang khi qua nguong canh bao, danh dau qua han (BE-20).</summary>
public interface ISlaEngine
{
    Task<SlaRunResult> RunOnceAsync(CancellationToken ct);
}
