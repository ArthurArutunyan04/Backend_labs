using Dapper;
using DefaultNamespace.Interfaces;
using DefaultNamespace.models;

namespace DefaultNamespace.Repositories;

public class AuditLogOrderRepository(UnitOfWork unitOfWork): IAuditLogOrderRepository
{
    public async Task<V1AuditLogOrderDal[]> BulkInsert(V1AuditLogOrderDal[] logs, CancellationToken token)
    {
        var sql = @"
            insert into audit_log_order
            (
                order_id,
                order_item_id,
                customer_id,
                order_status,
                created_at,
                updated_at
            )
            select
                order_id,
                order_item_id,
                customer_id,
                order_status,
                created_at,
                updated_at
            from unnest(@Logs)
            returning
                id,
                order_id,
                order_item_id,
                customer_id,
                order_status,
                created_at,
                updated_at;
        ";

        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1AuditLogOrderDal>(
            new CommandDefinition(sql, new { Logs = logs }, cancellationToken: token));

        return res.ToArray();
    }
}