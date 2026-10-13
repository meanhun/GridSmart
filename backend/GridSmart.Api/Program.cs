using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using GridSmart.Api.Extensions;
using GridSmart.Api.Hubs;
using GridSmart.Api.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

// So thap phan trong form (vi do, kinh do) luon doc theo dinh dang "10.945", khong phu thuoc ngon ngu may chu
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;
var isDemoLike = env.IsDevelopment() || env.IsEnvironment("Demo") || env.IsEnvironment("Testing");

// ---------- Logging (BE-02) ----------
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

// ---------- Options co validate khi khoi dong (BE-02) ----------
void Bind<T>(string section) where T : class =>
    builder.Services.AddOptions<T>().Bind(config.GetSection(section)).ValidateDataAnnotations().ValidateOnStart();
Bind<JwtOptions>(JwtOptions.Section);
Bind<AuthOptions>(AuthOptions.Section);
Bind<CorsOptions>(CorsOptions.Section);
Bind<StorageOptions>(StorageOptions.Section);
Bind<AiOptions>(AiOptions.Section);
Bind<DedupOptions>(DedupOptions.Section);
Bind<ResolveOptions>(ResolveOptions.Section);
Bind<SlaOptions>(SlaOptions.Section);
Bind<PipelineOptions>(PipelineOptions.Section);
Bind<RateLimitOptions>(RateLimitOptions.Section);
Bind<DevSeedOptions>(DevSeedOptions.Section);

// ---------- Tang ung dung ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config, enableDevSeeder: isDemoLike,
    enableBackgroundJobs: !args.Contains("seed-demo") && config.GetValue("BackgroundJobs:Enabled", true));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

// ---------- Loi -> ProblemDetails (BE-02) ----------
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    if (!ctx.ProblemDetails.Extensions.ContainsKey("code"))
    {
        ctx.ProblemDetails.Extensions["code"] = ctx.ProblemDetails.Status switch
        {
            401 => "UNAUTHORIZED",
            403 => "FORBIDDEN",
            404 => ErrorCodes.NotFound,
            429 => ErrorCodes.RateLimited,
            _ => "ERROR",
        };
    }
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Controllers ----------
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o =>
{
    // Loi binding/validate tu dong cua [ApiController] cung tra ProblemDetails kem code
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var problem = ctx.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(ctx.HttpContext, ctx.ModelState, 400, "Dữ liệu gửi lên không hợp lệ.");
        problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

// ---------- Xac thuc JWT + phan quyen dong (BE-06, BE-07) ----------
var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey.PadRight(32))),
        NameClaimType = GridSmartClaims.Name,
        RoleClaimType = GridSmartClaims.Role,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    // SignalR gui token qua query access_token
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
            return Task.CompletedTask;
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

// ---------- Gioi han tan suat (BE-06, BE-10, BE-21) ----------
var rate = config.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    static RateLimitPartition<string> ByIp(HttpContext ctx, int limit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = window, QueueLimit = 0 });
    o.AddPolicy("login", ctx => ByIp(ctx, rate.LoginPerMin, TimeSpan.FromMinutes(1)));
    o.AddPolicy("report", ctx => ByIp(ctx, rate.ReportPer10Min, TimeSpan.FromMinutes(10)));
    o.AddPolicy("track", ctx => ByIp(ctx, rate.TrackPerMin, TimeSpan.FromMinutes(1)));
    o.AddPolicy("classify", ctx => ByIp(ctx, rate.ClassifyPreviewPerMin, TimeSpan.FromMinutes(1)));
    o.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        var problem = new ProblemDetails { Status = 429, Title = "Bạn thao tác quá nhanh. Vui lòng thử lại sau." };
        problem.Extensions["code"] = ErrorCodes.RateLimited;
        await ctx.HttpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
    };
});

// ---------- CORS cho frontend, cho phep cookie va SignalR (BE-02) ----------
var cors = config.GetSection(CorsOptions.Section).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ---------- OpenAPI (BE-03) ----------
builder.Services.AddOpenApi("v1", o =>
{
    o.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "Đồng Nai GridSmart API", Version = "v1", Description = "API quản trị đô thị dạng lưới và phản ánh hiện trường." };
        doc.Components ??= new OpenApiComponents();
        doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token lấy từ POST /api/auth/login",
        };
        doc.Security ??= [];
        doc.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] });
        return Task.CompletedTask;
    });
});

builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddSqlServer(config.GetConnectionString("Default")!, name: "sqlserver");

var app = builder.Build();

// ---------- Lenh dong: sinh du lieu demo (BE-31) ----------
if (args.Contains("seed-demo"))
{
    var countArg = args.SkipWhile(a => a != "seed-demo").Skip(1).FirstOrDefault();
    var count = int.TryParse(countArg, out var c) ? c : 200;
    await using var scope = app.Services.CreateAsyncScope();
    var created = await scope.ServiceProvider.GetRequiredService<DemoDataService>().SeedAsync(count, CancellationToken.None);
    Console.WriteLine($"Đã sinh {created} sự cố demo.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) => diag.Set("TraceId", http.TraceIdentifier));

// Anh hien truong: phuc vu tu thu muc luu tru tai duong dan cong khai
var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var uploadRoot = Path.IsPathRooted(storage.LocalRoot) ? storage.LocalRoot : Path.Combine(app.Environment.ContentRootPath, storage.LocalRoot);
Directory.CreateDirectory(uploadRoot);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploadRoot), RequestPath = storage.PublicBasePath });

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi("/openapi/{documentName}.json");
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
{
    app.MapScalarApiReference("/scalar", o => o.WithTitle("GridSmart API").AddPreferredSecuritySchemes("Bearer"));
    // Mở đường dẫn gốc (ví dụ khi chạy từ Visual Studio) thì chuyển tới tài liệu API thay vì 404.
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) => await ctx.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
    }),
});

app.MapControllers();
app.MapHub<EventsHub>("/hubs/events");

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
