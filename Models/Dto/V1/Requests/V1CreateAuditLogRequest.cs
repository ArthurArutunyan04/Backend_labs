namespace Models.Dto.V1.Requests;

public class V1CreateAuditLogRequest
{
    public LogOrder[] Orders { get; set; }
    
    public class LogOrder
    {
        public long OrderId { get; set; }

        public long OrderItemId { get; set; }

        public long CustomerId { get; set; }

        public string OrderStatus { get; set; }
    }
}