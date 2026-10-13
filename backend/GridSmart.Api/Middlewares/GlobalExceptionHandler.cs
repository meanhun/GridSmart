using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GridSmart.Api.Middlewares;

/// <summary>
/// Chuyen moi exception thanh ProblemDetails (RFC 9457) kem truong code.
/// Loi SQL duoc anh xa giong mau canteen: 547 (khoa ngoai) va 8144 -> 400, 2627/2601 (trung) -> 409, 50000 (THROW nghiep vu) -> 400 kem thong bao.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int status;
        string code;
        string title;
        switch (exception)
        {
            case DomainException d:
                status = d.HttpStatus;
                code = d.Code;
                title = d.Message;
                break;
            case SqlException sql:
                (status, code, title) = MapSqlException(sql);
                break;
            case BadHttpRequestException b:
                status = b.StatusCode;
                code = ErrorCodes.ValidationFailed;
                title = "Dữ liệu gửi lên không hợp lệ.";
                break;
            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                return true;
            default:
                logger.LogError(exception, "Loi khong xu ly duoc");
                status = StatusCodes.Status500InternalServerError;
                code = "INTERNAL_ERROR";
                title = "Hệ thống gặp lỗi, vui lòng thử lại sau.";
                break;
        }

        context.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://gridsmart.local/errors/{code.ToLowerInvariant()}",
            Instance = context.Request.Path,
        };
        details.Extensions["code"] = code;
        if (env.IsDevelopment() && status >= 500) details.Detail = exception.ToString();
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = details, Exception = exception });
    }

    private static (int Status, string Code, string Title) MapSqlException(SqlException ex) => ex.Number switch
    {
        547 => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed,
            "Vui lòng nhập đầy đủ thông tin hoặc dữ liệu đang được tham chiếu bởi bản ghi khác."),
        2627 or 2601 => (StatusCodes.Status409Conflict, ErrorCodes.Duplicate, "Dữ liệu đã tồn tại."),
        8144 => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Kiểm tra lại tham số đầu vào."),
        50000 => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, ex.Message),
        // 6522: kieu geography bao loi hinh hoc (vi du tọa độ ngoài phạm vi, vòng không khép kín)
        6522 => (422, ErrorCodes.GridGeometryInvalid,
            "Ranh giới không hợp lệ với bản đồ. Kiểm tra lại tọa độ: GeoJSON là [kinh độ, vĩ độ]."),
        _ => (StatusCodes.Status500InternalServerError, "DATABASE_ERROR", "Đã xảy ra lỗi cơ sở dữ liệu."),
    };
}
