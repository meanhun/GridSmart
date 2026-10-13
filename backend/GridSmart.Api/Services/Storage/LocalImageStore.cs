using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace GridSmart.Api.Services.Storage;

/// <summary>
/// Luu anh len o dia cuc bo (BE-09): kiem tra magic bytes, doc GPS tu EXIF,
/// xoay theo huong chup, ma hoa lai JPEG (xoa toan bo EXIF), sinh anh thu nho.
/// </summary>
public sealed class LocalImageStore(IOptions<StorageOptions> options, IHostEnvironment env, TimeProvider clock) : IImageStore
{
    private const int MaxDimension = 2048;

    public async Task<StoredImage> SaveAsync(Stream content, long length, CancellationToken ct = default)
    {
        var o = options.Value;
        var maxBytes = o.MaxImageMb * 1024L * 1024L;
        if (length > maxBytes)
            throw new DomainException(ErrorCodes.ImageTooLarge, $"Ảnh vượt quá {o.MaxImageMb} MB.", 413);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        if (ms.Length == 0 || ms.Length > maxBytes)
            throw new DomainException(ms.Length == 0 ? ErrorCodes.ImageInvalid : ErrorCodes.ImageTooLarge,
                ms.Length == 0 ? "Thiếu ảnh hiện trường." : $"Ảnh vượt quá {o.MaxImageMb} MB.", ms.Length == 0 ? 400 : 413);
        var bytes = ms.ToArray();

        if (!ImageSignature.IsSupported(bytes))
            throw new DomainException(ErrorCodes.ImageInvalid, "Chỉ nhận ảnh JPEG, PNG, WebP hoặc HEIC.");

        var (lat, lon) = ReadGps(bytes);

        using var bitmap = DecodeOriented(bytes)
                           ?? throw new DomainException(ErrorCodes.ImageInvalid, "Không đọc được ảnh. Hãy chụp lại hoặc chọn ảnh JPEG.");
        using var main = Resize(bitmap, MaxDimension);
        using var thumb = Resize(bitmap, o.ThumbnailSize);

        var now = clock.GetUtcNow().UtcDateTime;
        var relativeDir = $"{now:yyyy}/{now:MM}";
        var dir = Path.Combine(RootPath(), now.ToString("yyyy"), now.ToString("MM"));
        System.IO.Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N");

        await File.WriteAllBytesAsync(Path.Combine(dir, name + ".jpg"), Encode(main, 85), ct);
        await File.WriteAllBytesAsync(Path.Combine(dir, name + "_thumb.jpg"), Encode(thumb, 75), ct);

        var baseUrl = o.PublicBasePath.TrimEnd('/');
        return new StoredImage($"{baseUrl}/{relativeDir}/{name}.jpg", $"{baseUrl}/{relativeDir}/{name}_thumb.jpg", lat, lon);
    }

    public async Task<byte[]?> ReadAsync(string url, CancellationToken ct = default)
    {
        var baseUrl = options.Value.PublicBasePath.TrimEnd('/') + "/";
        if (!url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) return null;
        var root = Path.GetFullPath(RootPath());
        var full = Path.GetFullPath(Path.Combine(root, url[baseUrl.Length..].Replace('/', Path.DirectorySeparatorChar)));
        // Chong duyet thu muc ra ngoai thu muc luu anh
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
        return await File.ReadAllBytesAsync(full, ct);
    }

    private string RootPath()
    {
        var root = options.Value.LocalRoot;
        return Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
    }

    private static (double? Lat, double? Lon) ReadGps(byte[] bytes)
    {
        try
        {
            using var s = new MemoryStream(bytes);
            var dirs = ImageMetadataReader.ReadMetadata(s);
            var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
            if (gps?.GetGeoLocation() is not { } loc || (loc.Latitude == 0 && loc.Longitude == 0)) return (null, null);
            return (loc.Latitude, loc.Longitude);
        }
        catch
        {
            return (null, null);
        }
    }

    private static SKBitmap? DecodeOriented(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return null;
        var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return null;

        var (degrees, swap) = codec.EncodedOrigin switch
        {
            SKEncodedOrigin.BottomRight => (180, false),
            SKEncodedOrigin.RightTop => (90, true),
            SKEncodedOrigin.LeftBottom => (270, true),
            _ => (0, false),
        };
        if (degrees == 0) return bitmap;

        var rotated = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
            canvas.RotateDegrees(degrees);
            canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }
        bitmap.Dispose();
        return rotated;
    }

    private static SKBitmap Resize(SKBitmap source, int maxSize)
    {
        var scale = Math.Min(1.0, (double)maxSize / Math.Max(source.Width, source.Height));
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        return source.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
               ?? source.Copy();
    }

    private static byte[] Encode(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }
}

/// <summary>Nhan dien dinh dang anh bang magic bytes, khong tin phan mo rong.</summary>
public static class ImageSignature
{
    public static bool IsSupported(ReadOnlySpan<byte> b)
    {
        if (b.Length < 12) return false;
        if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return true;                                    // JPEG
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return true;                    // PNG
        if (b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return true;             // WebP
        if (b[4..8].SequenceEqual("ftyp"u8))                                                              // HEIC/HEIF
        {
            var brand = System.Text.Encoding.ASCII.GetString(b[8..12]);
            return brand is "heic" or "heix" or "hevc" or "heim" or "heis" or "mif1" or "msf1";
        }
        return false;
    }
}
