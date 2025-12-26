namespace Consumer.Base;

public class MessageInfo<TMessage>
{
    public TMessage Message { get; set; }
    public ulong DeliveryTag { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}