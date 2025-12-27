namespace WebApp.Config
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; }

        public string ClientId { get; set; }

        public string OmsOrderCreatedTopic { get; set; }

        public string OmsOrderStatusChangedTopic { get; set; }
    }
}

