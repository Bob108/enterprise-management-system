using EMS.Application.AssetCategories;
using EMS.Application.Suppliers;
using EMS.Shared.Assets;
using EMS.Shared.Authorization;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/asset-categories")]
[Authorize]
public sealed class AssetCategoriesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssetCategoryDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetAssetCategoriesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Create(
        SaveAssetCategoryRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateAssetCategoryCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Update(
        int id, SaveAssetCategoryRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAssetCategoryCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteAssetCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public sealed class SuppliersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetSuppliersQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Create(
        SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSupplierCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Update(
        int id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSupplierCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSupplierCommand(id), cancellationToken);
        return NoContent();
    }
}
