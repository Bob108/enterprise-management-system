using EMS.Application.Procurement;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/procurement/requests")]
[Authorize] // list/detail visibility for plain requesters is enforced in the handlers
public sealed class PurchaseRequestsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseRequestListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] PurchaseRequestStatus? status,
        [FromQuery] bool mineOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(
            new GetPurchaseRequestsQuery(search, status, mineOnly, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseRequestDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPurchaseRequestByIdQuery(id), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Procurement.Raise)]
    public async Task<ActionResult<int>> Create(
        SavePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseRequestCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Procurement.Raise)]
    public async Task<IActionResult> Update(
        int id, SavePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseRequestCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/submit")]
    [HasPermission(Permissions.Procurement.Raise)]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    // Stage-dependent permission (approve.l1 vs approve.l2) is resolved in the handler.
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApprovePurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
        int id, RejectPurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectPurchaseRequestCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReturnPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/convert")]
    [HasPermission(Permissions.Procurement.ManagePurchaseOrders)]
    public async Task<ActionResult<int>> Convert(
        int id, CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new CreatePurchaseOrderCommand(id, request), cancellationToken));
}

[ApiController]
[Route("api/v1/procurement/orders")]
public sealed class PurchaseOrdersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Procurement.View)]
    public async Task<ActionResult<PagedResult<PurchaseOrderListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(
            new GetPurchaseOrdersQuery(search, status, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Procurement.View)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken));

    [HttpPost("{id:int}/issue")]
    [HasPermission(Permissions.Procurement.ManagePurchaseOrders)]
    public async Task<IActionResult> Issue(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new IssuePurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [HasPermission(Permissions.Procurement.ManagePurchaseOrders)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/close")]
    [HasPermission(Permissions.Procurement.ManagePurchaseOrders)]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ClosePurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/receive")]
    [HasPermission(Permissions.Procurement.ReceiveGoods)]
    public async Task<ActionResult<int>> Receive(
        int id, ReceiveGoodsRequest request, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new ReceiveGoodsCommand(id, request), cancellationToken));
}
