/*
 * Decima/Services/MqttService.cs
 * IMqttService 인터페이스의 실제 구현체입니다.
 * MQTTnet 라이브러리를 사용하여 브로커와의 통신을 관리합니다.
 */
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using Decima.Models;
using System.Text;

namespace Decima.Services
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

            // [수정] MqttClientOptionsBuilder에 사용자 인증 정보 추가
            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Address, _config.Port)
                .WithClientId($"plc-server-core-{Guid.NewGuid()}")
                .WithCleanSession();

            // 사용자 이름이 설정된 경우에만 인증 정보 추가
            if (!string.IsNullOrEmpty(_config.Username))
            {
                clientOptionsBuilder.WithCredentials(_config.Username, _config.Password);
            }

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(clientOptionsBuilder.Build())
                .Build();

            // 구독할 토픽 설정
            var topicFilters = new List<MqttTopicFilter>
            {
                new MqttTopicFilterBuilder().WithTopic("dt/#").Build(),      // 모든 텔레메트리 데이터
                new MqttTopicFilterBuilder().WithTopic("status/#").Build() // 모든 상태 데이터
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
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce) // QoS 2
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
