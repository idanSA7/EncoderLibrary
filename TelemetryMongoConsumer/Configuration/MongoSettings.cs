namespace TelemetryMongoConsumer.Configuration
{
    public class MongoSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DBName { get; set; } = string.Empty;
        public string CollectionName { get; set; } = string.Empty;
    }
}