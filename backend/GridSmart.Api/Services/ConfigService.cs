using GridSmart.Api.Data;

namespace GridSmart.Api.Services;

/// <summary>Danh muc su co va chinh sach SLA (BE-26).</summary>
public sealed class ConfigService(CategoriesDataAccess categories, SlaPoliciesDataAccess slaPolicies) : IConfigService
{
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        var rows = await categories.ReadAllAsync();
        return rows
            .Where(c => includeInactive || c.GetBool("is_active"))
            .OrderBy(c => c.GetInt("id"))
            .Select(c => new CategoryDto(c.GetInt("id"), c.GetString("category_code"), c.GetString("category_name"),
                c.GetByte("default_priority"), c.GetBool("is_active")))
            .ToList();
    }

    public async Task<int> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken ct)
    {
        var (code, priority) = Validate(request);
        if (await CodeExistsAsync(code, exceptId: null))
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Mã danh mục đã tồn tại.");
        return (int)await categories.InsertAsync(new Row
        {
            ["category_code"] = code,
            ["category_name"] = request.Name.Trim(),
            ["default_priority"] = priority,
            ["is_active"] = request.IsActive ?? true,
        });
    }

    public async Task UpdateCategoryAsync(int id, CategoryUpsertRequest request, CancellationToken ct)
    {
        var (code, priority) = Validate(request);
        var c = await categories.ReadByIdAsync(id) ?? throw DomainException.NotFound(ErrorCodes.NotFound, "Không tìm thấy danh mục.");
        if (await CodeExistsAsync(code, exceptId: id))
            throw DomainException.Conflict(ErrorCodes.Duplicate, "Mã danh mục đã tồn tại.");
        c["category_code"] = code;
        c["category_name"] = request.Name.Trim();
        c["default_priority"] = priority;
        if (request.IsActive is { } a) c["is_active"] = a;
        await categories.UpdateAsync(c.Pick(Cols.Category));
    }

    public async Task<IReadOnlyList<SlaPolicyDto>> ListSlaPoliciesAsync(CancellationToken ct)
    {
        var rows = await slaPolicies.ReadAllAsync();
        return rows
            .OrderByDescending(p => p.GetByte("priority"))
            .Select(p => new SlaPolicyDto(p.GetInt("id"), p.GetByte("priority"), p.GetByte("sla_level"), p.GetInt("sla_hours"),
                p.GetByte("escalation_percent"), p.GetStringOrNull("description")))
            .ToList();
    }

    public async Task UpdateSlaPoliciesAsync(IReadOnlyList<SlaPolicyUpdate> updates, CancellationToken ct)
    {
        var policies = await slaPolicies.ReadAllAsync();
        await using var tx = await ProcTransaction.BeginAsync();
        foreach (var u in updates)
        {
            if (u.SlaHours is < 1 or > 720) throw new DomainException(ErrorCodes.ValidationFailed, "Số giờ SLA từ 1 đến 720.");
            if (u.EscalationPercent is < 1 or > 100) throw new DomainException(ErrorCodes.ValidationFailed, "Phần trăm leo thang từ 1 đến 100.");
            var p = policies.FirstOrDefault(x => x.GetByte("priority") == u.Priority)
                    ?? throw DomainException.NotFound(ErrorCodes.NotFound, $"Không có chính sách cho mức ưu tiên {u.Priority}.");
            p["sla_hours"] = (short)u.SlaHours;
            p["escalation_percent"] = (byte)u.EscalationPercent;
            if (u.Description is not null) p["description"] = u.Description;
            await slaPolicies.UpdateAsync(p.Pick(Cols.SlaPolicy));
        }
        await tx.CommitAsync();
    }

    /// <summary>Kiem tra trung ma danh muc (khong co proc doc theo ma, loc tren ReadAll).</summary>
    private async Task<bool> CodeExistsAsync(string code, int? exceptId)
    {
        var rows = await categories.ReadAllAsync();
        return rows.Any(c => c.GetString("category_code") == code && (exceptId is null || c.GetInt("id") != exceptId));
    }

    private static (string Code, byte Priority) Validate(CategoryUpsertRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Length > 30) throw new DomainException(ErrorCodes.ValidationFailed, "Mã danh mục bắt buộc, tối đa 30 ký tự.");
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100) throw new DomainException(ErrorCodes.ValidationFailed, "Tên danh mục bắt buộc, tối đa 100 ký tự.");
        if (r.DefaultPriority is < 1 or > 3) throw new DomainException(ErrorCodes.ValidationFailed, "Mức ưu tiên từ 1 đến 3.");
        return (r.Code.Trim().ToUpperInvariant(), (byte)r.DefaultPriority);
    }
}
