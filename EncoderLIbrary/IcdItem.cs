using System.Text.Json.Serialization;

namespace EncoderLIbrary
{
    public class IcdItem : IIcdItem
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; }

        [JsonPropertyName("Location")]
        public int Location { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Mask")]
        public string Mask { get; set; }

        [JsonPropertyName("Bit")]
        public int Bit { get; set; }

        [JsonPropertyName("Min")]
        public float Min { get; set; }

        [JsonPropertyName("Max")]
        public float Max { get; set; }

        [JsonPropertyName("Type")]
        public string Type { get; set; }

        public int GetShiftFromMask()
        {
            if (string.IsNullOrWhiteSpace(Mask))
                return 0;

            // CHANGE: Added PadLeft(8, '0') to protect against short mask strings
            string trimmedMask = Mask.Trim().PadLeft(8, '0');

            for (int i = 0; i < trimmedMask.Length; i++)
            {
                int bitIndexFromRight = trimmedMask.Length - 1 - i;

                if (trimmedMask[bitIndexFromRight] == '1')
                {
                    return i;
                }
            }

            return 0;
        }
    }
}