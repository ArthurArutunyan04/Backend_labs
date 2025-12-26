namespace Consumer.Config;

public class RabbitMqSettings
{
    public string HostName { get; set; }

    public int Port { get; set; }
    
    public TopicSettingsUnit OrderCreated { get; set; }
    
    public TopicSettingsUnit OrderStatusChanged { get; set; }
    
    public class TopicSettingsUnit
    {
        public string Queue { get; set; }

        public ushort BatchSize { get; set; }

        public int BatchTimeoutSeconds { get; set; }
        
        public DeadLetterSettings DeadLetter { get; set; }
    }
    
    public class DeadLetterSettings
    {
        public string Dlx { get; set; }

        public string Dlq { get; set; }

        public string RoutingKey { get; set; }
    }
}