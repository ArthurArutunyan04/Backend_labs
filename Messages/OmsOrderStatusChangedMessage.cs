namespace Messages
{
    public class OmsOrderStatusChangedMessage : BaseMessage
    {
        public long OrderId { get; set; }
        public long CustomerId { get; set; }
        public string OrderStatus { get; set; }
        public long[] OrderItemIds { get; set; } = [];
    }
}

