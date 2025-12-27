using WebApp.BLL.Models;
using WebApp.DAL;
using WebApp.DAL.Interfaces;
using WebApp.DAL.Models;

namespace WebApp.BLL.Services
{
    public class AuditLogOrderService
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IAuditLogOrderRepository _auditLogOrderRepository;
        private readonly ILogger<AuditLogOrderService> _logger;

        public AuditLogOrderService(
            UnitOfWork unitOfWork,
            IAuditLogOrderRepository auditLogOrderRepository,
            ILogger<AuditLogOrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _auditLogOrderRepository = auditLogOrderRepository;
            _logger = logger;
        }

        public async Task<AuditLogOrderUnit[]> LogOrders(AuditLogOrderUnit[] auditLogs, CancellationToken token)
        {
            _logger.LogInformation("[AuditLogOrderService] Logging {Count} audit records", auditLogs.Length);

            var now = DateTimeOffset.UtcNow;

            var auditLogDals = auditLogs.Select(log => new V1AuditLogOrderDal
            {
                OrderId = log.OrderId,
                OrderItemId = log.OrderItemId,
                CustomerId = log.CustomerId,
                OrderStatus = log.OrderStatus,
                CreatedAt = now,
                UpdatedAt = now
            }).ToArray();

            var result = await _auditLogOrderRepository.BulkInsert(auditLogDals, token);

            _logger.LogInformation("[AuditLogOrderService] Successfully logged {Count} audit records", result.Length);

            return Map(result);
        }

        public async Task<AuditLogOrderUnit[]> GetAuditLogs(QueryAuditLogOrderModel model, CancellationToken token)
        {
            var result = await _auditLogOrderRepository.Query(new QueryAuditLogOrderDalModel
            {
                Ids = model.Ids,
                OrderIds = model.OrderIds,
                CustomerIds = model.CustomerIds,
                Limit = model.PageSize,
                Offset = (model.Page - 1) * model.PageSize
            }, token);

            return Map(result);
        }

        private AuditLogOrderUnit[] Map(V1AuditLogOrderDal[] auditLogs)
        {
            return auditLogs.Select(x => new AuditLogOrderUnit
            {
                Id = x.Id,
                OrderId = x.OrderId,
                OrderItemId = x.OrderItemId,
                CustomerId = x.CustomerId,
                OrderStatus = x.OrderStatus,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToArray();
        }
    }
}