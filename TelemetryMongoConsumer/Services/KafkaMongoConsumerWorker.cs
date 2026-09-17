using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelemetryMongoConsumer.Configuration;
using TelemetryMongoConsumer.Interfaces;

namespace TelemetryMongoConsumer.Services
{
    public class KafkaMongoConsumerWorker : IKafkaConsumerManager, IDisposable
    {
        private readonly ILogger<KafkaMongoConsumerWorker> _logger;
        private readonly KafkaSettings _kafkaSettings;
        private readonly ITelemetryPacketProcessor _packetProcessor;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _executingTask;

        public KafkaMongoConsumerWorker(
            ILogger<KafkaMongoConsumerWorker> logger,
            IOptions<KafkaSettings> kafkaOptions,
            ITelemetryPacketProcessor packetProcessor)
        {
            _logger = logger;
            _kafkaSettings = kafkaOptions.Value;
            _packetProcessor = packetProcessor;
        }

        public bool Start()
        {
            if (_executingTask != null && !_executingTask.IsCompleted)
            {
                _logger.LogWarning("Kafka Consumer is already running.");
                return false;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _executingTask = Task.Run(() => StartConsumerLoopAsync(_cancellationTokenSource.Token));
            _logger.LogInformation("Kafka Consumer started successfully.");
            return true;
        }

        public bool Stop()
        {
            if (_executingTask == null || _executingTask.IsCompleted)
            {
                _logger.LogWarning("Kafka Consumer is not running.");
                return false;
            }

            _cancellationTokenSource?.Cancel();

            try
            {
                _executingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _executingTask = null;

            _logger.LogInformation("Kafka Consumer stopped successfully.");
            return true;
        }

        private async Task StartConsumerLoopAsync(CancellationToken stoppingToken)
        {
            using IConsumer<Null, string> consumer = BuildConsumer();
            consumer.Subscribe(_kafkaSettings.Topic);
            _logger.LogInformation("Kafka Consumer subscribed to topic: {Topic}", _kafkaSettings.Topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ConsumeSingleMessageAsync(consumer, stoppingToken);
                }

                consumer.Close();
                _logger.LogInformation("Kafka Consumer closed gracefully.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "FATAL: Kafka Consumer loop crashed unexpectedly!");
            }
        }

        private async Task ConsumeSingleMessageAsync(IConsumer<Null, string> consumer, CancellationToken stoppingToken)
        {
            try
            {
                ConsumeResult<Null, string> consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value == null)
                {
                    return;
                }

                await _packetProcessor.ProcessAndSaveAsync(consumeResult);
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