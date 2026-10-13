namespace GridSmart.Api.Interfaces;

/// <summary>Lop 2 cua phan quyen: kiem tra pham vi theo cay hanh chinh.</summary>
public interface IScopeService
{
    /// <summary>True khi nguoi dung co quyen tren don vi co unitPath (hoac co pham vi toan he thong).</summary>
    bool CanAccess(string permission, string? unitPath);

    /// <summary>Nem 403 OUT_OF_SCOPE va ghi log canh bao neu khong co quyen.</summary>
    void EnsureAccess(string permission, string? unitPath, string resource);

    /// <summary>Pham vi cua mot quyen: toan he thong, hoac danh sach duong dan.</summary>
    ScopeFilter GetFilter(string permission);

    /// <summary>
    /// Chuoi unit_path cach nhau dau phay truyen cho proc (@scope_paths).
    /// null = toan he thong; "" = khong co quyen (service tra rong, khong goi DB).
    /// </summary>
    string? ScopePaths(string permission);
}

public sealed record ScopeFilter(bool IsGlobal, IReadOnlyList<string> Paths)
{
    public bool IsEmpty => !IsGlobal && Paths.Count == 0;
}
