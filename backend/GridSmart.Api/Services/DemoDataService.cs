using GridSmart.Api.Common;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using NetTopologySuite.IO;
using SkiaSharp;

namespace GridSmart.Api.Services;

/// <summary>Du lieu va cong cu trinh dien (BE-31).</summary>
public sealed class DemoDataService(
    IncidentsDataAccess incidents,
    IncidentImagesDataAccess incidentImages,
    IncidentLogsDataAccess incidentLogs,
    IncidentRatingsDataAccess ratings,
    GridsDataAccess grids,
    CategoriesDataAccess categories,
    SlaPoliciesDataAccess slaPolicies,
    IImageStore images,
    ISlaEngine sla,
    TimeProvider clock,
    ILogger<DemoDataService> logger)
{
    private static readonly string[] Descriptions =
    [
        "Rác thải tràn ra lòng đường", "Đống xà bần bỏ trên vỉa hè", "Buôn bán lấn chiếm vỉa hè", "Quán cóc lấn chiếm lòng đường",
        "Ổ gà lớn giữa đường", "Nắp cống bị vỡ", "Đèn đường không sáng", "Xả nước thải ra kênh, mùi hôi", "Xả thải trộm ban đêm",
    ];

    /// <summary>Lui created_at va sla_deadline de kich hoat leo thang hoac qua han ngay, roi chay SLA mot lan.</summary>
    public async Task<SlaRunResult> FastForwardAsync(long incidentId, int minutes, CancellationToken ct)
    {
        if (minutes is < 1 or > 60 * 24 * 7) throw new DomainException(ErrorCodes.ValidationFailed, "Số phút từ 1 đến 10080.");
        var incident = await incidents.ReadByIdAsync(incidentId)
                       ?? throw DomainException.NotFound(ErrorCodes.IncidentNotFound, "Không tìm thấy sự cố.");
        var shift = TimeSpan.FromMinutes(minutes);
        incident["created_at"] = incident.GetDateTime("created_at") - shift;
        if (incident.GetDateTimeOrNull("sla_deadline") is { } d) incident["sla_deadline"] = d - shift;
        await incidents.UpdateAsync(incident);
        return await sla.RunOnceAsync(ct);
    }

    /// <summary>Sinh su co ngau nhien trong 30 ngay, phu du trang thai, de heatmap va bieu do co du lieu.</summary>
    public async Task<int> SeedAsync(int count, CancellationToken ct)
    {
        var rnd = new Random(20261010);
        var now = clock.GetUtcNow().UtcDateTime;
        var gridRows = (await grids.ReadAllAsync())
            .Where(g => g.GetBool("is_active") && g.GetIntOrNull("assigned_officer_id") is not null).ToList();
        var categoryRows = (await categories.ReadAllAsync()).Where(c => c.GetBool("is_active")).ToList();
        var policies = (await slaPolicies.ReadAllAsync()).ToDictionary(p => p.GetInt("priority"));
        if (gridRows.Count == 0 || categoryRows.Count == 0)
            throw new InvalidOperationException("Chưa có ô lưới hoặc danh mục để sinh dữ liệu.");

        var imageUrls = new Dictionary<int, string>();
        foreach (var c in categoryRows)
            imageUrls[c.GetInt("id")] = await PlaceholderImageAsync(c.GetString("category_name"), c.GetInt("id"), ct);

        var used = (await incidents.ReadAllAsync()).Select(i => i.GetString("tracking_code")).ToHashSet();

        var drafts = new List<Row>();
        for (var n = 0; n < count; n++)
        {
            var grid = gridRows[rnd.Next(gridRows.Count)];
            var category = categoryRows[rnd.Next(categoryRows.Count)];
            var categoryPriority = category.GetInt("default_priority");
            var policy = policies[categoryPriority];
            var slaHours = policy.GetInt("sla_hours");
            var createdAt = now.AddMinutes(-rnd.Next(30, 60 * 24 * 30));
            var dispatchedAt = createdAt.AddMinutes(rnd.Next(1, 4));
            var deadline = dispatchedAt.AddHours(slaHours);
            var point = RandomPointIn(grid.GetString("boundary_wkt"), rnd);

            var roll = rnd.NextDouble();
            var status = roll switch
            {
                < 0.55 => IncidentStatus.Resolved,
                < 0.70 => IncidentStatus.InProgress,
                < 0.85 => IncidentStatus.Assigned,
                < 0.95 => IncidentStatus.New,
                _ => IncidentStatus.Cancelled,
            };
            // Su co con mo phai con trong thoi han hop ly, tranh qua nhieu su co mo cu
            if (status is IncidentStatus.Assigned or IncidentStatus.InProgress or IncidentStatus.New && (now - createdAt).TotalHours > slaHours * 3)
                status = IncidentStatus.Resolved;

            var row = new Row
            {
                ["tracking_code"] = TrackingCode.Generate(createdAt.Year),
                ["reporter_user_id"] = null,
                ["reporter_phone"] = $"09{rnd.Next(10000000, 99999999)}",
                ["description"] = Descriptions[rnd.Next(Descriptions.Length)],
                ["category_id"] = category.GetInt("id"),
                ["ai_label"] = category.GetString("category_code").ToLowerInvariant(),
                ["ai_confidence"] = 0.9m,
                ["location_point_wkt"] = IncidentRows.PointWkt(point.Lat, point.Lon),
                ["address_text"] = grid.GetString("name"),
                ["grid_id"] = grid.GetInt("id"),
                ["admin_unit_id"] = grid.GetInt("admin_unit_id"),
                ["priority"] = (byte)categoryPriority,
                ["status"] = (byte)status,
                ["created_at"] = createdAt,
                ["processed_at"] = createdAt,
                ["is_manual_review"] = false,
                ["is_overdue"] = false,
                ["duplicate_count"] = 0,
                ["assigned_officer_id"] = null,
                ["sla_policy_id"] = null,
                ["sla_deadline"] = null,
                ["accepted_at"] = null,
                ["resolved_at"] = null,
                ["escalated_at"] = null,
                ["parent_incident_id"] = null,
                ["updated_at"] = null,
            };
            if (status == IncidentStatus.New)
            {
                row["is_manual_review"] = true;
                row["category_id"] = null;
                row["ai_confidence"] = 0.3m;
            }
            else
            {
                row["assigned_officer_id"] = grid.GetIntOrNull("assigned_officer_id");
                row["sla_policy_id"] = policy.GetInt("id");
                row["sla_deadline"] = deadline;
            }
            if (status is IncidentStatus.InProgress or IncidentStatus.Resolved)
                row["accepted_at"] = dispatchedAt.AddMinutes(rnd.Next(5, 90));
            if (status == IncidentStatus.Resolved)
            {
                var resolvedAt = dispatchedAt.AddHours(slaHours * (0.2 + rnd.NextDouble() * 1.0));
                if (resolvedAt > now) resolvedAt = now.AddMinutes(-5);
                row["resolved_at"] = resolvedAt;
                row["is_overdue"] = resolvedAt > deadline;
            }
            else if (status != IncidentStatus.Cancelled && deadline < now)
            {
                row["is_overdue"] = true;
                row["escalated_at"] = dispatchedAt.AddHours(slaHours * 0.8);
            }
            drafts.Add(row);
        }

        // Ma tra cuu khong trung
        foreach (var r in drafts)
        {
            while (!used.Add(r.GetString("tracking_code")))
                r["tracking_code"] = TrackingCode.Generate(r.GetDateTime("created_at").Year);
        }

        // Mot so phan anh trung gop vao su co dang mo (dat duplicate_count truoc khi ghi)
        var parentIdx = drafts
            .Select((r, idx) => (r, idx))
            .Where(x => IncidentRows.Status(x.r) is IncidentStatus.Assigned or IncidentStatus.InProgress)
            .Take(count / 20)
            .Select(x => x.idx)
            .ToList();
        foreach (var idx in parentIdx) drafts[idx]["duplicate_count"] = 1;

        await using (var tx = await ProcTransaction.BeginAsync())
        {
            var ids = new List<long>();
            foreach (var r in drafts) ids.Add(await incidents.InsertAsync(r));

            for (var k = 0; k < drafts.Count; k++)
            {
                var r = drafts[k];
                var id = ids[k];
                var status = IncidentRows.Status(r);
                var createdAt = r.GetDateTime("created_at");
                var catId = r.GetIntOrNull("category_id") ?? categoryRows[0].GetInt("id");
                var officerId = r.GetIntOrNull("assigned_officer_id");

                await incidentImages.InsertAsync(new Row
                {
                    ["incident_id"] = id, ["image_type"] = ImageTypes.Before, ["image_url"] = imageUrls[catId],
                    ["capture_point_wkt"] = r["location_point_wkt"], ["uploaded_at"] = createdAt,
                });
                await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Created, "Tiếp nhận phản ánh từ công dân.",
                    createdAt, null, null, IncidentStatus.New);
                await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.GridMatched, "Thuộc ô lưới (dữ liệu demo).", createdAt.AddSeconds(5));
                await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.AiClassified, "AI phân loại (dữ liệu demo).", createdAt.AddSeconds(8));
                if (status == IncidentStatus.New) continue;

                var deadline = r.GetDateTime("sla_deadline");
                var dispatchedAt = deadline.AddHours(-policies[r.GetInt("priority")].GetInt("sla_hours"));
                await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Dispatched, "Tự động giao cho cán bộ phụ trách.",
                    dispatchedAt, null, IncidentStatus.New, IncidentStatus.Assigned);
                if (r.GetDateTimeOrNull("accepted_at") is { } acc)
                    await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Accepted, "Cán bộ nhận việc.", acc,
                        officerId, IncidentStatus.Assigned, IncidentStatus.InProgress);
                if (r.GetDateTimeOrNull("escalated_at") is { } esc)
                    await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Escalated, "Leo thang (dữ liệu demo).", esc);
                if (r.GetBool("is_overdue") && status != IncidentStatus.Resolved)
                    await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Overdue, "Quá hạn (dữ liệu demo).", deadline);
                if (r.GetDateTimeOrNull("resolved_at") is { } res)
                {
                    await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Resolved, "Đã xử lý xong (dữ liệu demo).", res,
                        officerId, IncidentStatus.InProgress, IncidentStatus.Resolved);
                    await incidentImages.InsertAsync(new Row
                    {
                        ["incident_id"] = id, ["image_type"] = ImageTypes.After, ["image_url"] = imageUrls[catId],
                        ["capture_point_wkt"] = r["location_point_wkt"], ["uploaded_by_user_id"] = officerId, ["uploaded_at"] = res,
                    });
                    if (rnd.NextDouble() < 0.6)
                        await ratings.InsertAsync(new Row { ["incident_id"] = id, ["rating"] = (byte)rnd.Next(3, 6), ["created_at"] = res.AddHours(2) });
                }
                if (status == IncidentStatus.Cancelled)
                    await IncidentLogWriter.AddAsync(incidentLogs, id, LogActionType.Cancelled, "Hủy (dữ liệu demo).", createdAt.AddHours(1),
                        null, IncidentStatus.Assigned, IncidentStatus.Cancelled);
            }

            // Phan anh trung con, gan vao cha
            foreach (var idx in parentIdx)
            {
                var p = drafts[idx];
                var parentId = ids[idx];
                var pCreated = p.GetDateTime("created_at");
                await incidents.InsertAsync(new Row
                {
                    ["tracking_code"] = NextCode(used, pCreated.Year),
                    ["reporter_user_id"] = null,
                    ["reporter_phone"] = $"09{rnd.Next(10000000, 99999999)}",
                    ["description"] = p["description"],
                    ["category_id"] = p["category_id"],
                    ["location_point_wkt"] = p["location_point_wkt"],
                    ["address_text"] = null,
                    ["grid_id"] = p["grid_id"],
                    ["admin_unit_id"] = p["admin_unit_id"],
                    ["priority"] = p["priority"],
                    ["status"] = (byte)IncidentStatus.New,
                    ["parent_incident_id"] = parentId,
                    ["created_at"] = pCreated.AddMinutes(30),
                    ["processed_at"] = pCreated.AddMinutes(30),
                    ["is_manual_review"] = false,
                    ["is_overdue"] = false,
                    ["duplicate_count"] = 0,
                });
            }
            await tx.CommitAsync();
        }

        logger.LogInformation("Da sinh {Count} su co demo va {Children} phan anh trung", drafts.Count, parentIdx.Count);
        return drafts.Count + parentIdx.Count;
    }

    private static string NextCode(HashSet<string> used, int year)
    {
        string code;
        do code = TrackingCode.Generate(year); while (!used.Add(code));
        return code;
    }

    /// <summary>Diem ngau nhien trong ranh gioi (WKT). Tra ve (vi do, kinh do).</summary>
    private static (double Lat, double Lon) RandomPointIn(string boundaryWkt, Random rnd)
    {
        var boundary = new WKTReader(GeoJson.Factory).Read(boundaryWkt);
        var env = boundary.EnvelopeInternal;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var p = GeoJson.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(
                env.MinX + rnd.NextDouble() * env.Width, env.MinY + rnd.NextDouble() * env.Height));
            if (boundary.Contains(p)) return (p.Y, p.X);
        }
        return (boundary.Centroid.Y, boundary.Centroid.X);
    }

    /// <summary>Anh mau don gian (mot mau nen + ten danh muc) de giao dien co anh hien thi.</summary>
    private async Task<string> PlaceholderImageAsync(string label, int seed, CancellationToken ct)
    {
        var colors = new[] { SKColors.SeaGreen, SKColors.SteelBlue, SKColors.Peru, SKColors.SlateGray, SKColors.IndianRed };
        using var bitmap = new SKBitmap(640, 480);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(colors[seed % colors.Length]);
            using var font = new SKFont(SKTypeface.Default, 40);
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawText($"Demo: {label}", 40, 250, SKTextAlign.Left, font, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        using var ms = new MemoryStream(data.ToArray());
        var stored = await images.SaveAsync(ms, ms.Length, ct);
        return stored.Url;
    }
}
