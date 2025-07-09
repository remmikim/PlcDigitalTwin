using MQTTnet;
using MQTTnet.Client;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Hermes.Services
{
    public class MqttClientService : IMqttClientService, IDisposable
    {
        private readonly IMqttClient _mqttClient;
        public bool IsConnected => _mqttClient.IsConnected;

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
        }

        public async Task<bool> ConnectAsync(string address, int port, string lwtTopic)
        {
            var lwtPayload = "{\"state\": \"offline\"}";

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(address, port)
                .WithClientId($"hermes-transmitter-{Guid.NewGuid()}")
                .WithCleanSession()
                // [수정] 제안해주신 대로 WithWillMessage 대신 개별 속성으로 LWT를 설정합니다.
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
            if (!_mqttClient.IsConnected)
            {
                // 연결되어 있지 않으면 발행하지 않음
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1
                .WithRetainFlag(retain)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        public void Dispose()
        {
            _mqttClient?.Dispose();
        }
    }
}
