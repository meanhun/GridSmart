namespace GridSmart.Api.Models.Enums;

/// <summary>Trang thai su co, khop cot tbl_incidents.status (tinyint).</summary>
public enum IncidentStatus : byte
{
    New = 0,
    Assigned = 1,
    InProgress = 2,
    Resolved = 3,
    Cancelled = 4,
}

/// <summary>Muc uu tien, khop tbl_incidents.priority va tbl_sla_policies.priority.</summary>
public enum Priority : byte
{
    Low = 1,
    Medium = 2,
    Urgent = 3,
}

/// <summary>Luu trong DB dang 'BEFORE' / 'AFTER'.</summary>
public enum ImageType
{
    Before,
    After,
}

/// <summary>Luu trong DB dang UPPER_SNAKE (CREATED, GRID_MATCHED...).</summary>
public enum LogActionType
{
    Created,
    GridMatched,
    AiClassified,
    Merged,
    Dispatched,
    Accepted,
    Escalated,
    Overdue,
    Resolved,
    Cancelled,
    Reassigned,
    Classified,
    PipelineError,
}

/// <summary>Luu trong DB dang UPPER_SNAKE.</summary>
public enum NotificationType
{
    NewAssignment,
    SlaWarning,
    Escalation,
    Overdue,
    ManualReview,
}

/// <summary>Ly do su co nam trong hang cho duyet cua dieu phoi vien.</summary>
public enum ReviewReason
{
    AiLowConfidence,
    NoGrid,
    NoOfficer,
    Returned,
    PipelineError,
}
