namespace GridSmart.Api.Common;

/// <summary>Chuyen enum sang dang UPPER_SNAKE cho API (khop gia tri trong CSDL).</summary>
public static class EnumText
{
    public static string UpperSnake<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
            sb.Append(char.ToUpperInvariant(name[i]));
        }
        return sb.ToString();
    }
}
