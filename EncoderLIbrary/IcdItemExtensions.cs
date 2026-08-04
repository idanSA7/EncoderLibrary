namespace EncoderLIbrary
{
    public static class IcdItemExtensions
    {
        private const int RequiredMaskLength = 8;
        private const char PaddingChar = '0';

        public static int GetShiftFromMask(this IcdItem item)
        {
            if (string.IsNullOrWhiteSpace(item?.Mask))
                return 0;

            string trimmedMask = item.Mask.Trim().PadLeft(RequiredMaskLength, PaddingChar);

            for (int shiftAmount = 0; shiftAmount < trimmedMask.Length; shiftAmount++)
            {
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