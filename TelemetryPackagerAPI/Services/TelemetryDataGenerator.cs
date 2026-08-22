using System;
using System.Collections.Generic;
using IcdModelsLIbrary;
using TelemetrySimulator.DTOs;

namespace TelemetrySimulator.Services
{
    public class TelemetryDataGenerator : ITelemetryDataGenerator
    {
        private const string FLOAT_FORMAT_SPECIFIER = "F2";
        private const string FLOAT_TYPE_NAME = "float";
        private const string DOUBLE_TYPE_NAME = "double";

        

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

            foreach (IcdItem item in icd.IcdItems)
            {
                string fieldName = item.Name;
                string fieldType = item.Type.ToString().ToLower();

                int minVal = item.Min;
                int maxVal = item.Max;

                if (fieldType.Contains(FLOAT_TYPE_NAME) || fieldType.Contains(DOUBLE_TYPE_NAME))
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
    }
}