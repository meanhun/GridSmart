using GridSmart.Api.Common;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Services;

/// <summary>
/// Pipeline nen cho moi su co moi (BE-12): o luoi -> AI -> gom trung -> dieu phoi.
/// Moi buoc idempotent nho kiem tra nhat ky da co; xong thi dat processed_at.
/// Moi buoc doc lai dong su co tu DB truoc khi sua, de khong ghi de thay doi cua buoc truoc.
/// </summary>
public sealed class IncidentPipeline(
    IncidentsDataAccess incidents,
    IncidentLogsDataAccess incidentLogs,
    IncidentImagesDataAccess incidentImages,
    CategoriesDataAccess categories,
    SlaPoliciesDataAccess slaPolicies,
    ISpatialRepository spatial,
    IImageClassifier classifier,
    IImageStore images,
    IDispatchService dispatch,
    IncidentEvents events,
    IOptions<AiOptions> aiOptions,
    IOptions<DedupOptions> dedupOptions,
    TimeProvider clock,
    ILogger<IncidentPipeline> logger) : IIncidentPipeline
{
    public async Task ProcessAsync(long incidentId, CancellationToken ct)
    {
        var incident = await incidents.ReadByIdAsync(incidentId);
        if (incident is null || incident.GetDateTimeOrNull("processed_at") is not null) return;

        try
        {
            await MatchGridAsync(incidentId, ct);
            await ClassifyAsync(incidentId, ct);
            var merged = await DeduplicateAsync(incidentId, ct);
            var current = await incidents.ReadByIdAsync(incidentId);
            if (!merged && current is not null)
            {
                var status = IncidentRows.Status(current);
                if (!current.GetBool("is_manual_review") && current.GetIntOrNull("grid_id") is not null && status == IncidentStatus.New)
                {
                    await dispatch.DispatchAsync(current, null, null, ct);
                }
                else if (current.GetBool("is_manual_review") && status == IncidentStatus.New)
                {
                    await dispatch.NotifyManualReviewAsync(current, ManualReviewText(current), ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Pipeline loi o su co {IncidentId}", incidentId);
            var failed = await incidents.ReadByIdAsync(incidentId);
            if (failed is not null)
            {
                failed["is_manual_review"] = true;
                await using (var tx = await ProcTransaction.BeginAsync())
                {
                    await incidents.UpdateAsync(failed);
                    await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.PipelineError,
                        $"Lỗi xử lý tự động: {ex.Message}", Now());
                    await tx.CommitAsync();
                }
                await dispatch.NotifyManualReviewAsync(failed, "Lỗi xử lý tự động, cần điều phối thủ công.", ct);
            }
        }

        var final = await incidents.ReadByIdAsync(incidentId);
        if (final is not null)
        {
            final["processed_at"] = Now();
            await incidents.UpdateAsync(final);
        }
    }

    /// <summary>UC-04: tim o luoi, neu khong co thi tim don vi thap nhat (BE-13).</summary>
    private async Task MatchGridAsync(long incidentId, CancellationToken ct)
    {
        if (await IncidentLogQueries.HasAsync(incidentLogs, incidentId, LogActionType.GridMatched)) return;
        var incident = await incidents.ReadByIdAsync(incidentId);
        if (incident is null) return;

        var now = Now();
        var lat = IncidentRows.Lat(incident);
        var lon = IncidentRows.Lon(incident);
        var grid = await spatial.FindGridByPointAsync(lat, lon, ct);
        string note;
        if (grid is not null)
        {
            incident["grid_id"] = grid.GridId;
            incident["admin_unit_id"] = grid.AdminUnitId;
            note = $"Thuộc ô lưới {grid.GridCode} - {grid.Name}.";
        }
        else
        {
            incident["is_manual_review"] = true;
            var unit = await spatial.FindAdminUnitByPointAsync(lat, lon, ct);
            incident["admin_unit_id"] = unit?.Id;
            note = unit is not null
                ? $"Không thuộc ô lưới nào. Thuộc đơn vị {unit.Name}, chuyển điều phối viên."
                : "Nằm ngoài địa bàn quản lý, chuyển điều phối viên cấp tỉnh.";
        }
        incident["updated_at"] = now;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.GridMatched, note, now);
            await tx.CommitAsync();
        }

        await events.PublishAsync(incident, RealtimeEvents.IncidentCreated, new
        {
            id = incidentId,
            trackingCode = incident.GetString("tracking_code"),
            lat,
            lon,
            categoryId = incident.GetIntOrNull("category_id"),
            adminUnitId = incident.GetIntOrNull("admin_unit_id"),
            gridId = incident.GetIntOrNull("grid_id"),
            createdAt = incident.GetDateTime("created_at"),
        }, ct);
    }

    /// <summary>UC-05: AI phan loai (BE-14).</summary>
    private async Task ClassifyAsync(long incidentId, CancellationToken ct)
    {
        if (await IncidentLogQueries.HasAsync(incidentLogs, incidentId, LogActionType.AiClassified)) return;
        var incident = await incidents.ReadByIdAsync(incidentId);
        if (incident is null) return;

        var now = Now();
        var threshold = aiOptions.Value.ConfidenceThreshold;

        ClassificationResult result;
        try
        {
            var beforeRows = await incidentImages.ReadByIncidentAsync(incidentId);
            var before = beforeRows
                .Where(x => x.GetString("image_type") == ImageTypes.Before)
                .Select(x => x.GetString("image_url"))
                .FirstOrDefault();
            var bytes = before is null ? null : await images.ReadAsync(before, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(aiOptions.Value.TimeoutSeconds));
            result = await classifier.ClassifyAsync(bytes, incident.GetStringOrNull("description"), timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AI loi o su co {IncidentId}", incidentId);
            result = new ClassificationResult(null, null, 0);
        }

        incident["ai_label"] = result.Label;
        incident["ai_confidence"] = Math.Round((decimal)result.Confidence, 4);

        var categoryRows = await categories.ReadAllAsync();
        var category = result.CategoryCode is null ? null
            : categoryRows.FirstOrDefault(c => c.GetBool("is_active")
                && string.Equals(c.GetString("category_code"), result.CategoryCode, StringComparison.OrdinalIgnoreCase));

        string note;
        if (category is not null && result.Confidence >= threshold)
        {
            incident["category_id"] = category.GetInt("id");
            incident["priority"] = category.GetByte("default_priority");
            note = $"AI phân loại: {category.GetString("category_name")} (độ tin cậy {result.Confidence:0.00}).";
        }
        else
        {
            incident["is_manual_review"] = true;
            note = $"AI không chắc chắn (nhãn {result.Label ?? "không xác định"}, độ tin cậy {result.Confidence:0.00}). Chuyển điều phối viên.";
            if (incident.GetIntOrNull("category_id") is { } chosen)
            {
                var c = await categories.ReadByIdAsync(chosen);
                if (c is not null) incident["priority"] = c.GetByte("default_priority");
                note += " Giữ danh mục do công dân chọn.";
            }
        }
        incident["updated_at"] = now;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.AiClassified, note, now);
            await tx.CommitAsync();
        }
    }

    /// <summary>UC-05: gom trung trong ban kinh, cung danh muc, trong khung gio (BE-15).</summary>
    private async Task<bool> DeduplicateAsync(long incidentId, CancellationToken ct)
    {
        var incident = await incidents.ReadByIdAsync(incidentId);
        if (incident is null) return true;
        if (incident.GetLongOrNull("parent_incident_id") is not null) return true;
        if (incident.GetIntOrNull("category_id") is not { } categoryId) return false;

        var opt = dedupOptions.Value;
        var gridId = incident.GetIntOrNull("grid_id");
        var resource = gridId is { } g ? $"dedup:grid:{g}" : $"dedup:cat:{categoryId}";
        // Khoa ung dung giu den het ham: hai luong cung o luoi/danh muc khong gom chong nhau
        await using var appLock = await ProcExecutor.AcquireAppLockAsync(resource, TimeSpan.FromSeconds(30));

        var now = Now();
        // Proc chi tra phan ban ghi cu hon (id nho hon), de khong tao vong cha-con
        var parentId = await spatial.FindParentIncidentAsync(IncidentRows.Lat(incident), IncidentRows.Lon(incident),
            categoryId, opt.RadiusMeters, opt.WindowHours, beforeId: incidentId, ct: ct);
        if (parentId is null || parentId.Value >= incidentId) return false;

        var parent = await incidents.ReadByIdAsync(parentId.Value);
        if (parent is null) return false;

        var parentCode = parent.GetString("tracking_code");
        var duplicateCount = parent.GetInt("duplicate_count") + 1;
        parent["duplicate_count"] = duplicateCount;
        parent["updated_at"] = now;
        incident["parent_incident_id"] = parentId.Value;
        incident["is_manual_review"] = false;
        incident["updated_at"] = now;

        var parentPriority = (int)IncidentRows.PriorityOf(parent);
        var bumpPriority = opt.PriorityBumpThreshold > 0 && duplicateCount >= opt.PriorityBumpThreshold
                           && parentPriority < (int)Priority.Urgent;
        var bumpedPriority = parentPriority + 1;
        if (bumpPriority)
        {
            // Tu nang uu tien khi nhieu nguoi cung phan anh
            var policies = await slaPolicies.ReadAllAsync();
            var policy = policies.FirstOrDefault(p => p.GetInt("priority") == bumpedPriority)
                         ?? throw new InvalidOperationException($"Thiếu chính sách SLA cho mức ưu tiên {bumpedPriority}.");
            parent["priority"] = (byte)bumpedPriority;
            if (parent.GetDateTimeOrNull("sla_deadline") is { } deadline && parent.GetIntOrNull("sla_policy_id") is { } oldPolicyId)
            {
                var oldPolicy = policies.FirstOrDefault(p => p.GetInt("id") == oldPolicyId)
                                ?? throw new InvalidOperationException("Thiếu chính sách SLA hiện tại của sự cố.");
                var start = SlaRules.SlaStart(deadline, oldPolicy.GetInt("sla_hours"));
                var newDeadline = SlaRules.ComputeDeadline(start, policy.GetInt("sla_hours"));
                if (newDeadline < deadline) parent["sla_deadline"] = newDeadline;
                parent["sla_policy_id"] = policy.GetInt("id");
            }
        }

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            await incidents.UpdateAsync(incident);
            await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.Merged,
                $"Gộp vào phản ánh {parentCode} cùng vị trí.", now);
            await IncidentLogWriter.AddAsync(incidentLogs, parentId.Value, LogActionType.Merged,
                $"Nhận thêm phản ánh trùng {incident.GetString("tracking_code")}. Tổng {duplicateCount + 1} phản ánh.", now);
            await incidents.UpdateAsync(parent);
            if (bumpPriority)
            {
                await IncidentLogWriter.AddAsync(incidentLogs, parentId.Value, LogActionType.Classified,
                    $"Tự nâng mức ưu tiên do có {duplicateCount + 1} phản ánh trùng.", now);
            }
            await tx.CommitAsync();
        }

        await events.UpdatedAsync(parent, ct);
        return true;
    }

    private static string ManualReviewText(Row incident) =>
        incident.GetIntOrNull("grid_id") is null ? "Sự cố nằm ngoài các ô lưới, cần chọn đơn vị phụ trách."
        : incident.GetIntOrNull("category_id") is null ? "AI không nhận diện được loại sự cố, cần phân loại thủ công."
        : "Sự cố cần điều phối thủ công.";

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
