using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using GridSmart.Api.Data;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;

namespace GridSmart.Api.Common;

public static class Paging
{
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize, int maxPageSize = 100) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 20, 1, maxPageSize));
}

/// <summary>Cac cot cua tung bang duoc phep dua vao proc Update (khong co created_at va cot tinh cua trigger).</summary>
public static class Cols
{
    public static readonly string[] User =
        ["id", "username", "password_hash", "full_name", "phone", "email", "is_active", "security_stamp",
         "failed_login_count", "locked_until", "last_login_at", "updated_at"];

    public static readonly string[] UserRole =
        ["id", "user_id", "role_id", "admin_unit_id", "assigned_by_user_id", "assigned_at", "expires_at", "is_active"];

    public static readonly string[] RefreshToken =
        ["id", "user_id", "token_hash", "expires_at", "created_by_ip", "revoked_at", "replaced_by_token_id", "security_stamp"];

    public static readonly string[] Role =
        ["id", "role_code", "role_name", "description", "is_system", "is_active"];

    public static readonly string[] Category =
        ["id", "category_code", "category_name", "default_priority", "is_active"];

    public static readonly string[] SlaPolicy =
        ["id", "priority", "sla_level", "sla_hours", "escalation_percent", "description"];

    public static readonly string[] AdminUnit =
        ["id", "unit_code", "name", "unit_type_id", "parent_id", "boundary_wkt", "is_active", "effective_from",
         "effective_to", "merged_into_unit_id", "updated_at"];

    public static readonly string[] Grid =
        ["id", "grid_code", "name", "admin_unit_id", "boundary_wkt", "assigned_officer_id", "is_active", "updated_at"];
}

public static class RowExtensions
{
    /// <summary>Tao dong moi chi gom cac cot can ghi (proc Update ghi de toan bo cot).</summary>
    public static Row Pick(this Row source, params string[] keys)
    {
        var row = new Row();
        foreach (var key in keys)
            if (source.TryGetValue(key, out var value)) row[key] = value;
        return row;
    }

    /// <summary>Ngay (DATE) doc tu dong DataAccess, co the la DateTime hoac DateOnly.</summary>
    public static DateOnly? DateOnlyOrNull(this Row row, string key) => row.GetValueOrDefault(key) switch
    {
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => null,
    };
}

/// <summary>Chuan hoa so dien thoai Viet Nam ve dang 0xxxxxxxxx.</summary>
public static partial class PhoneNumber
{
    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex VnPhone();

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11) digits = "0" + digits[2..];
        return digits;
    }

    public static bool IsValid(string? normalized) => normalized is not null && VnPhone().IsMatch(normalized);

    /// <summary>Chuan hoa va kiem tra; sai dinh dang thi nem loi 400.</summary>
    public static string? NormalizeOrThrow(string? input)
    {
        var n = Normalize(input);
        if (n is null) return null;
        if (!IsValid(n)) throw new DomainException(ErrorCodes.ValidationFailed, "Số điện thoại không hợp lệ (cần 10 chữ số, bắt đầu bằng 0).");
        return n;
    }
}

/// <summary>Sinh ma tra cuu DN-yyyy-XXXX (bo cac ky tu de nham 0, O, 1, I).</summary>
public static class TrackingCode
{
    public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string Generate(int year)
    {
        Span<char> chars = stackalloc char[4];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"DN-{year}-{new string(chars)}";
    }
}

/// <summary>Doc va ghi hinh hoc (GeoJSON, WKT), SRID 4326.</summary>
public static class GeoJson
{
    public static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new GeoJsonConverterFactory(Factory));
        return o;
    }

    public static Point Point(double latitude, double longitude) => Factory.CreatePoint(new Coordinate(longitude, latitude));

    public static JsonElement? ToElement(Geometry? geometry) =>
        geometry is null ? null : JsonSerializer.SerializeToElement(geometry, Options);

    /// <summary>WKT (SRID 4326, thu tu lon lat) doc tu SQL thanh geometry; null hoac rong thi tra null.</summary>
    public static Geometry? FromWkt(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt)) return null;
        var g = new WKTReader(Factory).Read(wkt);
        g.SRID = 4326;
        return g;
    }

    /// <summary>Geometry thanh WKT de ghi vao cot geography qua STGeomFromText.</summary>
    public static string? ToWkt(Geometry? geometry) => geometry is null ? null : new WKTWriter().Write(geometry);

    /// <summary>JSON GeoJSON cua mot WKT (dung cho ranh gioi trong chi tiet don vi/o luoi).</summary>
    public static JsonElement? ElementFromWkt(string? wkt) => ToElement(FromWkt(wkt));

    /// <summary>Nhan GeoJSON (Geometry/Feature) hoac WKT, tra ve Polygon/MultiPolygon hop le, vong ngoai nguoc chieu kim dong ho.</summary>
    /// <summary>Khung tọa độ bao quanh tỉnh Đồng Nai (vĩ độ, kinh độ), có chừa lề.</summary>
    public const double ProvinceMinLat = 10.2, ProvinceMaxLat = 12.2, ProvinceMinLon = 105.9, ProvinceMaxLon = 108.2;

    public static Geometry ParseArea(JsonElement? geoJson, string? wkt)
    {
        Geometry? g;
        try
        {
            if (!string.IsNullOrWhiteSpace(wkt))
            {
                g = new WKTReader(NtsGeometryServices.Instance).Read(wkt);
            }
            else if (geoJson is { ValueKind: JsonValueKind.Object } el)
            {
                if (el.TryGetProperty("type", out var t) && t.GetString() == "Feature" && el.TryGetProperty("geometry", out var inner))
                    el = inner;
                g = el.Deserialize<Geometry>(Options);
            }
            else
            {
                throw new DomainException(ErrorCodes.GridGeometryInvalid, "Cần cung cấp ranh giới dạng GeoJSON hoặc WKT.", 422);
            }
        }
        catch (DomainException) { throw; }
        catch (Exception ex)
        {
            throw new DomainException(ErrorCodes.GridGeometryInvalid, $"Không đọc được ranh giới: {ex.Message}", 422);
        }

        if (g is not (Polygon or MultiPolygon))
            throw new DomainException(ErrorCodes.GridGeometryInvalid, "Ranh giới phải là Polygon hoặc MultiPolygon.", 422);
        if (!g.IsValid)
            throw new DomainException(ErrorCodes.GridGeometryInvalid, "Ranh giới không hợp lệ (tự cắt hoặc không khép kín).", 422);
        // Tọa độ phải nằm trong tỉnh Đồng Nai. Nhập nhầm kinh/vĩ độ sẽ đưa ô lưới ra ngoài bản đồ, người dùng không thấy được.
        if (g.Coordinates.Any(c => c.Y < ProvinceMinLat || c.Y > ProvinceMaxLat || c.X < ProvinceMinLon || c.X > ProvinceMaxLon))
            throw new DomainException(ErrorCodes.GridGeometryInvalid,
                "Tọa độ nằm ngoài tỉnh Đồng Nai. Lưu ý: GeoJSON ghi theo thứ tự [kinh độ, vĩ độ] (ví dụ [106.82, 10.94]); "
                + "WKT ghi POLYGON((kinh độ vĩ độ, ...)). Vĩ độ trong khoảng 10.2–12.2, kinh độ trong khoảng 105.9–108.2.", 422);

        g.SRID = 4326;
        return OrientForGeography(g);
    }

    /// <summary>Kieu geography cua SQL Server yeu cau vong ngoai nguoc chieu kim dong ho, vong trong cung chieu.</summary>
    public static Geometry OrientForGeography(Geometry g)
    {
        static Polygon Fix(Polygon p)
        {
            var shell = p.Shell.IsCCW ? p.Shell : (LinearRing)p.Shell.Reverse();
            var holes = p.Holes.Select(h => h.IsCCW ? (LinearRing)h.Reverse() : h).ToArray();
            return Factory.CreatePolygon(shell, holes);
        }

        Geometry result = g switch
        {
            Polygon p => Fix(p),
            MultiPolygon mp => Factory.CreateMultiPolygon(mp.Geometries.Cast<Polygon>().Select(Fix).ToArray()),
            _ => g,
        };
        result.SRID = 4326;
        return result;
    }

    /// <summary>Khoang cach (m) giua hai diem theo cong thuc haversine.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371008.8;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }

    public static void EnsureCoordinates(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || double.IsNaN(latitude) || double.IsNaN(longitude))
            throw new DomainException(ErrorCodes.ValidationFailed, "Tọa độ không hợp lệ.");
    }
}

/// <summary>Truy van tren cay don vi hanh chinh (qua vw_admin_unit_ancestors).</summary>
public static class UnitHierarchy
{
    /// <summary>Id cua don vi va moi to tien (dung de phat su kien realtime).</summary>
    public static async Task<List<int>> GetAncestorIdsAsync(this AdminUnitsDataAccess units, int? unitId)
    {
        if (unitId is null) return [];
        var rows = await units.ReadAncestorsAsync(unitId.Value);
        return rows.Select(r => r.GetInt("ancestor_id")).ToList();
    }

    /// <summary>Duong dan don vi dang "Tinh > Phuong > Khu pho" (gom ca don vi do).</summary>
    public static async Task<List<UnitRef>> GetBreadcrumbAsync(this AdminUnitsDataAccess units, int? unitId)
    {
        if (unitId is null) return [];
        var rows = await units.ReadAncestorsAsync(unitId.Value);
        return rows
            .OrderBy(r => r.GetByte("ancestor_level_order"))
            .Select(r => new UnitRef(r.GetInt("ancestor_id"), r.GetString("ancestor_code"), r.GetString("ancestor_name"), r.GetString("ancestor_level_code")))
            .ToList();
    }

    /// <summary>Lay id don vi tu duong dan '/1/2/5/' -> 5.</summary>
    public static int? UnitIdFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Trim('/').Split('/');
        return int.TryParse(parts[^1], out var id) ? id : null;
    }
}
