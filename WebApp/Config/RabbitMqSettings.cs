namespace WebApp.Config;

public class RabbitMqSettings
{
    public string HostName { get; set; }

    public int Port { get; set; }

    public string Exchange { get; set; }

    public ExchangeMapping[] ExchangeMappings { get; set; }

    public class ExchangeMapping
    {
        public string Queue { get; set; }

        public string RoutingKeyPattern { get; set; }
        public DeadLetterSettings? DeadLetter { get; set; }
    }

    public class DeadLetterSettings
    {
        public string Dlx { get; set; }

        public string RoutingKey { get; set; }
    }
}