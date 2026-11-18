using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading.Tasks;
using TelemetryMongoConsumer.Configuration;
using TelemetryMongoConsumer.Models;
using TelemetryMongoConsumer.Interfaces;
namespace TelemetryMongoConsumer.Services
{
    

    public class TelemetryMongoRepository : ITelemetryMongoRepository
    {
        private readonly IMongoCollection<DecodedPacketDocument> _collection;

        public TelemetryMongoRepository(
            IMongoClient mongoClient,
            IOptions<MongoSettings> mongoOptions)
        {
            IMongoDatabase database = mongoClient.GetDatabase(mongoOptions.Value.DBName);
            _collection = database.GetCollection<DecodedPacketDocument>(mongoOptions.Value.CollectionName);
        }

        public async Task InsertDecodedPacketAsync(DecodedPacketDocument document)
        {
            await _collection.InsertOneAsync(document);
        }
    }
}