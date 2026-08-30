namespace TelemetryDeviceAPI.Models
{
    public class PacketContext
    {
        public byte[] RawData { get; set; } = [];
        public int DestinationPort { get; set; }
        public IcdType IcdType { get; set; }
        public Dictionary<string, object>? DecodedData { get; set; }
    }
}
