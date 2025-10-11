using DefaultNamespace;
using DefaultNamespace.Interfaces;
using DefaultNamespace.models;
using Models.Dto.Common;

namespace WebApp.BLL.Services;

public class AuditLogService(UnitOfWork unitOfWork, IAuditLogOrderRepository auditLogOrderRepository){
    public async Task<AuditLogOrderUnit[]> BatchInsert(AuditLogOrderUnit[] logUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);

        try
        {
            var dalModels = logUnits.Select(x => new V1AuditLogOrderDal
            {
                OrderId = x.OrderId,
                OrderItemId = x.OrderItemId,
                CustomerId = x.CustomerId,
                OrderStatus = x.OrderStatus,
                CreatedAt = now,
                UpdatedAt = now
            }).ToArray();

            var insertedLogs = await auditLogOrderRepository.BulkInsert(dalModels, token);

            await transaction.CommitAsync(token);
            
            var result = insertedLogs.Select(x => new AuditLogOrderUnit
            {
                OrderId = x.OrderId,
                OrderItemId = x.OrderItemId,
                CustomerId = x.CustomerId,
                OrderStatus = x.OrderStatus,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToArray();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }
}
