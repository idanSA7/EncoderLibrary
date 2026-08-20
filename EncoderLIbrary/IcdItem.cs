using System.Text.Json.Serialization;

namespace EncoderLIbrary
{
    public class IcdItem : IItem
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Location")]
        public int Location { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Mask")]
        public string Mask { get; set; }

        [JsonPropertyName("Size")]
        public int Size { get; set; }

        [JsonPropertyName("Min")]
        public int Min { get; set; }

        [JsonPropertyName("Max")]
        public int Max { get; set; }

        [JsonPropertyName("Type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DataType Type { get; set; }
    }
}