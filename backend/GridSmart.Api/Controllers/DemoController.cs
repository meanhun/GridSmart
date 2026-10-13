using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GridSmart.Api.Controllers;

/// <summary>Cong cu trinh dien (BE-31). Chi hoat dong o moi truong Demo va Testing, noi khac tra 404.</summary>
[ApiController]
[Route("api/demo")]
[Tags("Demo")]
[Authorize(Roles = RoleCodes.SystemAdmin)]
public sealed class DemoController(DemoDataService demo, IHostEnvironment env) : ControllerBase
{
    [HttpPost("incidents/{id:long}/fast-forward")]
    [EndpointSummary("Lùi thời gian sự cố để kích hoạt leo thang/quá hạn ngay (chỉ môi trường Demo).")]
    public async Task<ActionResult<SlaRunResult>> FastForward(long id, int minutes, CancellationToken ct)
    {
        if (!env.IsEnvironment("Demo") && !env.IsEnvironment("Testing")) return NotFound();
        return await demo.FastForwardAsync(id, minutes, ct);
    }
}
