using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nona.Services
{
    /// <summary>
    /// IManagedMqttClient를 사용하여 안정적인 MQTT 연결을 관리하는 서비스 구현체입니다.
    /// 자동 재연결, 메시지 큐잉 등 강력한 기능을 제공하여 전송 실패를 최소화합니다.
    /// </summary>
    public class MqttClientService : IMqttClientService
    {
        private IManagedMqttClient? _managedMqttClient;
        private bool _isConnected;

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                }
            }
        }

        public event Action<string, string>? LogMessageGenerated;

        public MqttClientService()
        {
            // ManagedMqttClient는 MQTTnet 라이브러리에서 제공하는 고수준 클라이언트입니다.
            var factory = new MqttFactory();
            _managedMqttClient = factory.CreateManagedMqttClient();

            // 연결 상태 변경 이벤트 핸들러 등록
            _managedMqttClient.ConnectedAsync += OnConnectedAsync;
            _managedMqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _managedMqttClient.ApplicationMessageProcessedAsync += OnApplicationMessageProcessedAsync;
        }

        public async Task StartAsync(string address, int port, string username, string password, string lwtTopic)
        {
            if (_managedMqttClient == null) return;

            var lwtPayload = "{\"state\": \"offline\"}";

            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(address, port)
                .WithClientId($"Nona-Client-{Guid.NewGuid()}")
                .WithCleanSession()
                .WithWillTopic(lwtTopic)
                .WithWillPayload(lwtPayload)
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            if (!string.IsNullOrEmpty(username))
            {
                clientOptionsBuilder.WithCredentials(username, password);
            }

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptionsBuilder.Build())
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5)) // 5초마다 재연결 시도
                .Build();

            // Managed Client 시작. 이제부터 백그라운드에서 연결을 관리합니다.
            await _managedMqttClient.StartAsync(managedOptions);
            RaiseLog("Info", $"MQTT 서비스 시작. 브로커 연결 시도 중... ({address}:{port})");
        }

        public async Task StopAsync()
        {
            if (_managedMqttClient != null && _managedMqttClient.IsStarted)
            {
                await _managedMqttClient.StopAsync();
                RaiseLog("Info", "MQTT 서비스가 정상적으로 중지되었습니다.");
            }
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (_managedMqttClient == null || !_managedMqttClient.IsStarted)
            {
                RaiseLog("Warning", "MQTT 서비스가 시작되지 않아 메시지를 발행할 수 없습니다.");
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(retain)
                .Build();

            // 메시지를 내부 큐에 추가합니다. Managed Client가 알아서 전송을 처리합니다.
            // 연결이 끊겨도 큐에 저장되었다가 재연결 시 전송됩니다.
            await _managedMqttClient.EnqueueAsync(message);
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            IsConnected = true;
            RaiseLog("Success", "MQTT 브로커에 성공적으로 연결되었습니다.");
            // LWT와 반대되는 온라인 상태 메시지 발행
            _ = PublishAsync("status/factory-01/assembly/line-a/transmitter-01/connection", "{\"state\": \"online\"}", true);
            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            IsConnected = false;
            RaiseLog("Warning", $"MQTT 브로커 연결 끊어짐. 5초 후 재연결을 시도합니다. 사유: {arg.Reason}");
            return Task.CompletedTask;
        }

        // 메시지가 성공적으로 처리(발행)되었을 때 호출되는 이벤트
        private Task OnApplicationMessageProcessedAsync(ApplicationMessageProcessedEventArgs arg)
        {
            if (arg.Exception != null)
            {
                RaiseLog("Error", $"메시지 발행 실패: {arg.ApplicationMessage.ApplicationMessage.Topic}. 오류: {arg.Exception.Message}");
            }
            // 성공 로그는 너무 많을 수 있으므로 오류 발생 시에만 기록
            return Task.CompletedTask;
        }

        private void RaiseLog(string level, string message)
        {
            LogMessageGenerated?.Invoke(level, message);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _managedMqttClient?.Dispose();
        }
    }
}
