using MQTTnet;
using MQTTnet.Client;
using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Services
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
                string logMessage = $"### MQTT: DISCONNECTED FROM SERVER. Reason: {e.ReasonString}, WasClean: {e.ClientWasConnected} ###";
                Debug.WriteLine(logMessage);
                return Task.CompletedTask;
            };

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                MessageReceivedHandler?.Invoke(e);
                return Task.CompletedTask;
            };
        }

        // [수정] ConnectAsync 메서드에 username, password 파라미터 추가
        public async Task<bool> ConnectAsync(string address, int port, string lwtTopic, string username, string password)
        {
            var lwtPayload = "{\"state\": \"offline\"}";

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(address, port)
                .WithClientId($"site-{Guid.NewGuid()}")
                .WithCleanSession()
                .WithWillTopic(lwtTopic)
                .WithWillPayload(lwtPayload)
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            // 사용자 이름이 설정된 경우에만 인증 정보 추가
            if (!string.IsNullOrEmpty(username))
            {
                optionsBuilder.WithCredentials(username, password);
            }

            var options = optionsBuilder.Build();

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
                var options = new MqttClientDisconnectOptions
                {
                    Reason = MqttClientDisconnectOptionsReason.NormalDisconnection,
                    ReasonString = "User requested disconnect."
                };
                await _mqttClient.DisconnectAsync(options, CancellationToken.None);
            }
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (!_mqttClient.IsConnected)
            {
                // [수정] 예외를 던져서 호출자가 문제를 인지하게 함
                throw new InvalidOperationException("The MQTT client is disconnected.");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(retain)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected)
            {
                // [수정] 예외를 던져서 호출자가 문제를 인지하게 함
                throw new InvalidOperationException("The MQTT client is disconnected.");
            }

            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            Debug.WriteLine($"### MQTT: SUBSCRIBED TO TOPIC: {topic} ###");
        }

        public void Dispose()
        {
            if (_mqttClient.IsConnected)
            {
                try
                {
                    _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during dispose/disconnect: {ex.Message}");
                }
            }
            _mqttClient?.Dispose();
        }
    }
}
