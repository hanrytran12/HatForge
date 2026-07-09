using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HatForge.API.Controllers;

[Authorize(Roles = nameof(UserRole.Lead))]
[Route("api/lead-inventory")]
public class LeadInventoryController : BaseApiController
{
    private readonly ILeadInventoryService _leadInventoryService;

    public LeadInventoryController(ILeadInventoryService leadInventoryService)
    {
        _leadInventoryService = leadInventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeadMaterialStockDto>>>> GetStocks()
        => Success(await _leadInventoryService.GetStocksAsync(CurrentUserId));

    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeadMaterialStockTransactionDto>>>> GetTransactions()
        => Success(await _leadInventoryService.GetTransactionsAsync(CurrentUserId));

    [HttpPost("stock-in")]
    public async Task<ActionResult<ApiResponse<LeadMaterialStockDto>>> StockIn([FromBody] StockInLeadMaterialDto dto)
        => Success(await _leadInventoryService.StockInAsync(dto, CurrentUserId));

    [HttpPost("adjust")]
    public async Task<ActionResult<ApiResponse<LeadMaterialStockDto>>> Adjust([FromBody] AdjustLeadMaterialStockDto dto)
        => Success(await _leadInventoryService.AdjustAsync(dto, CurrentUserId));
}
