using Microsoft.AspNetCore.Mvc;
using WebApp.BLL.Services;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;

namespace WebApp.Controllers.V1
{
    [Route("api/v1/audit")]
    public class AuditController : ControllerBase
    {
        private readonly AuditLogOrderService _auditLogOrderService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(AuditLogOrderService auditLogOrderService, ILogger<AuditController> logger)
        {
            _auditLogOrderService = auditLogOrderService;
            _logger = logger;
        }

        [HttpPost("log-order")]
        public async Task<ActionResult<V1AuditLogOrderResponse>> LogOrder(
            [FromBody] V1AuditLogOrderRequest request,
            CancellationToken token)
        {
            _logger.LogInformation("[AuditController] Received audit log request for {Count} orders",
                request.Orders.Length);

            try
            {
                // 1. Преобразуем DTO в модель БД
                var auditLogs = request.Orders.Select(order => new BLL.Models.AuditLogOrderUnit
                {
                    OrderId = order.OrderId,
                    OrderItemId = order.OrderItemId,
                    CustomerId = order.CustomerId,
                    OrderStatus = order.OrderStatus
                }).ToArray();


                // Принимает HTTP POST запрос от Consumer
                // Сохраняет данные в БД через сервис
                // 2. Сохраняем в таблицу audit_log_order
                var result = await _auditLogOrderService.LogOrders(auditLogs, token);

                var response = new V1AuditLogOrderResponse
                {
                    AuditLogs = result.Select(log => new V1AuditLogOrderResponse.AuditLogOrder
                    {
                        Id = log.Id,
                        OrderId = log.OrderId,
                        OrderItemId = log.OrderItemId,
                        CustomerId = log.CustomerId,
                        OrderStatus = log.OrderStatus,
                        CreatedAt = log.CreatedAt,
                        UpdatedAt = log.UpdatedAt
                    }).ToArray()
                };

                _logger.LogInformation("[AuditController] Successfully processed audit log request");

                // Возвращает HTTP ответ
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditController] Error processing audit log request");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}