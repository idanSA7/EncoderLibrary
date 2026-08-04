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

        [JsonPropertyName("Bit")]
        public int Size { get; set; }

        [JsonPropertyName("Min")]
        public int Min { get; set; }

        [JsonPropertyName("Max")]
        public int Max { get; set; }

        [JsonPropertyName("Type")]
        public DataType Type { get; set; }

        private const int RequiredMaskLength = 8;
        private const char PaddingChar = '0';
        public int GetShiftFromMask()
        {
            if (string.IsNullOrWhiteSpace(Mask))
                return 0;

            string trimmedMask = Mask.Trim().PadLeft(RequiredMaskLength, PaddingChar);

            for (int shiftAmount = 0; shiftAmount < trimmedMask.Length; shiftAmount++)
            {
                // Calculate the bit position starting from the least significant bit (rightmost side)
                int bitIndexFromRight = trimmedMask.Length - 1 - shiftAmount;

                if (trimmedMask[bitIndexFromRight] == '1')
                {
                    return shiftAmount;
                }
            }

            return 0;
        }
    }
}