namespace GridSmart.Api.Exceptions;

/// <summary>Loi nghiep vu, middleware chuyen thanh ProblemDetails voi truong code.</summary>
public class DomainException(string code, string message, int httpStatus = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int HttpStatus { get; } = httpStatus;

    public static DomainException NotFound(string code, string message) => new(code, message, 404);
    public static DomainException Forbidden(string code, string message) => new(code, message, 403);
    public static DomainException Conflict(string code, string message) => new(code, message, 409);
    public static DomainException Unprocessable(string code, string message) => new(code, message, 422);
}

/// <summary>Ma loi nghiep vu tra ve cho frontend.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string ImageInvalid = "IMAGE_INVALID";
    public const string ImageTooLarge = "IMAGE_TOO_LARGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string IncidentNotFound = "INCIDENT_NOT_FOUND";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string NotAssignedOfficer = "NOT_ASSIGNED_OFFICER";
    public const string AfterPhotoOutOfGrid = "AFTER_PHOTO_OUT_OF_GRID";
    public const string AlreadyRated = "ALREADY_RATED";
    public const string NotResolved = "NOT_RESOLVED";
    public const string OutOfScope = "OUT_OF_SCOPE";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string TokenReused = "TOKEN_REUSED";
    public const string AdminUnitInvalidLevel = "ADMIN_UNIT_INVALID_LEVEL";
    public const string AdminUnitCycle = "ADMIN_UNIT_CYCLE";
    public const string GridGeometryInvalid = "GRID_GEOMETRY_INVALID";
    public const string PrivilegeEscalation = "PRIVILEGE_ESCALATION";
    public const string NotFound = "NOT_FOUND";
    public const string Duplicate = "DUPLICATE";
    public const string Conflict = "CONFLICT";
}
