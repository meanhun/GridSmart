using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>Nguoi dung va gan vai tro theo pham vi (BE-08).</summary>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.UserView)]
    [EndpointSummary("Người dùng trong phạm vi.")]
    public Task<PagedResult<UserListItem>> List(string? q, string? roleCode, int? unitId, bool? locked, int? page, int? pageSize, CancellationToken ct) =>
        users.ListAsync(q, roleCode, unitId, locked, page, pageSize, ct);

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.UserView)]
    [EndpointSummary("Chi tiết người dùng kèm vai trò.")]
    public Task<UserDetail> Get(int id, CancellationToken ct) => users.GetAsync(id, ct);

    [HttpPost]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType<CreatedId>(StatusCodes.Status201Created)]
    [EndpointSummary("Tạo người dùng (chưa có vai trò).")]
    public async Task<IActionResult> Create(UserCreateRequest request, CancellationToken ct)
    {
        var id = await users.CreateAsync(request, ct);
        return Created($"/api/users/{id}", new CreatedId(id));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.UserManage)]
    [EndpointSummary("Sửa thông tin, bật tắt người dùng.")]
    public async Task<IActionResult> Update(int id, UserUpdateRequest request, CancellationToken ct)
    {
        await users.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/lock")]
    [HasPermission(Permissions.UserManage)]
    [EndpointSummary("Khóa tài khoản và thu hồi phiên.")]
    public async Task<IActionResult> Lock(int id, CancellationToken ct)
    {
        await users.LockAsync(id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/unlock")]
    [HasPermission(Permissions.UserManage)]
    [EndpointSummary("Mở khóa tài khoản.")]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct)
    {
        await users.LockAsync(id, false, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reset-password")]
    [HasPermission(Permissions.UserManage)]
    [EndpointSummary("Đặt lại mật khẩu, thu hồi mọi phiên.")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/roles")]
    [HasPermission(Permissions.UserView)]
    [EndpointSummary("Vai trò của người dùng kèm phạm vi, người gán, hạn dùng.")]
    public Task<IReadOnlyList<UserRoleDto>> Roles(int id, CancellationToken ct) => users.GetRolesAsync(id, ct);

    [HttpPost("{id:int}/roles")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType<CreatedId>(StatusCodes.Status201Created)]
    [EndpointSummary("Gán vai trò theo phạm vi, có kiểm tra chống leo quyền.")]
    public async Task<IActionResult> AssignRole(int id, AssignRoleRequest request, CancellationToken ct) =>
        Created($"/api/users/{id}/roles", new CreatedId(await users.AssignRoleAsync(id, request, ct)));

    [HttpDelete("{id:int}/roles/{userRoleId:int}")]
    [HasPermission(Permissions.UserManage)]
    [EndpointSummary("Thu hồi vai trò (is_active = 0, giữ lịch sử).")]
    public async Task<IActionResult> RevokeRole(int id, int userRoleId, CancellationToken ct)
    {
        await users.RevokeRoleAsync(id, userRoleId, ct);
        return NoContent();
    }
}

/// <summary>Vai tro va quyen (BE-08).</summary>
[ApiController]
[Route("api/roles")]
[Tags("Users")]
[HasPermission(Permissions.RoleManage)]
public sealed class RolesController(IRoleService roles) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Vai trò và quyền của từng vai trò.")]
    public Task<IReadOnlyList<RoleDto>> List(CancellationToken ct) => roles.ListAsync(ct);

    [HttpPost]
    [ProducesResponseType<CreatedId>(StatusCodes.Status201Created)]
    [EndpointSummary("Tạo vai trò mới.")]
    public async Task<IActionResult> Create(RoleUpsertRequest request, CancellationToken ct) =>
        Created("/api/roles", new CreatedId(await roles.CreateAsync(request, ct)));

    [HttpPut("{id:int}")]
    [EndpointSummary("Sửa vai trò. Không đổi mã của vai trò hệ thống.")]
    public async Task<IActionResult> Update(int id, RoleUpsertRequest request, CancellationToken ct)
    {
        await roles.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/permissions")]
    [EndpointSummary("Đặt lại danh sách quyền. Người giữ vai trò phải đăng nhập lại.")]
    public async Task<IActionResult> SetPermissions(int id, List<string> permissions, CancellationToken ct)
    {
        await roles.SetPermissionsAsync(id, permissions, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Ngừng vai trò không phải vai trò hệ thống và không còn ai giữ.")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("~/api/permissions")]
    [EndpointSummary("Danh mục 25 quyền.")]
    public Task<IReadOnlyList<PermissionDto>> ListPermissions(CancellationToken ct) => roles.ListPermissionsAsync(ct);
}
