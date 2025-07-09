/*
 * Server.Core/Services/ServerWorker.cs
 * 애플리케이션의 모든 서비스를 조정하는 메인 워커 서비스입니다.
 */
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Core.Services
{
    public class ServerWorker : BackgroundService
    {
        private readonly ILogger<ServerWorker> _logger;
        private readonly IMqttService _mqttService;
        private readonly IFirebaseService _firebaseService;
        private readonly IDataProcessorService _dataProcessor;

        public ServerWorker(
            ILogger<ServerWorker> logger,
            IMqttService mqttService,
            IFirebaseService firebaseService,
            IDataProcessorService dataProcessor)
        {
            _logger = logger;
            _mqttService = mqttService;
            _firebaseService = firebaseService;
            _dataProcessor = dataProcessor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ServerWorker starting at: {time}", System.DateTimeOffset.Now);

            // Firebase 서비스를 초기화하고, 명령 수신 시 호출될 핸들러를 등록합니다.
            _firebaseService.Initialize(HandleFirebaseCommandAsync);

            // MQTT 서비스를 시작하고, 메시지 수신 시 DataProcessor를 호출하도록 설정합니다.
            await _mqttService.StartAsync(_dataProcessor.ProcessMqttMessageAsync, stoppingToken);

            // 애플리케이션이 종료될 때까지 대기합니다.
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ServerWorker stopping.");
            await _mqttService.StopAsync();
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Firestore에서 새로운 명령이 감지되었을 때 호출되는 콜백 메서드입니다.
        /// </summary>
        private async Task HandleFirebaseCommandAsync(string commandId, Dictionary<string, object> commandData)
        {
            _logger.LogInformation("Handling command from Firebase: {CommandId}", commandId);
            try
            {
                // 필수 필드 확인
                if (!commandData.TryGetValue("target_plc", out var targetPlcObj) ||
                    !commandData.TryGetValue("device", out var deviceObj) ||
                    !commandData.TryGetValue("value", out var valueObj))
                {
                    _logger.LogWarning("Command {CommandId} is missing required fields.", commandId);
                    return;
                }

                string targetPlc = targetPlcObj.ToString()!;
                string device = deviceObj.ToString()!;

                // 명령 토픽 생성 (예: cmd/factory-01/assembly/line-a/plc-001/write)
                // 이 예제에서는 토픽 구조를 단순화합니다. 실제 환경에서는 plc_metadata를 참조하여 전체 토픽을 구성해야 합니다.
                string commandTopic = $"cmd/factory-01/assembly/line-a/{targetPlc}/write/{device}";

                // 페이로드 생성
                var payload = new { value = valueObj };
                string jsonPayload = JsonConvert.SerializeObject(payload);

                // MQTT로 명령 발행
                await _mqttService.PublishCommandAsync(commandTopic, jsonPayload);

                // TODO: 성공적으로 처리된 명령을 command_queue에서 삭제하거나 상태를 'processed'로 업데이트하는 로직 추가
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error handling Firebase command {CommandId}", commandId);
            }
        }
    }
}
