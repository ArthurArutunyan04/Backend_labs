namespace WebApp.BLL.Models
{
    public class AuditLogOrderUnit
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public long OrderItemId { get; set; }
        public long CustomerId { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class QueryAuditLogOrderModel
    {
        public long[]? Ids { get; set; }
        public long[]? OrderIds { get; set; }
        public long[]? CustomerIds { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}