using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TelemetryMongoConsumer.Configuration;
using TelemetryMongoConsumer.Interfaces;
using TelemetryMongoConsumer.Models;

namespace TelemetryMongoConsumer.Services
{
    public class KafkaMongoConsumerWorker : BackgroundService
    {
        private readonly ILogger<KafkaMongoConsumerWorker> _logger;
        private readonly ITelemetryMongoRepository _repository;
        private readonly KafkaSettings _kafkaSettings;
        private readonly JsonSerializerOptions _jsonOptions;

        public KafkaMongoConsumerWorker(
            ILogger<KafkaMongoConsumerWorker> logger,
            ITelemetryMongoRepository repository,
            IOptions<KafkaSettings> kafkaOptions)
        {
            _logger = logger;
            _repository = repository;
            _kafkaSettings = kafkaOptions.Value;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            using IConsumer<string, string> consumer = BuildConsumer();
            consumer.Subscribe(_kafkaSettings.Topic);
            _logger.LogInformation("Kafka Mongo Consumer subscribed to topic: {Topic}", _kafkaSettings.Topic);

            try
            {
                await RunConsumerLoopAsync(consumer, stoppingToken);
            }
            finally
            {
                consumer.Close();
                _logger.LogInformation("Kafka Consumer closed gracefully.");
            }
        }

        private async Task RunConsumerLoopAsync(IConsumer<string, string> consumer, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value != null)
                    {
                        await ProcessAndPersistMessageAsync(consumeResult);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Kafka message or persisting to MongoDB");
                }
            }
        }

        private async Task ProcessAndPersistMessageAsync(ConsumeResult<string, string> consumeResult)
        {
            DecodedPacketDocument? document = JsonSerializer.Deserialize<DecodedPacketDocument>(
                consumeResult.Message.Value,
                _jsonOptions);

            if (document == null)
            {
                _logger.LogWarning("Skipping null or corrupted message from Kafka key: {Key}", consumeResult.Message.Key);
                return;
            }

            await _repository.InsertDecodedPacketAsync(document);
            _logger.LogInformation("Stored decoded packet in Mongo. Key: {Key}", consumeResult.Message.Key);
        }

        private IConsumer<string, string> BuildConsumer()
        {
            ConsumerConfig consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = _kafkaSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            return new ConsumerBuilder<string, string>(consumerConfig).Build();
        }
    }
}