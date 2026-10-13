namespace GridSmart.Api.Constants;

/// <summary>Ma quyen, khop 25 dong cua tbl_permissions.</summary>
public static class Permissions
{
    public const string IncidentCreate = "incident.create";
    public const string IncidentViewOwn = "incident.view_own";
    public const string IncidentRate = "incident.rate";
    public const string IncidentView = "incident.view";
    public const string IncidentAccept = "incident.accept";
    public const string IncidentResolve = "incident.resolve";
    public const string IncidentClassify = "incident.classify";
    public const string IncidentDispatch = "incident.dispatch";
    public const string IncidentReassign = "incident.reassign";
    public const string IncidentCancel = "incident.cancel";
    /// <summary>Xoa han phan anh (khong can ly do). Chi cap cho quan tri he thong.</summary>
    public const string IncidentDelete = "incident.delete";
    public const string IncidentReceiveEscalation = "incident.receive_escalation";
    public const string GridView = "grid.view";
    public const string GridManage = "grid.manage";
    public const string GridAssignOfficer = "grid.assign_officer";
    public const string AdminUnitView = "admin_unit.view";
    public const string AdminUnitManage = "admin_unit.manage";
    public const string DashboardView = "dashboard.view";
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string KpiView = "kpi.view";
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string CategoryManage = "category.manage";
    public const string SlaManage = "sla.manage";

    /// <summary>Tat ca ma quyen, dung de doi chieu voi CSDL.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        IncidentCreate, IncidentViewOwn, IncidentRate, IncidentView, IncidentAccept, IncidentResolve,
        IncidentClassify, IncidentDispatch, IncidentReassign, IncidentCancel, IncidentReceiveEscalation,
        GridView, GridManage, GridAssignOfficer, AdminUnitView, AdminUnitManage,
        DashboardView, ReportView, ReportExport, KpiView,
        UserView, UserManage, RoleManage, CategoryManage, SlaManage,
    ];
}

/// <summary>Ma vai tro he thong.</summary>
public static class RoleCodes
{
    public const string Citizen = "CITIZEN";
    public const string GridOfficer = "GRID_OFFICER";
    public const string Dispatcher = "DISPATCHER";
    public const string Leader = "LEADER";
    public const string SystemAdmin = "SYSTEM_ADMIN";
}

/// <summary>Ma cap hanh chinh.</summary>
public static class AdminLevelCodes
{
    public const string Province = "PROVINCE";
    public const string Commune = "COMMUNE";
    public const string Hamlet = "HAMLET";
}
