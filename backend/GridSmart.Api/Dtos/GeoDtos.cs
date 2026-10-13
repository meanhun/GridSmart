using System.Text.Json;

namespace GridSmart.Api.Dtos;

// ---------- O luoi (BE-25) ----------
public sealed record GridDetail(
    int Id, string GridCode, string Name, int AdminUnitId, IReadOnlyList<UnitRef> AdminUnitPath, bool IsActive,
    JsonElement? Boundary, int? AssignedOfficerId, IReadOnlyList<GridOfficerDto> Officers,
    string StatusColor, int OpenIncidentCount, int OverdueIncidentCount);

public sealed record GridOfficerDto(int UserId, string FullName, string? Phone, bool IsPrimary);
public sealed record GridUpsertRequest(string GridCode, string Name, int AdminUnitId, JsonElement? Boundary, string? Wkt, bool? IsActive);
public sealed record GridSaveResult(int Id, IReadOnlyList<string> Warnings);
public sealed record AssignOfficersRequest(IReadOnlyList<int> UserIds, int? PrimaryUserId);
public sealed record ImportResultRow(string Code, string Action, string? Error, IReadOnlyList<string> Warnings);
public sealed record ImportResult(bool DryRun, int Created, int Updated, int Failed, IReadOnlyList<ImportResultRow> Rows);

// ---------- Don vi hanh chinh (BE-24) ----------
public sealed record AdminUnitNode(int Id, string UnitCode, string Name, string TypeCode, string TypeName, string LevelCode, int LevelOrder,
    int? ParentId, string? UnitPath, bool IsActive, IReadOnlyList<AdminUnitNode> Children);

public sealed record AdminUnitDetail(int Id, string UnitCode, string Name, int UnitTypeId, string TypeCode, string LevelCode,
    int? ParentId, string? UnitPath, IReadOnlyList<UnitRef> Path, bool IsActive, DateOnly? EffectiveFrom, DateOnly? EffectiveTo,
    int? MergedIntoUnitId, JsonElement? Boundary, int ChildCount, int GridCount);

public sealed record AdminLevelDto(int Id, string LevelCode, string LevelName, int LevelOrder);
public sealed record AdminUnitTypeDto(int Id, string TypeCode, string TypeName, int LevelId, string LevelCode);
public sealed record AdminUnitUpsertRequest(string UnitCode, string Name, int UnitTypeId, int? ParentId, JsonElement? Boundary, string? Wkt,
    DateOnly? EffectiveFrom, bool? IsActive);
public sealed record NewUnitSpec(string UnitCode, string Name, int UnitTypeId, int? ParentId);
public sealed record MergeRequest(IReadOnlyList<int> SourceUnitIds, int? TargetUnitId, NewUnitSpec? NewUnit, DateOnly EffectiveDate);
public sealed record MergeResult(int TargetUnitId, int MovedChildren, int MovedGrids, int MovedRoleAssignments, int AffectedUsers);
