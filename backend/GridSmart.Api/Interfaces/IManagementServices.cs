using System.Text.Json;

namespace GridSmart.Api.Interfaces;

/// <summary>O luoi (UC-09, BE-25).</summary>
public interface IGridService
{
    /// <summary>FeatureCollection GeoJSON dang chuoi (dung chung cho /api/grids va /api/dashboard/grid-status).</summary>
    Task<string> GetFeatureCollectionAsync(int? unitId, CancellationToken ct);
    Task<GridDetail> GetAsync(int id, CancellationToken ct);
    Task<GridSaveResult> CreateAsync(GridUpsertRequest request, CancellationToken ct);
    Task<GridSaveResult> UpdateAsync(int id, GridUpsertRequest request, CancellationToken ct);
    Task DeactivateAsync(int id, CancellationToken ct);
    Task<ImportResult> ImportAsync(JsonElement featureCollection, bool dryRun, CancellationToken ct);
    Task AssignOfficersAsync(int id, AssignOfficersRequest request, CancellationToken ct);
}

/// <summary>Don vi hanh chinh (BE-24).</summary>
public interface IAdminUnitService
{
    Task<IReadOnlyList<AdminUnitNode>> GetTreeAsync(int? rootId, int? depth, bool includeInactive, CancellationToken ct);
    Task<AdminUnitDetail> GetAsync(int id, CancellationToken ct);
    Task<AdminUnitDetail?> LocateAsync(double latitude, double longitude, CancellationToken ct);
    Task<IReadOnlyList<AdminLevelDto>> GetLevelsAsync(CancellationToken ct);
    Task<IReadOnlyList<AdminUnitTypeDto>> GetTypesAsync(CancellationToken ct);
    Task<int> CreateAsync(AdminUnitUpsertRequest request, CancellationToken ct);
    Task UpdateAsync(int id, AdminUnitUpsertRequest request, CancellationToken ct);
    Task<MergeResult> MergeAsync(MergeRequest request, CancellationToken ct);
    Task<ImportResult> ImportAsync(JsonElement featureCollection, bool dryRun, CancellationToken ct);
}

/// <summary>Quan ly nguoi dung va gan vai tro theo pham vi (BE-08).</summary>
public interface IUserService
{
    Task<PagedResult<UserListItem>> ListAsync(string? q, string? roleCode, int? unitId, bool? locked, int? page, int? pageSize, CancellationToken ct);
    Task<UserDetail> GetAsync(int id, CancellationToken ct);
    Task<int> CreateAsync(UserCreateRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UserUpdateRequest request, CancellationToken ct);
    Task LockAsync(int id, bool locked, CancellationToken ct);
    Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct);
    Task<IReadOnlyList<UserRoleDto>> GetRolesAsync(int id, CancellationToken ct);
    Task<int> AssignRoleAsync(int id, AssignRoleRequest request, CancellationToken ct);
    Task RevokeRoleAsync(int id, int userRoleId, CancellationToken ct);
}

/// <summary>Quan ly vai tro va quyen (BE-08).</summary>
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct);
    Task<int> CreateAsync(RoleUpsertRequest request, CancellationToken ct);
    Task UpdateAsync(int id, RoleUpsertRequest request, CancellationToken ct);
    Task SetPermissionsAsync(int id, IReadOnlyList<string> permissions, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Danh muc su co va chinh sach SLA (BE-26).</summary>
public interface IConfigService
{
    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(bool includeInactive, CancellationToken ct);
    Task<int> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken ct);
    Task UpdateCategoryAsync(int id, CategoryUpsertRequest request, CancellationToken ct);
    Task<IReadOnlyList<SlaPolicyDto>> ListSlaPoliciesAsync(CancellationToken ct);

    /// <summary>Chi ap dung cho su co dieu phoi sau thoi diem doi (han cu da luu trong sla_deadline).</summary>
    Task UpdateSlaPoliciesAsync(IReadOnlyList<SlaPolicyUpdate> updates, CancellationToken ct);
}
