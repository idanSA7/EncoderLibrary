using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using TelemetryMongoConsumer.Interfaces;
using TelemetryMongoConsumer.Models;

namespace TelemetryMongoConsumer.Services
{
    public class TelemetryPacketProcessor : ITelemetryPacketProcessor
    {
        private readonly ITelemetryMongoRepository _repository;

        public TelemetryPacketProcessor(ITelemetryMongoRepository repository)
        {
            _repository = repository;
        }

        public async Task ProcessAndSaveAsync(ConsumeResult<Null, string> consumeResult)
        {
            DecodedPacketDocument document = BuildDecodedPacketDocument(consumeResult.Topic, consumeResult.Message.Value);
            await _repository.InsertDecodedPacketAsync(document);
        }

        private DecodedPacketDocument BuildDecodedPacketDocument(string topic, string jsonPayload)
        {
            Dictionary<string, object> parameters = ParseParametersFromJson(jsonPayload);
            string icdType = ExtractIcdTypeFromTopic(topic);

            return new DecodedPacketDocument
            {
                IcdType = icdType,
                DecodedAt = DateTime.UtcNow,
                Parameters = parameters
            };
        }

        private Dictionary<string, object> ParseParametersFromJson(string jsonPayload)
        {
            BsonDocument rawBsonDoc = BsonSerializer.Deserialize<BsonDocument>(jsonPayload);
            return rawBsonDoc.ToDictionary();
        }

        private string ExtractIcdTypeFromTopic(string topic)
        {
            if (topic.StartsWith("telemetry-", StringComparison.OrdinalIgnoreCase))
            {
                return topic.Substring("telemetry-".Length);
            }

            return topic;
        }
    }
}