namespace Consumer.Config;

public class RabbitMqSettings
{
    public string HostName { get; set; }
    public int Port { get; set; }
    public string OrderCreatedQueue { get; set; }
    public ushort BatchSize { get; set; }
    public int BatchTimeoutSeconds { get; set; }
}