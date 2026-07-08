using EMS.Application.Assets;
using EMS.Application.Common.Interfaces;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/assets")]
public sealed class AssetsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Assets.View)]
    public async Task<ActionResult<PagedResult<AssetListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? departmentId,
        [FromQuery] AssetStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(
            new GetAssetsQuery(search, categoryId, departmentId, status, page, pageSize),
            cancellationToken));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Assets.View)]
    public async Task<ActionResult<AssetDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetAssetByIdQuery(id), cancellationToken));

    [HttpGet("{id:int}/qrcode")]
    [HasPermission(Permissions.Assets.View)]
    public async Task<IActionResult> GetQrCode(
        int id, [FromServices] IQrCodeGenerator qrGenerator, CancellationToken cancellationToken)
    {
        var asset = await mediator.Send(new GetAssetByIdQuery(id), cancellationToken);
        return File(qrGenerator.GeneratePng(asset.AssetCode), "image/png");
    }

    [HttpPost]
    [HasPermission(Permissions.Assets.Create)]
    public async Task<ActionResult<int>> Register(
        RegisterAssetRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new RegisterAssetCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Assets.Edit)]
    public async Task<IActionResult> Update(
        int id, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAssetCommand(id, request), cancellationToken);
        return NoContent();
    }

    // Lifecycle transitions are verbs, not PATCHes (design §8).

    [HttpPost("{id:int}/assign")]
    [HasPermission(Permissions.Assets.Assign)]
    public async Task<IActionResult> Assign(
        int id, AssignAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new AssignAssetCommand(id, request.EmployeeId, request.ConditionNotes), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/return")]
    [HasPermission(Permissions.Assets.Assign)]
    public async Task<IActionResult> Return(
        int id, ReturnAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReturnAssetCommand(id, request.ConditionNotes), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/transfer")]
    [HasPermission(Permissions.Assets.Transfer)]
    public async Task<IActionResult> Transfer(
        int id, TransferAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new TransferAssetCommand(id, request.ToDepartmentId, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/repair")]
    [HasPermission(Permissions.Assets.Edit)]
    public async Task<IActionResult> MarkUnderRepair(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkAssetUnderRepairCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/repaired")]
    [HasPermission(Permissions.Assets.Edit)]
    public async Task<IActionResult> MarkRepaired(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkAssetRepairedCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/report-lost")]
    [HasPermission(Permissions.Assets.Edit)]
    public async Task<IActionResult> ReportLost(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReportAssetLostCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/recover")]
    [HasPermission(Permissions.Assets.Edit)]
    public async Task<IActionResult> Recover(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecoverAssetCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/dispose")]
    [HasPermission(Permissions.Assets.Dispose)]
    public async Task<IActionResult> Dispose(
        int id, DisposeAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new DisposeAssetCommand(id, request), cancellationToken);
        return NoContent();
    }
}
