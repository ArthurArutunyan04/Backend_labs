using Microsoft.AspNetCore.Mvc;
using Models.Dto.Common;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using WebApp.BLL.Services;
using WebApp.Validators;

namespace WebApp.Controllers.V1;

[Route("api/v1/audit-log")]
public class AuditLogController(AuditLogService auditLogService, ValidatorFactory validatorFactory) : ControllerBase
{
    [HttpPost("batch-create")]
    public async Task<ActionResult<V1CreateAuditLogResponse>> V1BatchCreate([FromBody] V1CreateAuditLogRequest request, CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1CreateAuditLogRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        var logUnits = request.Orders.Select(x => new AuditLogOrderUnit
        {
            OrderId = x.OrderId,
            OrderItemId = x.OrderItemId,
            CustomerId = x.CustomerId,
            OrderStatus = x.OrderStatus
        }).ToArray();

        var res = await auditLogService.BatchInsert(logUnits, token);

        return Ok(new V1CreateAuditLogResponse
        {
            Orders = Map(res)
        });
    }

    private AuditLogOrderUnit[] Map(AuditLogOrderUnit[] logs)
    {
        return logs.Select(x => new AuditLogOrderUnit
        {
            OrderId = x.OrderId,
            OrderItemId = x.OrderItemId,
            CustomerId = x.CustomerId,
            OrderStatus = x.OrderStatus,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToArray();
    }
}