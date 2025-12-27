namespace Consumer.Config
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; }

        public string GroupId { get; set; }

        public string OmsOrderCreatedTopic { get; set; }

        public string OmsOrderStatusChangedTopic { get; set; }

        public int CollectBatchSize { get; set; }

        public int CollectTimeoutMs { get; set; }
    }
}

