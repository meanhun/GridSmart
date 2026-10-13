using System.Text.Json;
using GridSmart.Api.Data;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace GridSmart.Api.Services;

/// <summary>O luoi (UC-09, BE-25).</summary>
public sealed class GridService(
    GridsDataAccess grids,
    GridOfficersDataAccess gridOfficers,
    AdminUnitsDataAccess units,
    UsersDataAccess users,
    IScopeService scope,
    TimeProvider clock) : IGridService
{
    public async Task<string> GetFeatureCollectionAsync(int? unitId, CancellationToken ct)
    {
        var fc = new FeatureCollection();
        var scopePaths = scope.ScopePaths(Permissions.GridView);
        if (scopePaths == "") return JsonSerializer.Serialize(fc, GeoJson.Options);

        string? unitPath = null;
        if (unitId is { } uid)
        {
            var unit = await units.ReadByIdAsync(uid) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
            unitPath = unit.GetStringOrNull("unit_path") ?? "";
        }

        var rows = await grids.ReadStatusAsync(scopePaths, unitPath, null);
        foreach (var g in rows.OrderBy(r => r.GetString("grid_code")))
        {
            fc.Add(new Feature(GeoJson.FromWkt(g.GetStringOrNull("boundary_wkt")), new AttributesTable
            {
                { "gridId", g.GetInt("grid_id") },
                { "gridCode", g.GetString("grid_code") },
                { "name", g.GetString("name") },
                { "adminUnitId", g.GetInt("admin_unit_id") },
                { "hamletName", g.GetStringOrNull("hamlet_name") },
                { "communeId", g.GetIntOrNull("commune_id") },
                { "communeName", g.GetStringOrNull("commune_name") },
                { "statusColor", g.GetStringOrNull("status_color") },
                { "openIncidentCount", g.GetInt("open_incident_count") },
                { "overdueIncidentCount", g.GetInt("overdue_incident_count") },
            }));
        }
        return JsonSerializer.Serialize(fc, GeoJson.Options);
    }

    public async Task<GridDetail> GetAsync(int id, CancellationToken ct)
    {
        var g = await grids.ReadDetailAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
        scope.EnsureAccess(Permissions.GridView, g.GetStringOrNull("unit_path"), $"grid:{id}");

        var adminUnitId = g.GetInt("admin_unit_id");
        var breadcrumb = await units.GetBreadcrumbAsync(adminUnitId);
        var officers = (await gridOfficers.ReadByGridAsync(id))
            .OrderByDescending(o => o.GetBool("is_primary"))
            .Select(o => new GridOfficerDto(o.GetInt("user_id"), o.GetString("full_name"), o.GetStringOrNull("phone"), o.GetBool("is_primary")))
            .ToList();
        var status = (await grids.ReadStatusAsync(null, null, id)).FirstOrDefault();

        return new GridDetail(id, g.GetString("grid_code"), g.GetString("name"), adminUnitId, breadcrumb, g.GetBool("is_active"),
            GeoJson.ElementFromWkt(g.GetStringOrNull("boundary_wkt")), g.GetIntOrNull("assigned_officer_id"), officers,
            status?.GetStringOrNull("status_color") ?? "GREEN",
            status?.GetInt("open_incident_count") ?? 0,
            status?.GetInt("overdue_incident_count") ?? 0);
    }

    public async Task<GridSaveResult> CreateAsync(GridUpsertRequest request, CancellationToken ct)
    {
        var (unit, boundary) = await ValidateAsync(request, null);
        var code = request.GridCode.Trim();
        if (await grids.ReadByCodeAsync(code) is not null)
            throw DomainException.Conflict(ErrorCodes.Duplicate, $"Mã ô lưới {request.GridCode} đã tồn tại.");

        var id = (int)await grids.InsertAsync(new Row
        {
            ["grid_code"] = code,
            ["name"] = request.Name.Trim(),
            ["admin_unit_id"] = unit.GetInt("id"),
            ["boundary_wkt"] = GeoJson.ToWkt(boundary),
            ["assigned_officer_id"] = null,
            ["is_active"] = request.IsActive ?? true,
            ["created_at"] = Now(),
            ["updated_at"] = null,
        });
        return new GridSaveResult(id, await WarningsAsync(id, boundary, unit));
    }

    public async Task<GridSaveResult> UpdateAsync(int id, GridUpsertRequest request, CancellationToken ct)
    {
        var grid = await grids.ReadDetailAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
        scope.EnsureAccess(Permissions.GridManage, grid.GetStringOrNull("unit_path"), $"grid:{id}");
        var (unit, boundary) = await ValidateAsync(request, grid);
        var code = request.GridCode.Trim();
        var sameCode = await grids.ReadByCodeAsync(code);
        if (sameCode is not null && sameCode.GetInt("id") != id)
            throw DomainException.Conflict(ErrorCodes.Duplicate, $"Mã ô lưới {request.GridCode} đã tồn tại.");

        var row = grid.Pick(Cols.Grid);
        row["grid_code"] = code;
        row["name"] = request.Name.Trim();
        row["admin_unit_id"] = unit.GetInt("id");
        row["boundary_wkt"] = GeoJson.ToWkt(boundary);
        if (request.IsActive is { } active) row["is_active"] = active;
        row["updated_at"] = Now();
        await grids.UpdateAsync(row);
        return new GridSaveResult(id, await WarningsAsync(id, boundary, unit));
    }

    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var grid = await grids.ReadDetailAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
        scope.EnsureAccess(Permissions.GridManage, grid.GetStringOrNull("unit_path"), $"grid:{id}");
        var row = grid.Pick(Cols.Grid);
        row["is_active"] = false;
        row["updated_at"] = Now();
        await grids.UpdateAsync(row);
    }

    public async Task<ImportResult> ImportAsync(JsonElement featureCollection, bool dryRun, CancellationToken ct)
    {
        if (!featureCollection.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            throw new DomainException(ErrorCodes.GridGeometryInvalid, "File phải là GeoJSON FeatureCollection.", 422);

        var rows = new List<ImportResultRow>();
        int created = 0, updated = 0, failed = 0;
        foreach (var f in features.EnumerateArray())
        {
            var props = f.TryGetProperty("properties", out var p) ? p : default;
            var code = GetString(props, "grid_code") ?? GetString(props, "gridCode") ?? "";
            try
            {
                var unitCode = GetString(props, "unit_code") ?? GetString(props, "unitCode")
                               ?? throw new DomainException(ErrorCodes.ValidationFailed, "Thiếu unit_code.");
                var unitId = (await units.ReadByCodeAsync(unitCode))?.GetInt("id")
                             ?? throw new DomainException(ErrorCodes.ValidationFailed, $"Không có đơn vị {unitCode}.");
                var request = new GridUpsertRequest(code, GetString(props, "name") ?? code, unitId,
                    f.TryGetProperty("geometry", out var geom) ? geom : null, null, true);
                if (string.IsNullOrWhiteSpace(code)) throw new DomainException(ErrorCodes.ValidationFailed, "Thiếu grid_code.");

                var existing = (await grids.ReadByCodeAsync(code))?.GetInt("id");
                if (dryRun)
                {
                    var (unit, boundary) = await ValidateAsync(request, null);
                    rows.Add(new ImportResultRow(code, existing is null ? "CREATE" : "UPDATE", null, await WarningsAsync(existing ?? 0, boundary, unit)));
                }
                else
                {
                    var res = existing is { } eid ? await UpdateAsync(eid, request, ct) : await CreateAsync(request, ct);
                    rows.Add(new ImportResultRow(code, existing is null ? "CREATE" : "UPDATE", null, res.Warnings));
                }
                if (existing is null) created++; else updated++;
            }
            catch (DomainException ex)
            {
                failed++;
                rows.Add(new ImportResultRow(code, "ERROR", ex.Message, []));
            }
        }
        return new ImportResult(dryRun, created, updated, failed, rows);
    }

    public async Task AssignOfficersAsync(int id, AssignOfficersRequest request, CancellationToken ct)
    {
        var grid = await grids.ReadDetailAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy ô lưới.");
        var unitPath = grid.GetStringOrNull("unit_path");
        scope.EnsureAccess(Permissions.GridAssignOfficer, unitPath, $"grid:{id}");

        var userIds = request.UserIds.Distinct().ToList();
        if (request.PrimaryUserId is { } pr && !userIds.Contains(pr))
            throw new DomainException(ErrorCodes.ValidationFailed, "Cán bộ chính phải nằm trong danh sách cán bộ.");

        var eligible = (await users.FindOfficersForUnitAsync(unitPath))
            .Select(o => o.GetInt("user_id"))
            .ToHashSet();
        foreach (var uid in userIds)
        {
            if (!eligible.Contains(uid))
                throw DomainException.Unprocessable(ErrorCodes.OutOfScope, $"Người dùng {uid} không có vai trò cán bộ ô lưới phụ trách địa bàn này.");
        }

        var now = Now();
        // Giữ nguyên thời điểm phân công của cán bộ đã có trong ô lưới
        var assignedAt = (await gridOfficers.ReadAllAsync())
            .Where(o => o.GetInt("grid_id") == id)
            .ToDictionary(o => o.GetInt("user_id"), o => o.GetDateTime("assigned_at"));

        await using var tx = await ProcTransaction.BeginAsync();
        await gridOfficers.DeleteByGridAsync(id);
        foreach (var uid in userIds)
        {
            var at = assignedAt.TryGetValue(uid, out var existingAt) ? existingAt : now;
            await gridOfficers.InsertAsync(id, uid, request.PrimaryUserId == uid, at);
        }

        var row = grid.Pick(Cols.Grid);
        row["assigned_officer_id"] = request.PrimaryUserId;
        row["updated_at"] = now;
        await grids.UpdateAsync(row);
        await tx.CommitAsync();
    }

    /// <summary>Kiem tra du lieu va quyen; boundary lay tu request, hoac giu ranh gioi cu khi sua ma khong gui hinh.</summary>
    private async Task<(Row Unit, Geometry Boundary)> ValidateAsync(GridUpsertRequest request, Row? existing)
    {
        if (string.IsNullOrWhiteSpace(request.GridCode) || request.GridCode.Length > 50)
            throw new DomainException(ErrorCodes.ValidationFailed, "Mã ô lưới bắt buộc, tối đa 50 ký tự.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 150)
            throw new DomainException(ErrorCodes.ValidationFailed, "Tên ô lưới bắt buộc, tối đa 150 ký tự.");

        var unit = await units.ReadByIdAsync(request.AdminUnitId);
        if (unit is null || !unit.GetBool("is_active"))
            throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị hành chính.");
        scope.EnsureAccess(Permissions.GridManage, unit.GetStringOrNull("unit_path"), $"admin_unit:{unit.GetInt("id")}");

        Geometry boundary;
        if (request.Boundary is null && request.Wkt is null && existing?.GetStringOrNull("boundary_wkt") is { } oldWkt)
            boundary = GeoJson.FromWkt(oldWkt)!;
        else
            boundary = GeoJson.ParseArea(request.Boundary, request.Wkt);
        return (unit, boundary);
    }

    /// <summary>
    /// Canh bao (khong chan): chong lan o luoi khac, vuot ra ngoai don vi cha.
    /// Chong lan tinh bang NTS tren toa do do; dien tich xap xi m2 de so voi nguong 1 m2 cua ban cu.
    /// </summary>
    private async Task<List<string>> WarningsAsync(int gridId, Geometry boundary, Row unit)
    {
        var warnings = new List<string>();
        var others = await grids.ReadAllAsync();
        foreach (var g in others)
        {
            if (g.GetInt("id") == gridId || !g.GetBool("is_active")) continue;
            var other = GeoJson.FromWkt(g.GetStringOrNull("boundary_wkt"));
            if (other is null || !other.Intersects(boundary)) continue;

            var overlap = other.Intersection(boundary);
            if (overlap.IsEmpty) continue;
            var areaM2 = overlap.Area * 111_320d * 111_320d * Math.Cos(overlap.Centroid.Y * Math.PI / 180);
            // Chi canh bao khi phan giao dang ke (lon hon 1 m2), bo qua truong hop chung canh
            if (areaM2 > 1) warnings.Add($"Chồng lấn với ô lưới {g.GetString("grid_code")}.");
        }

        var unitBoundary = GeoJson.FromWkt(unit.GetStringOrNull("boundary_wkt"));
        if (unitBoundary is not null && !unitBoundary.Contains(boundary))
            warnings.Add($"Ô lưới vượt ra ngoài ranh giới {unit.GetString("name")}.");
        return warnings;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
