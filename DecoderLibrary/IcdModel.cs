using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DecoderLIbrary
{
    public class IcdModel
    {
        public List<IcdItem> IcdItems { get; set; }

        public IcdModel()
        {
            IcdItems = new List<IcdItem>();
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

                IcdModel model = new IcdModel
                {
                    IcdItems = deserializedItems ?? new List<IcdItem>()
                };

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to deserialize ICD JSON: {ex.Message}");
                throw; 
            }
        }
    }
}