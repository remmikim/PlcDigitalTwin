using MQTTnet;
using MQTTnet.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Hermes.Services
{
    public class MqttClientService : IMqttClientService, IDisposable
    {
        private readonly IMqttClient _mqttClient;
        public bool IsConnected => _mqttClient.IsConnected;

        public Action<MqttApplicationMessageReceivedEventArgs>? MessageReceivedHandler { get; set; }

        public MqttClientService()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttClient.ConnectedAsync += e =>
            {
                Debug.WriteLine("### MQTT: CONNECTED WITH SERVER ###");
                return Task.CompletedTask;
            };

            _mqttClient.DisconnectedAsync += e =>
            {
                Debug.WriteLine("### MQTT: DISCONNECTED FROM SERVER ###");
                return Task.CompletedTask;
            };

            // [추가] 메시지 수신 이벤트 핸들러
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                MessageReceivedHandler?.Invoke(e);
                return Task.CompletedTask;
            };
        }

        public async Task<bool> ConnectAsync(string address, int port, string lwtTopic)
        {
            var lwtPayload = "{\"state\": \"offline\"}";

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(address, port)
                .WithClientId($"hermes-transmitter-{Guid.NewGuid()}")
                .WithCleanSession()
                .WithWillTopic(lwtTopic)
                .WithWillPayload(lwtPayload)
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            try
            {
                var result = await _mqttClient.ConnectAsync(options, CancellationToken.None);
                return result.ResultCode == MqttClientConnectResultCode.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (!_mqttClient.IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(retain)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        /// <summary>
        /// [추가] 토픽 구독 메서드
        /// </summary>
        public async Task SubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected) return;

            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            Debug.WriteLine($"### MQTT: SUBSCRIBED TO TOPIC: {topic} ###");
        }

        public void Dispose()
        {
            _mqttClient?.Dispose();
        }
    }
}
