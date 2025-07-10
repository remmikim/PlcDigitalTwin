using MQTTnet;
using MQTTnet.Client;
using System;
using System.Diagnostics;
using System.Threading;
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
                // =================================================================
                // [수정] 연결 해제 원인을 자세히 로깅하도록 변경
                // =================================================================
                string logMessage = $"### MQTT: DISCONNECTED FROM SERVER. Reason: {e.ReasonString}, WasClean: {e.ClientWasConnected} ###";
                Debug.WriteLine(logMessage);
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
                // 정상적인 연결 해제를 위해 옵션을 설정합니다.
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
            // Dispose 시에 연결을 비동기적으로 끊으려고 시도합니다.
            // 하지만 App.OnExit과 같은 동기 컨텍스트에서는 문제가 될 수 있으므로
            // Task.Run을 사용하여 별도 스레드에서 실행하거나,
            // 혹은 DisconnectAsync를 미리 호출하도록 유도하는 것이 좋습니다.
            if (_mqttClient.IsConnected)
            {
                try
                {
                    // 애플리케이션 종료 시에는 동기적으로 기다리는 것이 더 안정적일 수 있습니다.
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
