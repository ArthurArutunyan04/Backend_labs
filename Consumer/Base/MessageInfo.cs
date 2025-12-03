namespace Consumer.Base;

public class MessageInfo
{
    public string Message { get; set; }
    public ulong DeliveryTag { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}   