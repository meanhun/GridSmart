using System.Text.Json;
using GridSmart.Api.Data;
using Microsoft.Data.SqlClient;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace GridSmart.Api.Services;

/// <summary>Don vi hanh chinh (BE-24).</summary>
public sealed class AdminUnitService(
    AdminUnitsDataAccess units,
    AdminUnitTypesDataAccess unitTypes,
    AdminLevelsDataAccess levels,
    GridsDataAccess grids,
    UserRolesDataAccess userRoles,
    UsersDataAccess users,
    IncidentsDataAccess incidents,
    IScopeService scope,
    ISpatialRepository spatial,
    TimeProvider clock) : IAdminUnitService
{
    public async Task<IReadOnlyList<AdminUnitNode>> GetTreeAsync(int? rootId, int? depth, bool includeInactive, CancellationToken ct)
    {
        var scopePaths = scope.ScopePaths(Permissions.AdminUnitView);
        if (scopePaths == "") return [];

        string? rootPath = null;
        if (rootId is { } r)
        {
            rootPath = (await units.ReadByIdAsync(r))?.GetStringOrNull("unit_path")
                       ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        }

        var rows = (await units.ReadTreeAsync(includeInactive))
            .Where(u => ScopeService.MatchesScopePaths(scopePaths, u.GetStringOrNull("unit_path")))
            .Where(u => rootPath is null || (u.GetStringOrNull("unit_path") is { } up && up.StartsWith(rootPath, StringComparison.Ordinal)))
            .Select(x => new TreeRow(x.GetInt("id"), x.GetString("unit_code"), x.GetString("name"), x.GetString("type_code"),
                x.GetString("type_name"), x.GetString("level_code"), x.GetByte("level_order"), x.GetIntOrNull("parent_id"),
                x.GetStringOrNull("unit_path"), x.GetBool("is_active")))
            .ToList();

        var ids = rows.Select(x => x.Id).ToHashSet();
        var byParent = rows.ToLookup(x => x.ParentId);
        var maxDepth = depth ?? int.MaxValue;

        AdminUnitNode Build(TreeRow x, int level)
        {
            var children = level >= maxDepth
                ? new List<AdminUnitNode>()
                : byParent[x.Id].OrderBy(c => c.Name).Select(c => Build(c, level + 1)).ToList();
            return new AdminUnitNode(x.Id, x.UnitCode, x.Name, x.TypeCode, x.TypeName, x.LevelCode, x.LevelOrder, x.ParentId, x.UnitPath, x.IsActive, children);
        }

        // Goc la don vi co cha nam ngoai tap nhin thay
        return rows.Where(x => x.ParentId is null || !ids.Contains(x.ParentId.Value))
            .OrderBy(x => x.LevelOrder).ThenBy(x => x.Name)
            .Select(x => Build(x, 1)).ToList();
    }

    public async Task<AdminUnitDetail> GetAsync(int id, CancellationToken ct)
    {
        var u = await units.ReadDetailAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        scope.EnsureAccess(Permissions.AdminUnitView, u.GetStringOrNull("unit_path"), $"admin_unit:{id}");
        return new AdminUnitDetail(
            id,
            u.GetString("unit_code"),
            u.GetString("name"),
            u.GetInt("unit_type_id"),
            u.GetString("type_code"),
            u.GetString("level_code"),
            u.GetIntOrNull("parent_id"),
            u.GetStringOrNull("unit_path"),
            await units.GetBreadcrumbAsync(id),
            u.GetBool("is_active"),
            u.DateOnlyOrNull("effective_from"),
            u.DateOnlyOrNull("effective_to"),
            u.GetIntOrNull("merged_into_unit_id"),
            GeoJson.ElementFromWkt(u.GetStringOrNull("boundary_wkt")),
            u.GetInt("child_count"),
            u.GetInt("grid_count"));
    }

    public async Task<AdminUnitDetail?> LocateAsync(double latitude, double longitude, CancellationToken ct)
    {
        GeoJson.EnsureCoordinates(latitude, longitude);
        var unit = await spatial.FindAdminUnitByPointAsync(latitude, longitude, ct);
        return unit is null ? null : await GetAsync(unit.Id, ct);
    }

    public async Task<IReadOnlyList<AdminLevelDto>> GetLevelsAsync(CancellationToken ct)
    {
        var rows = await levels.ReadAllAsync();
        return rows.OrderBy(l => l.GetByte("level_order"))
            .Select(l => new AdminLevelDto(l.GetInt("id"), l.GetString("level_code"), l.GetString("level_name"), l.GetByte("level_order")))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminUnitTypeDto>> GetTypesAsync(CancellationToken ct)
    {
        var rows = await unitTypes.ReadAllWithLevelAsync();
        // Proc khong tra level_order: sap theo level_id (cap duoc danh so theo thu tu cap)
        return rows.OrderBy(t => t.GetInt("level_id")).ThenBy(t => t.GetInt("id"))
            .Select(t => new AdminUnitTypeDto(t.GetInt("id"), t.GetString("type_code"), t.GetString("type_name"), t.GetInt("level_id"), t.GetString("level_code")))
            .ToList();
    }

    public async Task<int> CreateAsync(AdminUnitUpsertRequest request, CancellationToken ct)
    {
        Validate(request);
        await EnsureParentScopeAsync(request.ParentId);
        var code = request.UnitCode.Trim();
        if (await units.ReadByCodeAsync(code) is not null)
            throw DomainException.Conflict(ErrorCodes.Duplicate, $"Mã đơn vị {request.UnitCode} đã tồn tại.");

        var now = Now();
        var newId = await MapTriggerErrorsAsync(() => units.InsertAsync(new Row
        {
            ["unit_code"] = code,
            ["name"] = request.Name.Trim(),
            ["unit_type_id"] = request.UnitTypeId,
            ["parent_id"] = request.ParentId,
            ["boundary_wkt"] = BoundaryWkt(request.Boundary, request.Wkt),
            ["is_active"] = request.IsActive ?? true,
            ["effective_from"] = request.EffectiveFrom?.ToDateTime(TimeOnly.MinValue),
            ["effective_to"] = null,
            ["merged_into_unit_id"] = null,
            ["created_at"] = now,
            ["updated_at"] = null,
        }));
        return (int)newId;
    }

    public async Task UpdateAsync(int id, AdminUnitUpsertRequest request, CancellationToken ct)
    {
        Validate(request);
        var unit = await units.ReadByIdAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị.");
        scope.EnsureAccess(Permissions.AdminUnitManage, unit.GetStringOrNull("unit_path"), $"admin_unit:{id}");
        if (request.ParentId != unit.GetIntOrNull("parent_id")) await EnsureParentScopeAsync(request.ParentId);
        var code = request.UnitCode.Trim();
        var sameCode = await units.ReadByCodeAsync(code);
        if (sameCode is not null && sameCode.GetInt("id") != id)
            throw DomainException.Conflict(ErrorCodes.Duplicate, $"Mã đơn vị {request.UnitCode} đã tồn tại.");

        var row = unit.Pick(Cols.AdminUnit);
        row["unit_code"] = code;
        row["name"] = request.Name.Trim();
        row["unit_type_id"] = request.UnitTypeId;
        row["parent_id"] = request.ParentId;
        if (request.Boundary is not null || request.Wkt is not null)
            row["boundary_wkt"] = BoundaryWkt(request.Boundary, request.Wkt);
        if (request.EffectiveFrom is { } ef) row["effective_from"] = ef.ToDateTime(TimeOnly.MinValue);
        if (request.IsActive is { } active) row["is_active"] = active;
        row["updated_at"] = Now();
        await MapTriggerErrorsAsync(() => units.UpdateAsync(row));
    }

    /// <summary>Sap nhap theo luong 9.2: chuyen cay con, o luoi, vai tro sang don vi dich; dong don vi nguon.</summary>
    public async Task<MergeResult> MergeAsync(MergeRequest request, CancellationToken ct)
    {
        if (request.SourceUnitIds.Count == 0)
            throw new DomainException(ErrorCodes.ValidationFailed, "Cần chọn ít nhất một đơn vị nguồn.");
        if ((request.TargetUnitId is null) == (request.NewUnit is null))
            throw new DomainException(ErrorCodes.ValidationFailed, "Chọn một đơn vị đích có sẵn hoặc khai báo đơn vị mới.");

        var distinctSourceIds = request.SourceUnitIds.Distinct().ToList();
        var sources = new List<Row>();
        foreach (var sid in distinctSourceIds)
        {
            var s = await units.ReadByIdAsync(sid);
            if (s is not null) sources.Add(s);
        }
        if (sources.Count != distinctSourceIds.Count)
            throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đủ đơn vị nguồn.");
        foreach (var s in sources) scope.EnsureAccess(Permissions.AdminUnitManage, s.GetStringOrNull("unit_path"), $"admin_unit:{s.GetInt("id")}");
        if (sources.Any(s => !s.GetBool("is_active")))
            throw DomainException.Conflict(ErrorCodes.Conflict, "Có đơn vị nguồn đã ngừng hoạt động.");

        var now = Now();
        var sourceIds = sources.Select(s => s.GetInt("id")).ToList();
        var sourceIdList = string.Join(",", sourceIds);
        var effectiveTo = request.EffectiveDate.ToDateTime(TimeOnly.MinValue);

        await using var tx = await ProcTransaction.BeginAsync();

        int targetId;
        if (request.TargetUnitId is { } tid)
        {
            var target = await units.ReadByIdAsync(tid) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị đích.");
            if (sourceIds.Contains(tid))
                throw new DomainException(ErrorCodes.ValidationFailed, "Đơn vị đích không được nằm trong danh sách nguồn.");
            scope.EnsureAccess(Permissions.AdminUnitManage, target.GetStringOrNull("unit_path"), $"admin_unit:{tid}");
            targetId = tid;
        }
        else
        {
            var spec = request.NewUnit!;
            var parentId = spec.ParentId ?? sources[0].GetIntOrNull("parent_id");
            await EnsureParentScopeAsync(parentId);
            if (await units.ReadByCodeAsync(spec.UnitCode.Trim()) is not null)
                throw DomainException.Conflict(ErrorCodes.Duplicate, $"Mã đơn vị {spec.UnitCode} đã tồn tại.");

            var boundaries = sources.Select(s => GeoJson.FromWkt(s.GetStringOrNull("boundary_wkt"))).OfType<Geometry>().ToList();
            var unionWkt = boundaries.Count == 0
                ? null
                : GeoJson.ToWkt(GeoJson.OrientForGeography(UnaryUnionOp.Union(boundaries)));

            targetId = (int)await MapTriggerErrorsAsync(() => units.InsertAsync(new Row
            {
                ["unit_code"] = spec.UnitCode.Trim(),
                ["name"] = spec.Name.Trim(),
                ["unit_type_id"] = spec.UnitTypeId,
                ["parent_id"] = parentId,
                ["boundary_wkt"] = unionWkt,
                ["is_active"] = true,
                ["effective_from"] = effectiveTo,
                ["effective_to"] = null,
                ["merged_into_unit_id"] = null,
                ["created_at"] = now,
                ["updated_at"] = null,
            }));
        }

        // 1. Chuyen don vi con (trigger tinh lai unit_path cho ca cay con)
        var children = await units.ReadChildrenOfAsync(sourceIdList);
        foreach (var c in children)
        {
            var row = c.Pick(Cols.AdminUnit);
            row["parent_id"] = targetId;
            row["updated_at"] = now;
            await MapTriggerErrorsAsync(() => units.UpdateAsync(row));
        }

        // 2. Chuyen o luoi va ca phan anh dang nam trong don vi nguon
        var gridRows = await grids.ReadByUnitsAsync(sourceIdList);
        foreach (var g in gridRows)
        {
            var row = g.Pick(Cols.Grid);
            row["admin_unit_id"] = targetId;
            row["updated_at"] = now;
            await grids.UpdateAsync(row);
        }
        foreach (var sid in sourceIds)
            await incidents.RepointAdminUnitAsync(sid, targetId);

        // 3. Chuyen vai tro; neu nguoi dung da co vai tro do o don vi dich thi vo hieu ban cu
        var roleRows = (await userRoles.ReadByUnitsAsync(sourceIdList)).Where(r => r.GetBool("is_active")).ToList();
        var existingAtTarget = await userRoles.ReadByUnitsAsync(targetId.ToString());
        var taken = existingAtTarget.Select(x => (UserId: x.GetInt("user_id"), RoleId: x.GetInt("role_id"))).ToHashSet();
        foreach (var r in roleRows)
        {
            var row = r.Pick(Cols.UserRole);
            if (taken.Add((r.GetInt("user_id"), r.GetInt("role_id")))) row["admin_unit_id"] = targetId;
            else row["is_active"] = false;
            await userRoles.UpdateAsync(row);
        }

        var affectedUserIds = roleRows.Select(r => r.GetInt("user_id")).Distinct().ToList();
        var affectedUsers = 0;
        foreach (var uid in affectedUserIds)
        {
            var user = await users.ReadByIdAsync(uid);
            if (user is null) continue;
            user["security_stamp"] = Guid.NewGuid();
            user["updated_at"] = now;
            await users.UpdateAsync(user.Pick(Cols.User));
            affectedUsers++;
        }

        // 4. Dong don vi nguon (doc lai: buoc 1 co the da doi parent_id cua chinh cac don vi nguon)
        foreach (var sid in sourceIds)
        {
            var source = await units.ReadByIdAsync(sid) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị nguồn.");
            var row = source.Pick(Cols.AdminUnit);
            row["is_active"] = false;
            row["effective_to"] = effectiveTo;
            row["merged_into_unit_id"] = targetId;
            row["updated_at"] = now;
            await MapTriggerErrorsAsync(() => units.UpdateAsync(row));
        }

        await tx.CommitAsync();
        return new MergeResult(targetId, children.Count, gridRows.Count, roleRows.Count, affectedUsers);
    }

    public async Task<ImportResult> ImportAsync(JsonElement featureCollection, bool dryRun, CancellationToken ct)
    {
        if (!featureCollection.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            throw new DomainException(ErrorCodes.ValidationFailed, "File phải là GeoJSON FeatureCollection.", 422);

        var typeRows = await unitTypes.ReadAllWithLevelAsync();
        var types = typeRows.ToDictionary(t => t.GetString("type_code"), t => t.GetInt("id"), StringComparer.OrdinalIgnoreCase);
        var pendingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ImportResultRow>();
        int created = 0, updated = 0, failed = 0;

        foreach (var f in features.EnumerateArray())
        {
            var props = f.TryGetProperty("properties", out var p) ? p : default;
            var code = Str(props, "unit_code") ?? "";
            try
            {
                if (string.IsNullOrWhiteSpace(code)) throw new DomainException(ErrorCodes.ValidationFailed, "Thiếu unit_code.");
                var typeCode = Str(props, "type_code") ?? throw new DomainException(ErrorCodes.ValidationFailed, "Thiếu type_code.");
                if (!types.TryGetValue(typeCode, out var typeId)) throw new DomainException(ErrorCodes.ValidationFailed, $"Loại đơn vị {typeCode} không tồn tại.");
                var parentCode = Str(props, "parent_code");
                int? parentId = null;
                if (parentCode is not null)
                {
                    parentId = (await units.ReadByCodeAsync(parentCode))?.GetInt("id");
                    if (parentId is null && !(dryRun && pendingCodes.Contains(parentCode)))
                        throw new DomainException(ErrorCodes.ValidationFailed, $"Không có đơn vị cha {parentCode}.");
                }
                var geometry = f.TryGetProperty("geometry", out var g) && g.ValueKind == JsonValueKind.Object ? g : (JsonElement?)null;
                var request = new AdminUnitUpsertRequest(code, Str(props, "name") ?? code, typeId, parentId, geometry, null, null, true);
                var existing = (await units.ReadByCodeAsync(code))?.GetInt("id");

                if (dryRun)
                {
                    Validate(request);
                    if (geometry is not null) GeoJson.ParseArea(geometry, null);
                    pendingCodes.Add(code);
                }
                else if (existing is { } eid) await UpdateAsync(eid, request, ct);
                else await CreateAsync(request, ct);

                rows.Add(new ImportResultRow(code, existing is null ? "CREATE" : "UPDATE", null, []));
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

    private async Task EnsureParentScopeAsync(int? parentId)
    {
        if (parentId is null)
        {
            scope.EnsureAccess(Permissions.AdminUnitManage, null, "admin_unit:root");
            return;
        }
        var parent = await units.ReadByIdAsync(parentId.Value)
                     ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy đơn vị cha.");
        scope.EnsureAccess(Permissions.AdminUnitManage, parent.GetStringOrNull("unit_path"), $"admin_unit:{parentId}");
    }

    /// <summary>Ranh gioi dang WKT (null neu khong co): GeoJSON/WKT -> Polygon hop le.</summary>
    private static string? BoundaryWkt(JsonElement? geoJson, string? wkt) =>
        geoJson is null && wkt is null ? null : GeoJson.ToWkt(GeoJson.ParseArea(geoJson, wkt));

    private static void Validate(AdminUnitUpsertRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.UnitCode) || r.UnitCode.Length > 30)
            throw new DomainException(ErrorCodes.ValidationFailed, "Mã đơn vị bắt buộc, tối đa 30 ký tự.");
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 150)
            throw new DomainException(ErrorCodes.ValidationFailed, "Tên đơn vị bắt buộc, tối đa 150 ký tự.");
        if (r.UnitTypeId is < 1 or > 255)
            throw new DomainException(ErrorCodes.ValidationFailed, "Loại đơn vị không hợp lệ.");
    }

    /// <summary>Trigger trg_tbl_admin_units_path bao loi bang RAISERROR (50000); doi thanh ma loi nghiep vu.</summary>
    private static async Task<T> MapTriggerErrorsAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex) when (ex.Number == 50000)
        {
            var msg = ex.Message;
            if (msg.Contains("cấp thấp hơn", StringComparison.OrdinalIgnoreCase))
                throw DomainException.Unprocessable(ErrorCodes.AdminUnitInvalidLevel, "Đơn vị con phải thuộc cấp thấp hơn đơn vị cha.");
            if (msg.Contains("cây con", StringComparison.OrdinalIgnoreCase))
                throw DomainException.Unprocessable(ErrorCodes.AdminUnitCycle, "Không thể chọn đơn vị cha nằm trong cây con của chính nó.");
            throw;
        }
    }

    private sealed record TreeRow(int Id, string UnitCode, string Name, string TypeCode, string TypeName, string LevelCode, byte LevelOrder,
        int? ParentId, string? UnitPath, bool IsActive);

    private static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
