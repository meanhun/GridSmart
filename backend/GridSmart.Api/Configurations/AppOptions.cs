using System.ComponentModel.DataAnnotations;

namespace GridSmart.Api.Configurations;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    [Required] public string Issuer { get; set; } = "GridSmart";
    [Required] public string Audience { get; set; } = "GridSmart";
    [Required, MinLength(32)] public string SigningKey { get; set; } = "";
    [Range(1, 1440)] public int AccessTokenMinutes { get; set; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; set; } = 7;
}

public sealed class AuthOptions
{
    public const string Section = "Auth";
    [Range(1, 50)] public int MaxFailedLogins { get; set; } = 5;
    [Range(1, 1440)] public int LockoutMinutes { get; set; } = 15;
}

public sealed class CorsOptions
{
    public const string Section = "Cors";
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173"];
}

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string Provider { get; set; } = "Local";
    [Required] public string LocalRoot { get; set; } = "wwwroot/uploads";
    [Required] public string PublicBasePath { get; set; } = "/uploads";
    [Range(1, 50)] public int MaxImageMb { get; set; } = 10;
    [Range(100, 2000)] public int ThumbnailSize { get; set; } = 400;
}

public sealed class AiOptions
{
    public const string Section = "Ai";
    /// <summary>Mock (mac dinh), OpenAI, Http.</summary>
    public string Provider { get; set; } = "Mock";
    [Range(0, 1)] public double ConfidenceThreshold { get; set; } = 0.6;
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 15;
    /// <summary>Nhan AI -> category_code.</summary>
    public Dictionary<string, string> LabelMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>category_code -> danh sach tu khoa (ban Mock).</summary>
    public Dictionary<string, string[]> MockKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? OpenAiApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string? HttpEndpoint { get; set; }
}

public sealed class DedupOptions
{
    public const string Section = "Dedup";
    [Range(1, 1000)] public double RadiusMeters { get; set; } = 20;
    [Range(1, 720)] public int WindowHours { get; set; } = 24;
    [Range(0, 1000)] public int PriorityBumpThreshold { get; set; } = 5;
}

public sealed class ResolveOptions
{
    public const string Section = "Resolve";
    [Range(0, 5000)] public double AfterPhotoToleranceMeters { get; set; } = 50;
}

public sealed class SlaOptions
{
    public const string Section = "Sla";
    [Range(5, 3600)] public int ScanIntervalSeconds { get; set; } = 60;
}

public sealed class PipelineOptions
{
    public const string Section = "Pipeline";
    [Range(1, 1440)] public int RecoveryIntervalMinutes { get; set; } = 5;
    [Range(0, 60)] public int RecoveryMinAgeMinutes { get; set; } = 1;
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";
    [Range(1, 10000)] public int ReportPer10Min { get; set; } = 5;
    /// <summary>So phan anh toi da moi 10 phut cho mot so dien thoai (gioi han theo SDT, tach khoi gioi han theo IP).</summary>
    [Range(1, 10000)] public int ReportPerPhonePer10Min { get; set; } = 5;
    [Range(1, 10000)] public int TrackPerMin { get; set; } = 20;
    [Range(1, 10000)] public int LoginPerMin { get; set; } = 10;
    [Range(1, 10000)] public int ClassifyPreviewPerMin { get; set; } = 5;
}

public sealed class DevSeedOptions
{
    public const string Section = "DevSeed";
    public string? DefaultPassword { get; set; }
}
