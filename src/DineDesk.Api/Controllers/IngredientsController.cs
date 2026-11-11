using DineDesk.Api.Contracts;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

[Authorize]
public class IngredientsController : ApiControllerBase
{
    private readonly IIngredientService _ingredientService;

    public IngredientsController(IIngredientService ingredientService)
    {
        _ingredientService = ingredientService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ingredients = await _ingredientService.GetAllAsync();
        return Ok(ingredients.Select(i => new IngredientResponse(i.Id, i.Name, i.UnitOfMeasure, i.QuantityInStock, i.ReorderThreshold)));
    }

    [HttpPost("{id:int}/restock")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Restock(int id, [FromBody] RestockRequest request)
    {
        var result = await _ingredientService.RestockAsync(id, request.QuantityToAdd);
        return FromResult(result, _ => NoContent());
    }
}
