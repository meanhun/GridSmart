using GridSmart.Api.BackgroundJobs;
using GridSmart.Api.DataAccess;
using GridSmart.Api.DataAccess.Repositories;
using GridSmart.Api.Services.Ai;
using GridSmart.Api.Services.Export;
using GridSmart.Api.Services.Storage;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Extensions;

/// <summary>Dang ky lop DataAccess (moi bang mot lop, goi stored procedure), luu anh, AI, JWT, job nen.</summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool enableDevSeeder, bool enableBackgroundJobs = true)
    {
        // Chuoi ket noi cho ProcExecutor (ConnectionStrings:Default)
        ConnectionHelper.SetConfiguration(config);

        // DataAccess: mot lop cho moi bang, khong trang thai
        services.AddScoped<AdminLevelsDataAccess>();
        services.AddScoped<AdminUnitTypesDataAccess>();
        services.AddScoped<AdminUnitsDataAccess>();
        services.AddScoped<RolesDataAccess>();
        services.AddScoped<PermissionsDataAccess>();
        services.AddScoped<RolePermissionsDataAccess>();
        services.AddScoped<UsersDataAccess>();
        services.AddScoped<UserRolesDataAccess>();
        services.AddScoped<RefreshTokensDataAccess>();
        services.AddScoped<CategoriesDataAccess>();
        services.AddScoped<SlaPoliciesDataAccess>();
        services.AddScoped<GridsDataAccess>();
        services.AddScoped<GridOfficersDataAccess>();
        services.AddScoped<IncidentsDataAccess>();
        services.AddScoped<IncidentImagesDataAccess>();
        services.AddScoped<IncidentLogsDataAccess>();
        services.AddScoped<IncidentRatingsDataAccess>();
        services.AddScoped<NotificationsDataAccess>();
        services.AddScoped<ReportsDataAccess>();

        services.AddScoped<ISpatialRepository, SpatialRepository>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IImageStore, LocalImageStore>();
        services.AddSingleton<IExcelExporter, ExcelExporter>();
        services.AddSingleton<IIncidentQueue, InMemoryIncidentQueue>();
        services.AddScoped<DemoDataService>();

        // AI: chon ban cai dat theo cau hinh Ai:Provider (mac dinh Mock)
        services.AddHttpClient<OpenAiImageClassifier>();
        services.AddHttpClient<HttpImageClassifier>();
        services.AddSingleton<MockImageClassifier>();
        services.AddScoped<IImageClassifier>(sp =>
            sp.GetRequiredService<IOptions<AiOptions>>().Value.Provider.ToLowerInvariant() switch
            {
                "openai" => sp.GetRequiredService<OpenAiImageClassifier>(),
                "http" => sp.GetRequiredService<HttpImageClassifier>(),
                _ => sp.GetRequiredService<MockImageClassifier>(),
            });

        if (enableBackgroundJobs)
        {
            services.AddHostedService<IncidentPipelineWorker>();
            services.AddHostedService<PipelineRecoveryWorker>();
            services.AddHostedService<SlaWorker>();
        }
        if (enableDevSeeder) services.AddHostedService<DevDataSeeder>();
        return services;
    }
}
