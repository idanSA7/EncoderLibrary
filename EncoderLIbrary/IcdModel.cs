using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EncoderLIbrary
{
    public class IcdModel
    {
        public List<IcdItem> items { get; set; }

        public IcdModel()
        {
            items = new List<IcdItem>();
        }

        public static IcdModel LoadFromJson(string jsonContent)
        {
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                List<IcdItem> deserializedItems = JsonSerializer.Deserialize<List<IcdItem>>(jsonContent, options);

                return new IcdModel
                {
                    items = deserializedItems ?? new List<IcdItem>()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to deserialize ICD JSON: {ex.Message}");
                return null; 
            }
        }

    }
}