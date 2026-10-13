using System.Globalization;

namespace GridSmart.Api.Data;

/// <summary>
/// Mot dong du lieu tu SQL: ten cot = key, DBNull da chuyen thanh null.
/// Getter khong-OrNull nem loi neu cot thieu hoac la NULL; getter OrNull tra null cho ca cot thieu.
/// </summary>
public class Row : Dictionary<string, object?>
{
    public int GetInt(string key) => Convert.ToInt32(Required(key), CultureInfo.InvariantCulture);

    public int? GetIntOrNull(string key) => Optional(key) is { } v ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;

    public long GetLong(string key) => Convert.ToInt64(Required(key), CultureInfo.InvariantCulture);

    public long? GetLongOrNull(string key) => Optional(key) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : null;

    public byte GetByte(string key) => Convert.ToByte(Required(key), CultureInfo.InvariantCulture);

    public bool GetBool(string key) => Convert.ToBoolean(Required(key), CultureInfo.InvariantCulture);

    public string GetString(string key) => Convert.ToString(Required(key), CultureInfo.InvariantCulture)!;

    public string? GetStringOrNull(string key) => Optional(key) is { } v ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    public DateTime GetDateTime(string key) => Convert.ToDateTime(Required(key), CultureInfo.InvariantCulture);

    public DateTime? GetDateTimeOrNull(string key) => Optional(key) is { } v ? Convert.ToDateTime(v, CultureInfo.InvariantCulture) : null;

    public decimal? GetDecimalOrNull(string key) => Optional(key) is { } v ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : null;

    public double? GetDoubleOrNull(string key) => Optional(key) is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;

    public Guid GetGuid(string key)
    {
        var v = Required(key);
        return v is Guid g ? g : Guid.Parse(Convert.ToString(v, CultureInfo.InvariantCulture)!);
    }

    private object Required(string key)
    {
        if (!TryGetValue(key, out var v))
            throw new KeyNotFoundException($"Row không có cột '{key}'.");
        return v ?? throw new InvalidOperationException($"Cột '{key}' là NULL.");
    }

    private object? Optional(string key) => TryGetValue(key, out var v) ? v : null;
}
