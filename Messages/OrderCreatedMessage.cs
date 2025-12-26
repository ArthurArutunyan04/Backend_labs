using Models.Dto.Common;

namespace Messages;

public class OmsOrderCreatedMessage : BaseMessage
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string DeliveryAddress { get; set; }
    public long TotalPriceCents { get; set; }
    public string TotalPriceCurrency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public OrderItemMessage[] OrderItems { get; set; }

    public override string RoutingKey => "order.created";

    public class OrderItemMessage
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public string ProductTitle { get; set; }
        public string ProductUrl { get; set; }
        public long PriceCents { get; set; }
        public string PriceCurrency { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}