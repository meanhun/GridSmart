using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GridSmart.Api.Extensions;

/// <summary>Dang ky cac service nghiep vu.</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<object>, PasswordHasher<object>>();

        services.AddScoped<IScopeService, ScopeService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IncidentEvents>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPublicIncidentService, PublicIncidentService>();
        services.AddScoped<IIncidentPipeline, IncidentPipeline>();
        services.AddScoped<IDispatchService, DispatchService>();
        services.AddScoped<IIncidentWorkflowService, IncidentWorkflowService>();
        services.AddScoped<IIncidentQueryService, IncidentQueryService>();
        services.AddScoped<ISlaEngine, SlaEngine>();

        services.AddScoped<IGridService, GridService>();
        services.AddScoped<IAdminUnitService, AdminUnitService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<DashboardService>());
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
