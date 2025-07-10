/*
 * Server.Core/Services/MqttService.cs
 * 연결 안정성 확보를 위해 MQTT 프로토콜 버전을 명시적으로 지정합니다.
 */
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using Server.Core.Models;
using System.Text;

namespace Server.Core.Services
{
    public class MqttService : IMqttService
    {
        private readonly ILogger<MqttService> _logger;
        private readonly MqttBrokerConfig _config;
        private IManagedMqttClient? _managedMqttClient;
        private Func<string, string, Task>? _messageHandler;

        public MqttService(IOptions<ServerConfig> config, ILogger<MqttService> logger)
        {
            _config = config.Value.MqttBroker;
            _logger = logger;
        }

        public async Task StartAsync(Func<string, string, Task> messageHandler, CancellationToken cancellationToken)
        {
            _messageHandler = messageHandler;

            var factory = new MqttFactory();
            _managedMqttClient = factory.CreateManagedMqttClient();

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(_config.Address, _config.Port)
                    .WithClientId($"plc-server-core-{Guid.NewGuid()}")
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311) // <-- 이 줄을 추가하여 프로토콜 버전을 명시합니다.
                    .WithCleanSession()
                    .Build())
                .Build();

            var topicFilters = new List<MqttTopicFilter>
            {
                new MqttTopicFilterBuilder().WithTopic("dt/#").Build(),
                new MqttTopicFilterBuilder().WithTopic("status/#").Build()
            };
            await _managedMqttClient.SubscribeAsync(topicFilters);

            _managedMqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceivedAsync;
            _managedMqttClient.ConnectedAsync += OnConnectedAsync;
            _managedMqttClient.DisconnectedAsync += OnDisconnectedAsync;

            _logger.LogInformation("Starting Managed MQTT client...");
            await _managedMqttClient.StartAsync(options);
        }

        public async Task PublishCommandAsync(string topic, string payload, bool retain = false)
        {
            if (_managedMqttClient == null)
            {
                throw new InvalidOperationException("MQTT client is not started.");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag(retain)
                .Build();

            await _managedMqttClient.EnqueueAsync(message);
            _logger.LogInformation("Published to topic '{Topic}': {Payload}", topic, payload);
        }

        public async Task StopAsync()
        {
            if (_managedMqttClient != null)
            {
                _logger.LogInformation("Stopping Managed MQTT client.");
                await _managedMqttClient.StopAsync();
                _managedMqttClient.Dispose();
            }
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            _logger.LogInformation("Successfully connected to MQTT broker at {Address}:{Port}", _config.Address, _config.Port);
            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            _logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}. Will try to reconnect.", arg.Reason);
            return Task.CompletedTask;
        }

        private async Task OnMqttMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("Received message on topic '{Topic}': {Payload}", topic, payload);

            if (_messageHandler != null)
            {
                await _messageHandler(topic, payload);
            }
        }
    }
}
