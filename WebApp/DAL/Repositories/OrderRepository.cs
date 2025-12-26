using System.Text;
using Dapper;
using DefaultNamespace.Interfaces;
using DefaultNamespace.models;

namespace DefaultNamespace.Repositories;

public class OrderRepository(UnitOfWork unitOfWork) : IOrderRepository
{
    public async Task<V1OrderDal[]> BulkInsert(V1OrderDal[] model, CancellationToken token)
    {
        var sql = @"
            INSERT INTO orders 
            (
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                status
             )
            SELECT 
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                status
            FROM UNNEST(@Orders)
            RETURNING 
                id,
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                status;
        ";

        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(
            sql, new { Orders = model }, cancellationToken: token));

        return res.ToArray();
    }

    public async Task<V1OrderDal[]> Query(QueryOrdersDalModel model, CancellationToken token)
    {
        var sql = new StringBuilder(@"
            SELECT 
                id,
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                status
            FROM orders
        ");

        var param = new DynamicParameters();
        var conditions = new List<string>();

        if (model.Ids?.Length > 0)
        {
            param.Add("Ids", model.Ids);
            conditions.Add("id = ANY(@Ids)");
        }

        if (model.CustomerIds?.Length > 0)
        {
            param.Add("CustomerIds", model.CustomerIds);
            conditions.Add("customer_id = ANY(@CustomerIds)");
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE " + string.Join(" AND ", conditions));
        }

        if (model.Limit > 0)
        {
            sql.Append(" LIMIT @Limit");
            param.Add("Limit", model.Limit);
        }

        if (model.Offset > 0)
        {
            sql.Append(" OFFSET @Offset");
            param.Add("Offset", model.Offset);
        }

        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(
            sql.ToString(), param, cancellationToken: token));

        return res.ToArray();
    }

    public async Task<List<V1OrderDal>> GetByIdsAsync(List<long> orderIds, CancellationToken token)
    {
        if (orderIds.Count == 0) return [];

        var sql = @"
            SELECT id, customer_id, delivery_address, total_price_cents, total_price_currency, created_at, updated_at, status
            FROM orders
            WHERE id = ANY(@OrderIds)
        ";

        var conn = await unitOfWork.GetConnection(token);
        var result = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(sql, new { OrderIds = orderIds }, cancellationToken: token));
        return result.ToList();
    }

    public async Task UpdateStatusesAsync(List<long> orderIds, string newStatus, CancellationToken token)
    {
        if (orderIds.Count == 0) return;

        var sql = @"
            UPDATE orders
            SET status = @NewStatus, updated_at = NOW()
            WHERE id = ANY(@OrderIds)
        ";

        var conn = await unitOfWork.GetConnection(token);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { OrderIds = orderIds, NewStatus = newStatus }, cancellationToken: token));
    }
}