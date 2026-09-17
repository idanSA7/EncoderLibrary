using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TelemetryMongoConsumer.Configuration;
using TelemetryMongoConsumer.Interfaces;
using TelemetryMongoConsumer.Models;

namespace TelemetryMongoConsumer.Services
{
    public class KafkaMongoConsumerWorker : IKafkaConsumerManager, IDisposable
    {
        private readonly ILogger<KafkaMongoConsumerWorker> _logger;
        private readonly ITelemetryMongoRepository _repository;
        private readonly KafkaSettings _kafkaSettings;

        private CancellationTokenSource? _cts;
        private Task? _executingTask;

        public bool IsRunning => _executingTask != null && !_executingTask.IsCompleted;

        public KafkaMongoConsumerWorker(
            ILogger<KafkaMongoConsumerWorker> logger,
            ITelemetryMongoRepository repository,
            IOptions<KafkaSettings> kafkaOptions)
        {
            _logger = logger;
            _repository = repository;
            _kafkaSettings = kafkaOptions.Value;
        }

        public bool Start()
        {
            if (IsRunning) return false;

            _cts = new CancellationTokenSource();
            _executingTask = Task.Run(() => RunConsumerLoopAsync(_cts.Token));
            _logger.LogInformation("Kafka Mongo Consumer started manually.");
            return true;
        }

        public bool Stop()
        {
            if (!IsRunning || _cts == null) return false;

            _cts.Cancel();
            try
            {
                int timeoutSec = _kafkaSettings.StopTimeoutSeconds > 0 ? _kafkaSettings.StopTimeoutSeconds : 5;
                _executingTask?.Wait(TimeSpan.FromSeconds(timeoutSec));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }

            _cts.Dispose();
            _cts = null;
            _executingTask = null;
            _logger.LogInformation("Kafka Mongo Consumer stopped manually.");
            return true;
        }

        private async Task RunConsumerLoopAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (_kafkaSettings.Topics == null || _kafkaSettings.Topics.Count == 0)
                {
                    _logger.LogError("No Kafka topics found in configuration. Subscription aborted.");
                    return;
                }

                using IConsumer<Null, string> consumer = BuildConsumer();
                consumer.Subscribe(_kafkaSettings.Topics);
                _logger.LogInformation("Subscribed to topics: {Topics}", string.Join(", ", _kafkaSettings.Topics));

                while (!stoppingToken.IsCancellationRequested)
                {
                    await ConsumeAndProcessSingleMessageAsync(consumer, stoppingToken);
                }

                consumer.Close();
                _logger.LogInformation("Kafka Consumer closed gracefully.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "FATAL: Kafka Consumer loop crashed unexpectedly!");
            }
        }

        private async Task ConsumeAndProcessSingleMessageAsync(IConsumer<Null, string> consumer, CancellationToken stoppingToken)
        {
            try
            {
                ConsumeResult<Null, string> consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value == null)
                {
                    return;
                }

                await ProcessAndPersistMessageAsync(consumeResult);
                consumer.Commit(consumeResult);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Kafka message or persisting to MongoDB");
            }
        }

        private async Task ProcessAndPersistMessageAsync(ConsumeResult<Null, string> consumeResult)
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

        private IConsumer<Null, string> BuildConsumer()
        {
            ConsumerConfig config = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = _kafkaSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            return new ConsumerBuilder<Null, string>(config).Build();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}