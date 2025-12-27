using Dapper;
using Npgsql;
using WebApp.DAL.Interfaces;
using WebApp.DAL.Models;

namespace WebApp.DAL.Repositories
{
    public class AuditLogOrderRepository : IAuditLogOrderRepository
    {
        private readonly UnitOfWork _unitOfWork;

        public AuditLogOrderRepository(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<V1AuditLogOrderDal[]> BulkInsert(V1AuditLogOrderDal[] auditLogs, CancellationToken token)
        {
            // ИСПРАВЛЕНИЕ: Используем отдельные INSERT для каждой записи
            const string sql = @"
                INSERT INTO audit_log_order 
                (order_id, order_item_id, customer_id, order_status, created_at, updated_at)
                VALUES 
                (@OrderId, @OrderItemId, @CustomerId, @OrderStatus, @CreatedAt, @UpdatedAt)
                RETURNING *";

            var connection = await _unitOfWork.GetConnection(token);
            var result = new List<V1AuditLogOrderDal>();

            // Выполняем для каждой записи отдельно
            foreach (var auditLog in auditLogs)
            {
                var inserted = await connection.QuerySingleAsync<V1AuditLogOrderDal>(
                    new CommandDefinition(sql, auditLog, cancellationToken: token));
                result.Add(inserted);
            }

            return result.ToArray();
        }

        public async Task<V1AuditLogOrderDal[]> Query(QueryAuditLogOrderDalModel model, CancellationToken token)
        {
            var sql = @"
                SELECT *
                FROM audit_log_order
                WHERE 1=1";

            var parameters = new DynamicParameters();

            if (model.Ids?.Length > 0)
            {
                sql += " AND id = ANY(@Ids)";
                parameters.Add("Ids", model.Ids);
            }

            if (model.OrderIds?.Length > 0)
            {
                sql += " AND order_id = ANY(@OrderIds)";
                parameters.Add("OrderIds", model.OrderIds);
            }

            if (model.CustomerIds?.Length > 0)
            {
                sql += " AND customer_id = ANY(@CustomerIds)";
                parameters.Add("CustomerIds", model.CustomerIds);
            }

            sql += " ORDER BY created_at DESC LIMIT @Limit OFFSET @Offset";
            parameters.Add("Limit", model.Limit);
            parameters.Add("Offset", model.Offset);

            var connection = await _unitOfWork.GetConnection(token);
            var result = await connection.QueryAsync<V1AuditLogOrderDal>(
                new CommandDefinition(sql, parameters, cancellationToken: token));

            return result.ToArray();
        }
    }
}