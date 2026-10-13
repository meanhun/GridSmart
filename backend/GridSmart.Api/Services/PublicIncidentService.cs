using GridSmart.Api.Common;
using GridSmart.Api.Data;
using GridSmart.Api.DataAccess;
using GridSmart.Api.Dtos;
using GridSmart.Api.Exceptions;
using GridSmart.Api.Interfaces;
using GridSmart.Api.Models.Enums;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Services;

/// <summary>Nghiep vu cong khai cho cong dan: UC-01, UC-02, UC-03.</summary>
public sealed class PublicIncidentService(
    IncidentsDataAccess incidents,
    IncidentImagesDataAccess incidentImages,
    IncidentLogsDataAccess incidentLogs,
    IncidentRatingsDataAccess ratings,
    CategoriesDataAccess categories,
    UsersDataAccess users,
    AdminUnitsDataAccess adminUnits,
    IImageStore images,
    IImageClassifier classifier,
    ISpatialRepository spatial,
    IIncidentQueue queue,
    IOptions<RateLimitOptions> rateOptions,
    TimeProvider clock) : IPublicIncidentService
{
    private static readonly LogActionType[] PublicLogTypes =
        [LogActionType.Created, LogActionType.Dispatched, LogActionType.Accepted, LogActionType.Resolved, LogActionType.Cancelled];

    public async Task<CreateIncidentResult> CreateAsync(CreateIncidentCommand command, CancellationToken ct)
    {
        var now = Now();
        var phone = PhoneNumber.NormalizeOrThrow(command.ReporterPhone);
        if (command.Description is { Length: > 1000 })
            throw new DomainException(ErrorCodes.ValidationFailed, "Ghi chú tối đa 1000 ký tự.");

        // Gioi han theo so dien thoai: tu choi phan anh thu N+1 trong 10 phut (gioi han theo IP nam o middleware).
        if (phone is not null)
        {
            var recent = await incidents.CountRecentByPhoneAsync(phone, now.AddMinutes(-10));
            if (recent >= rateOptions.Value.ReportPerPhonePer10Min)
                throw new DomainException(ErrorCodes.RateLimited,
                    "Bạn đã gửi quá nhiều phản ánh từ số điện thoại này. Vui lòng thử lại sau ít phút.", 429);
        }

        Row? category = null;
        if (command.CategoryId is { } cid)
        {
            category = await categories.ReadByIdAsync(cid);
            if (category is null || !category.GetBool("is_active"))
                throw new DomainException(ErrorCodes.ValidationFailed, "Danh mục không hợp lệ.");
        }

        var stored = await images.SaveAsync(command.Image, command.ImageLength, ct);
        var lat = command.Latitude ?? stored.ExifLatitude;
        var lon = command.Longitude ?? stored.ExifLongitude;
        if (lat is null || lon is null)
            throw new DomainException(ErrorCodes.LocationRequired, "Cần vị trí của sự cố. Hãy bật định vị hoặc chọn điểm trên bản đồ.");
        GeoJson.EnsureCoordinates(lat.Value, lon.Value);

        var code = await NewTrackingCodeAsync(now.Year);
        var point = IncidentRows.PointWkt(lat.Value, lon.Value);
        var priority = category is not null ? category.GetByte("default_priority") : (byte)Priority.Medium;

        var row = new Row
        {
            ["tracking_code"] = code,
            ["reporter_user_id"] = command.ReporterUserId,
            ["reporter_phone"] = phone,
            ["description"] = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            ["category_id"] = command.CategoryId,
            ["priority"] = priority,
            ["location_point_wkt"] = point,
            ["address_text"] = string.IsNullOrWhiteSpace(command.AddressText) ? null : command.AddressText.Trim(),
            ["status"] = (byte)IncidentStatus.New,
            ["created_at"] = now,
        };

        long incidentId;
        await using (var tx = await ProcTransaction.BeginAsync())
        {
            incidentId = await incidents.InsertAsync(row);
            await incidentImages.InsertAsync(new Row
            {
                ["incident_id"] = incidentId,
                ["image_type"] = ImageTypes.Before,
                ["image_url"] = stored.Url,
                ["capture_point_wkt"] = point,
                ["uploaded_by_user_id"] = command.ReporterUserId,
                ["uploaded_at"] = now,
            });
            await IncidentLogWriter.AddAsync(incidentLogs, incidentId, LogActionType.Created,
                "Tiếp nhận phản ánh từ công dân.", now, command.ReporterUserId, null, IncidentStatus.New);
            await tx.CommitAsync();
        }

        queue.Enqueue(incidentId);
        return new CreateIncidentResult(code);
    }

    public async Task<IReadOnlyList<CategorySuggestion>> ClassifyPreviewAsync(Stream image, long length, string? description, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        var result = await classifier.ClassifyAsync(ms.ToArray(), description, ct);
        var rows = (await categories.ReadAllAsync()).Where(c => c.GetBool("is_active")).ToList();

        // Danh muc AI chon dung dau, cac danh muc con lai xep sau voi do tin cay thap
        return rows
            .Select(c => new CategorySuggestion(c.GetInt("id"), c.GetString("category_code"), c.GetString("category_name"),
                c.GetString("category_code") == result.CategoryCode ? result.Confidence : 0))
            .OrderByDescending(s => s.Confidence).ThenBy(s => s.CategoryId)
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        var rows = await categories.ReadAllAsync();
        return rows
            .Where(c => c.GetBool("is_active"))
            .OrderBy(c => c.GetInt("id"))
            .Select(c => new CategoryDto(c.GetInt("id"), c.GetString("category_code"), c.GetString("category_name"),
                c.GetInt("default_priority"), true))
            .ToList();
    }

    public async Task<LocateResult> LocateAsync(double latitude, double longitude, CancellationToken ct)
    {
        GeoJson.EnsureCoordinates(latitude, longitude);
        var grid = await spatial.FindGridByPointAsync(latitude, longitude, ct);
        var unit = await spatial.FindAdminUnitByPointAsync(latitude, longitude, ct);
        var unitId = grid?.AdminUnitId ?? unit?.Id;
        var crumbs = await IncidentRows.BreadcrumbAsync(adminUnits, unitId);
        return new LocateResult(
            crumbs.FirstOrDefault(c => c.LevelCode == AdminLevelCodes.Hamlet)?.Name,
            crumbs.FirstOrDefault(c => c.LevelCode == AdminLevelCodes.Commune)?.Name,
            crumbs.FirstOrDefault(c => c.LevelCode == AdminLevelCodes.Province)?.Name,
            grid is not null);
    }

    public async Task<bool> VerifyTrackingAsync(string code, string? phone, CancellationToken ct)
    {
        var p = PhoneNumber.Normalize(phone);
        var c = code.Trim().ToUpperInvariant();
        if (p is null) return false;
        var row = await incidents.ReadByTrackingCodeAsync(c);
        return row is not null && row.GetStringOrNull("reporter_phone") == p;
    }

    public async Task<TrackResult> TrackAsync(string code, string? phone, int? currentUserId, CancellationToken ct)
    {
        var incident = await FindForCitizenAsync(code, phone, currentUserId);
        var incidentId = IncidentRows.Id(incident);
        var incidentDetail = await incidents.ReadDetailAsync(incidentId) ?? incident;

        // Ticket con hien thi tien do cua ticket cha
        var display = incidentDetail;
        var parentId = incident.GetLongOrNull("parent_incident_id");
        if (parentId is { } pid)
            display = await incidents.ReadDetailAsync(pid) ?? throw new InvalidOperationException("Không tìm thấy phản ánh gốc.");
        var displayId = IncidentRows.Id(display);

        var ownImages = await incidentImages.ReadByIncidentAsync(incidentId);
        var beforeImages = ownImages
            .Where(x => x.GetString("image_type") == ImageTypes.Before)
            .Select(ImageUrls.ToDto).ToList();
        var afterImages = (await incidentImages.ReadByIncidentAsync(displayId))
            .Where(x => x.GetString("image_type") == ImageTypes.After)
            .Select(ImageUrls.ToDto).ToList();

        var logRows = new List<Row>(await incidentLogs.ReadByIncidentAsync(displayId));
        if (displayId != incidentId) logRows.AddRange(await incidentLogs.ReadByIncidentAsync(incidentId));
        var timeline = logRows
            .Select(l => new { Row = l, Type = IncidentRows.ParseLogType(l.GetStringOrNull("action_type")) })
            .Where(x => x.Type is not null && PublicLogTypes.Contains(x.Type.Value))
            .OrderBy(x => x.Row.GetDateTime("created_at")).ThenBy(x => x.Row.GetLong("id"))
            .Select(x => new TimelineItem(x.Row.GetString("action_type"), IncidentText.PublicMessage(x.Type!.Value)!, x.Row.GetDateTime("created_at")))
            .DistinctBy(ti => (ti.Code, ti.At))
            .ToList();
        var isMerged = parentId is not null;
        if (isMerged)
        {
            timeline.Insert(Math.Min(1, timeline.Count), new TimelineItem("MERGED",
                "Phản ánh của bạn được gộp với phản ánh khác cùng vị trí để xử lý một lần.",
                incident.GetDateTimeOrNull("updated_at") ?? incident.GetDateTime("created_at")));
        }

        var rating = (await ratings.ReadByIncidentAsync(incidentId)).FirstOrDefault();
        // Ly do cua can bo (huy, tra lai, giao lai) hien o moi trang thai, tru khi phan anh da bi xoa (khi do khong tra cuu duoc).
        var staffNotes = logRows
            .Select(l => new { Row = l, Type = IncidentRows.ParseLogType(l.GetStringOrNull("action_type")) })
            .Where(x => x.Type is LogActionType.Cancelled or LogActionType.Reassigned)
            .Select(x => new { x.Type, Note = x.Row.GetString("action_note").Trim(), At = x.Row.GetDateTime("created_at"), Id = x.Row.GetLong("id") })
            .Where(x => x.Note.Length > 0 && !x.Note.StartsWith("Hủy theo phản ánh gốc", StringComparison.Ordinal))
            .OrderBy(x => x.At).ThenBy(x => x.Id)
            .Select(x => new StaffNote(StaffActionLabel(x.Note), ExtractReason(x.Note), x.At))
            .ToList();
        var incidentStatus = IncidentRows.Status(incident);
        var status = incidentStatus == IncidentStatus.Cancelled ? incidentStatus : IncidentRows.Status(display);
        var point = IncidentRows.ParsePoint(incident.GetStringOrNull("location_point_wkt"));

        return new TrackResult(
            incident.GetString("tracking_code"), IncidentText.StatusCode(status), IncidentText.PublicStatusText(status),
            display.GetStringOrNull("category_name") ?? incidentDetail.GetStringOrNull("category_name"),
            incident.GetStringOrNull("address_text"),
            point?.Y ?? 0, point?.X ?? 0,
            incident.GetDateTime("created_at"),
            display.GetDateTimeOrNull("resolved_at") ?? incident.GetDateTimeOrNull("resolved_at"),
            isMerged, isMerged ? display.GetStringOrNull("tracking_code") : null,
            beforeImages, afterImages,
            timeline,
            status == IncidentStatus.Resolved && rating is null,
            rating?.GetIntOrNull("rating"), rating?.GetStringOrNull("feedback"),
            staffNotes);
    }

    /// <summary>Lay phan ly do tu noi dung nhat ky: "Hủy: X", "Cán bộ trả lại: X", "Giao lại cho cán bộ Y. Lý do: X".</summary>
    private static string ExtractReason(string note)
    {
        const string marker = "Lý do: ";
        var at = note.IndexOf(marker, StringComparison.Ordinal);
        if (at >= 0) return note[(at + marker.Length)..].Trim();
        var colon = note.IndexOf(": ", StringComparison.Ordinal);
        return colon >= 0 ? note[(colon + 2)..].Trim() : note;
    }

    private static string StaffActionLabel(string note) =>
        note.StartsWith("Hủy", StringComparison.Ordinal) ? "Hủy sự cố"
        : note.StartsWith("Cán bộ trả lại", StringComparison.Ordinal) ? "Cán bộ trả lại"
        : "Giao lại";

    public async Task RateAsync(string code, RateRequest request, int? currentUserId, CancellationToken ct)
    {
        if (request.Rating is < 1 or > 5)
            throw new DomainException(ErrorCodes.ValidationFailed, "Đánh giá từ 1 đến 5 sao.");
        if (request.Feedback is { Length: > 500 })
            throw new DomainException(ErrorCodes.ValidationFailed, "Góp ý tối đa 500 ký tự.");

        var incident = await FindForCitizenAsync(code, request.Phone, currentUserId);
        var incidentId = IncidentRows.Id(incident);
        if (IncidentRows.Status(incident) != IncidentStatus.Resolved)
            throw DomainException.Conflict(ErrorCodes.NotResolved, "Chỉ đánh giá được khi sự cố đã xử lý xong.");
        if ((await ratings.ReadByIncidentAsync(incidentId)).Count > 0)
            throw DomainException.Conflict(ErrorCodes.AlreadyRated, "Bạn đã đánh giá phản ánh này.");

        await ratings.InsertAsync(new Row
        {
            ["incident_id"] = incidentId,
            ["rating"] = request.Rating,
            ["feedback"] = string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim(),
            ["created_at"] = Now(),
        });
    }

    /// <summary>
    /// Phan anh cua chinh nguoi dung (theo user_id hoac so dien thoai tai khoan), moi nhat truoc.
    /// </summary>
    public async Task<IReadOnlyList<MyIncidentItem>> GetMyIncidentsAsync(int userId, CancellationToken ct)
    {
        var user = await users.ReadByIdAsync(userId);
        var rows = await incidents.ListByReporterAsync(userId, user?.GetStringOrNull("phone"));
        return rows.Select(r => new MyIncidentItem(
            r.GetString("tracking_code"),
            IncidentText.StatusCode((IncidentStatus)r.GetByte("status")),
            r.GetStringOrNull("category_name"),
            r.GetStringOrNull("address_text"),
            r.GetDateTime("created_at"),
            r.GetDateTimeOrNull("resolved_at"))).ToList();
    }

    /// <summary>Sai ma hoac sai so dien thoai deu tra 404 giong nhau de chong do ma.</summary>
    private async Task<Row> FindForCitizenAsync(string code, string? phone, int? currentUserId)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        var p = PhoneNumber.Normalize(phone);
        var incident = await incidents.ReadByTrackingCodeAsync(c);
        var owns = incident is not null &&
                   ((p is not null && incident.GetStringOrNull("reporter_phone") == p)
                    || (currentUserId is not null && incident.GetIntOrNull("reporter_user_id") == currentUserId));
        if (incident is null || !owns)
            throw DomainException.NotFound(ErrorCodes.IncidentNotFound, "Không tìm thấy phản ánh với mã và số điện thoại đã nhập.");
        return incident;
    }

    private async Task<string> NewTrackingCodeAsync(int year)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = TrackingCode.Generate(year);
            if (await incidents.ReadByTrackingCodeAsync(code) is null) return code;
        }
        throw new InvalidOperationException("Không sinh được mã tra cứu.");
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
