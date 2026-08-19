namespace TelemetryDeviceAPI.Services
{
    public interface IKafkaProducerService
    {
        public Task ProduceAsync(string topic, byte[] messageContent);
    }
}
