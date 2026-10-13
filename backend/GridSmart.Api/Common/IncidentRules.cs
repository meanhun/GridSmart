namespace GridSmart.Api.Common;

/// <summary>May trang thai su co (tai lieu luong, muc 10.1).</summary>
public static class IncidentStateMachine
{
    private static readonly Dictionary<IncidentStatus, IncidentStatus[]> Allowed = new()
    {
        [IncidentStatus.New] = [IncidentStatus.Assigned, IncidentStatus.Cancelled],
        [IncidentStatus.Assigned] = [IncidentStatus.InProgress, IncidentStatus.Cancelled, IncidentStatus.New, IncidentStatus.Assigned],
        [IncidentStatus.InProgress] = [IncidentStatus.Resolved, IncidentStatus.Assigned, IncidentStatus.New, IncidentStatus.Cancelled],
        [IncidentStatus.Resolved] = [],
        [IncidentStatus.Cancelled] = [],
    };

    public static bool CanTransition(IncidentStatus from, IncidentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureTransition(IncidentStatus from, IncidentStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw DomainException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"Không thể chuyển sự cố từ trạng thái {from} sang {to}.");
        }
    }

    public static bool IsOpen(IncidentStatus status) =>
        status is IncidentStatus.New or IncidentStatus.Assigned or IncidentStatus.InProgress;
}

/// <summary>Quy tac tinh SLA.</summary>
public static class SlaRules
{
    public static DateTime ComputeDeadline(DateTime dispatchedAtUtc, int slaHours) =>
        dispatchedAtUtc.AddHours(slaHours);

    /// <summary>Moc leo thang = thoi diem bat dau + escalationPercent % cua tong thoi han.</summary>
    public static DateTime EscalationDueAt(DateTime startUtc, DateTime deadlineUtc, int escalationPercent)
    {
        var total = deadlineUtc - startUtc;
        return startUtc + TimeSpan.FromTicks(total.Ticks * escalationPercent / 100);
    }

    /// <summary>Thoi han ban dau cua su co suy ra tu deadline va so gio SLA.</summary>
    public static DateTime SlaStart(DateTime deadlineUtc, int slaHours) => deadlineUtc.AddHours(-slaHours);
}
