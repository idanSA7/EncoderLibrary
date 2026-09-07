using Confluent.Kafka;
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
    public class KafkaMongoConsumerWorker : IKafkaConsumerManager, IDisposable
    {
        private readonly ILogger<KafkaMongoConsumerWorker> _logger;
        private readonly ITelemetryMongoRepository _repository;
        private readonly KafkaSettings _kafkaSettings;
        private readonly JsonSerializerOptions _jsonOptions;

        private CancellationTokenSource? _cts;
        private Task? _executingTask;
        private readonly object _lock = new();

        public bool IsRunning => _executingTask != null && !_executingTask.IsCompleted;

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

        public bool Start()
        {
            lock (_lock)
            {
                if (IsRunning) return false;

                _cts = new CancellationTokenSource();
                _executingTask = Task.Run(() => RunConsumerLoopAsync(_cts.Token));
                _logger.LogInformation("Kafka Mongo Consumer started manually.");
                return true;
            }
        }

        public bool Stop()
        {
            lock (_lock)
            {
                if (!IsRunning || _cts == null) return false;

                _cts.Cancel();
                try
                {
                    _executingTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }

                _cts.Dispose();
                _cts = null;
                _executingTask = null;
                _logger.LogInformation("Kafka Mongo Consumer stopped manually.");
                return true;
            }
        }

        private async Task RunConsumerLoopAsync(CancellationToken stoppingToken)
        {
            using IConsumer<string, string> consumer = BuildConsumer();
            consumer.Subscribe(_kafkaSettings.Topic);
            _logger.LogInformation("Subscribed to topic: {Topic}", _kafkaSettings.Topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        ConsumeResult<string, string> consumeResult = consumer.Consume(stoppingToken);
                        if (consumeResult?.Message?.Value != null)
                        {
                            DecodedPacketDocument? document = JsonSerializer.Deserialize<DecodedPacketDocument>(
                                consumeResult.Message.Value, _jsonOptions);

                            if (document != null)
                            {
                                await _repository.InsertDecodedPacketAsync(document);
                                _logger.LogInformation("Stored packet in Mongo. Key: {Key}", consumeResult.Message.Key);
                            }

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
            finally
            {
                consumer.Close();
                _logger.LogInformation("Kafka Consumer closed gracefully.");
            }
        }

        private IConsumer<string, string> BuildConsumer()
        {
            ConsumerConfig config = new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = _kafkaSettings.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            return new ConsumerBuilder<string, string>(config).Build();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}