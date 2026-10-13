using GridSmart.Api.Data;

namespace GridSmart.Api.DataAccess.Repositories;

/// <summary>Goi cac thu tuc/ham khong gian trong database/03_spatial_procedures.sql qua ProcExecutor.</summary>
public sealed class SpatialRepository : ISpatialRepository
{
    // ct duoc nhan de khop interface, nhung ProcExecutor hien khong nhan CancellationToken.
    public async Task<GridMatch?> FindGridByPointAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var r = await ProcExecutor.QuerySingleAsync("sp_find_grid_by_point",
            ProcExecutor.P("@latitude", latitude),
            ProcExecutor.P("@longitude", longitude));
        return r is null
            ? null
            : new GridMatch(r.GetInt("id"), r.GetString("grid_code"), r.GetString("name"),
                r.GetInt("admin_unit_id"), r.GetIntOrNull("assigned_officer_id"));
    }

    public async Task<AdminUnitMatch?> FindAdminUnitByPointAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var r = await ProcExecutor.QuerySingleAsync("sp_find_admin_unit_by_point",
            ProcExecutor.P("@latitude", latitude),
            ProcExecutor.P("@longitude", longitude));
        return r is null
            ? null
            : new AdminUnitMatch(r.GetInt("id"), r.GetString("unit_code"), r.GetString("name"),
                r.GetString("level_code"), r.GetString("unit_path"));
    }

    public async Task<long?> FindParentIncidentAsync(double latitude, double longitude, int categoryId, double radiusMeters, int windowHours, long? beforeId = null, CancellationToken ct = default)
    {
        var r = await ProcExecutor.QuerySingleAsync("sp_find_parent_incident",
            ProcExecutor.P("@latitude", latitude),
            ProcExecutor.P("@longitude", longitude),
            ProcExecutor.P("@category_id", categoryId),
            ProcExecutor.P("@radius_m", radiusMeters),
            ProcExecutor.P("@window_hours", windowHours),
            ProcExecutor.P("@before_id", beforeId));
        return r?.GetLong("id");
    }

    public async Task<bool?> IsPointInIncidentGridAsync(long incidentId, double latitude, double longitude, double toleranceMeters, CancellationToken ct = default)
    {
        // Ham vo huong: goi bang lenh SQL van ban (khong phai proc), van tham gia giao dich hien tai.
        var result = await ProcExecutor.ScalarSqlAsync(
            "SELECT dbo.fn_is_point_in_incident_grid(@incident_id, @latitude, @longitude, @tolerance_m)",
            ProcExecutor.P("@incident_id", incidentId),
            ProcExecutor.P("@latitude", latitude),
            ProcExecutor.P("@longitude", longitude),
            ProcExecutor.P("@tolerance_m", toleranceMeters));
        return result is null or DBNull ? null : Convert.ToBoolean(result);
    }

    public async Task<IReadOnlyList<EscalationTarget>> FindEscalationTargetsAsync(long incidentId, string? levelCode, CancellationToken ct = default)
    {
        var rows = await ProcExecutor.QueryAsync("sp_find_escalation_targets",
            ProcExecutor.P("@incident_id", incidentId),
            ProcExecutor.P("@level_code", levelCode));
        return rows.Select(r => new EscalationTarget(
            r.GetInt("user_id"), r.GetString("full_name"),
            r.GetInt("scope_unit_id"), r.GetString("scope_unit_name"),
            r.GetString("ancestor_level_code"), r.GetInt("ancestor_level_order"))).ToList();
    }
}
