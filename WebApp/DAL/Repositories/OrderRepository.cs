using System.Text;
using Dapper;
using DefaultNamespace.Interfaces;
using DefaultNamespace.models;

namespace DefaultNamespace.Repositories;

public class OrderRepository(UnitOfWork unitOfWork) : IOrderRepository
{
    public async Task<V1OrderDal[]> BulkInsert(V1OrderDal[] model, CancellationToken token)
    {
        if (model.Length == 0) return [];

        var sql = @"
            INSERT INTO orders 
            (
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                order_status 
            )
            SELECT 
                t.customer_id,
                t.delivery_address,
                t.total_price_cents,
                t.total_price_currency,
                t.created_at,
                t.updated_at,
                t.order_status
            FROM UNNEST(
                @customerIds::bigint[],
                @addresses::text[],
                @prices::bigint[],
                @currencies::text[],
                @created::timestamptz[],
                @updated::timestamptz[],
                @statuses::text[]
            ) AS t(
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                order_status
            )
            RETURNING 
                id,
                customer_id,
                delivery_address,
                total_price_cents,
                total_price_currency,
                created_at,
                updated_at,
                order_status AS status; 
        ";

        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderDal>(new CommandDefinition(sql, new
        {
            customerIds = model.Select(x => x.CustomerId).ToArray(),
            addresses = model.Select(x => x.DeliveryAddress).ToArray(),
            prices = model.Select(x => x.TotalPriceCents).ToArray(),
            currencies = model.Select(x => x.TotalPriceCurrency).ToArray(),
            created = model.Select(x => x.CreatedAt).ToArray(),
            updated = model.Select(x => x.UpdatedAt).ToArray(),
            statuses = model.Select(x => x.Status).ToArray()
        }, cancellationToken: token));

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
                order_status AS status 
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
            SELECT 
                id, customer_id, delivery_address, 
                total_price_cents, total_price_currency, 
                created_at, updated_at,
                order_status AS status  
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
            SET order_status = @NewStatus, updated_at = NOW() 
            WHERE id = ANY(@OrderIds)
        ";

        var conn = await unitOfWork.GetConnection(token);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { OrderIds = orderIds, NewStatus = newStatus }, cancellationToken: token));
    }
}