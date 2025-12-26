namespace Messages;

public abstract class BaseMessage
{
    public abstract string RoutingKey { get; }
}