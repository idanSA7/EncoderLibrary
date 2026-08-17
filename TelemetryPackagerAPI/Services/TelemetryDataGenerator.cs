using IcdModelsLIbrary;
using System;
using System.Collections.Generic;
using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public interface ITelemetryDataGenerator
    {
        Dictionary<string, string> PrepareTelemetryData(IcdModel icd, TelemetrySimulationRequestDto configuration);
    }

    public class TelemetryDataGenerator : ITelemetryDataGenerator
    {
        private const string SYNC_FIELD_VALUE = "172";
        private const string LENGTH_FIELD_VALUE = "38";
        private const string FLOAT_FORMAT_SPECIFIER = "F2";
        private const string FLOAT_TYPE_NAME = "float";

        private byte _packetCounter = 0;

        public Dictionary<string, string> PrepareTelemetryData(IcdModel icd, TelemetrySimulationRequestDto configuration)
        {
            Dictionary<string, string> currentTelemetryInputs = GenerateRandomTelemetryInputs(icd);

            if (configuration.TelemetryInputs != null && configuration.TelemetryInputs.Count > 0)
            {
                foreach (KeyValuePair<string, string> input in configuration.TelemetryInputs)
                {
                    currentTelemetryInputs[input.Key] = input.Value;
                }
            }

            return currentTelemetryInputs;
        }

        private Dictionary<string, string> GenerateRandomTelemetryInputs(IcdModel icd)
        {
            Random random = new Random();
            Dictionary<string, string> randomInputs = new Dictionary<string, string>();

            _packetCounter++;

            foreach (IcdItem item in icd.IcdItems)
            {
                string fieldName = item.Name;
                string fieldType = item.Type.ToString().ToLower();
                SpecialTelemetryField specialField = ParseSpecialField(fieldName);

                if (specialField == SpecialTelemetryField.Sync)
                {
                    randomInputs[fieldName] = SYNC_FIELD_VALUE;
                    continue;
                }

                if (specialField == SpecialTelemetryField.Counter)
                {
                    randomInputs[fieldName] = _packetCounter.ToString();
                    continue;
                }

                if (specialField == SpecialTelemetryField.Length)
                {
                    randomInputs[fieldName] = LENGTH_FIELD_VALUE;
                    continue;
                }

                int minVal = item.Min;
                int maxVal = item.Max;

                if (fieldType.Contains(FLOAT_TYPE_NAME))
                {
                    double range = maxVal - minVal;
                    double sample = (random.NextDouble() * range) + minVal;
                    randomInputs[fieldName] = sample.ToString(FLOAT_FORMAT_SPECIFIER);
                }
                else
                {
                    if (minVal == maxVal)
                    {
                        randomInputs[fieldName] = minVal.ToString();
                    }
                    else
                    {
                        randomInputs[fieldName] = random.Next(minVal, maxVal + 1).ToString();
                    }
                }
            }

            return randomInputs;
        }

        private SpecialTelemetryField ParseSpecialField(string fieldName)
        {
            if (fieldName.Equals(nameof(SpecialTelemetryField.Sync), StringComparison.OrdinalIgnoreCase))
                return SpecialTelemetryField.Sync;

            if (fieldName.Equals(nameof(SpecialTelemetryField.Counter), StringComparison.OrdinalIgnoreCase))
                return SpecialTelemetryField.Counter;

            if (fieldName.Equals(nameof(SpecialTelemetryField.Length), StringComparison.OrdinalIgnoreCase))
                return SpecialTelemetryField.Length;

            return SpecialTelemetryField.None;
        }
    }
}