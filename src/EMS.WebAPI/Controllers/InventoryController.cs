using EMS.Application.Inventory;
using EMS.Application.Warehouses;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Inventory;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/inventory/items")]
public sealed class InventoryController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Inventory.View)]
    public async Task<ActionResult<PagedResult<InventoryItemListDto>>> GetItems(
        [FromQuery] string? search,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(
            new GetInventoryItemsQuery(search, lowStockOnly, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Inventory.View)]
    public async Task<ActionResult<InventoryItemDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetInventoryItemByIdQuery(id), cancellationToken));

    [HttpGet("{id:int}/transactions")]
    [HasPermission(Permissions.Inventory.View)]
    public async Task<ActionResult<IReadOnlyList<InventoryTransactionDto>>> GetTransactions(
        int id, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetItemTransactionsQuery(id, take), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<ActionResult<int>> Create(
        CreateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateInventoryItemCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Update(
        int id, UpdateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateInventoryItemCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteInventoryItemCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/stock-in")]
    [HasPermission(Permissions.Inventory.StockIn)]
    public async Task<IActionResult> StockIn(
        int id, StockMovementRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new StockInCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/stock-out")]
    [HasPermission(Permissions.Inventory.StockOut)]
    public async Task<IActionResult> StockOut(
        int id, StockMovementRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new StockOutCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/adjust")]
    [HasPermission(Permissions.Inventory.Adjust)]
    public async Task<IActionResult> Adjust(
        int id, StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AdjustStockCommand(id, request), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/warehouses")]
[Authorize]
public sealed class WarehousesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetWarehousesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Create(
        SaveWarehouseRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateWarehouseCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Update(
        int id, SaveWarehouseRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateWarehouseCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteWarehouseCommand(id), cancellationToken);
        return NoContent();
    }
}
