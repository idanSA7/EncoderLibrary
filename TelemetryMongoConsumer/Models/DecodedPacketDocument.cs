using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TelemetryMongoConsumer.Models
{
    public class DecodedPacketDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string IcdType { get; set; } = string.Empty;

        public DateTime DecodedAt { get; set; } = DateTime.UtcNow;

        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}