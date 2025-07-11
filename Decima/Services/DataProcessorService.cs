/*
 * Decima/Services/DataProcessorService.cs
 * IDataProcessorService 인터페이스의 실제 구현체입니다.
 * MQTT 메시지를 파싱하고 Firestore에 쓸 데이터를 준비합니다.
 */
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Decima.Services
{
    public class DataProcessorService : IDataProcessorService
    {
        private readonly ILogger<DataProcessorService> _logger;
        private readonly IFirebaseService _firebaseService;

        public DataProcessorService(ILogger<DataProcessorService> logger, IFirebaseService firebaseService)
        {
            _logger = logger;
            _firebaseService = firebaseService;
        }

        public async Task ProcessMqttMessageAsync(string topic, string payload)
        {
            try
            {
                var topicParts = topic.Split('/');
                if (topicParts.Length < 2)
                {
                    _logger.LogWarning("Received message with invalid topic format: {Topic}", topic);
                    return;
                }

                string messageType = topicParts[0];

                // 스위치 표현식을 사용하여 메시지 타입에 따라 분기합니다.
                var updates = messageType switch
                {
                    "dt" => ProcessTelemetryData(topicParts, payload),
                    // "status" 등 다른 타입의 메시지도 이곳에 추가할 수 있습니다.
                    _ => null
                };

                if (updates != null && updates.Count > 0)
                {
                    await _firebaseService.WriteDataAsync(updates);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message for topic {Topic}", topic);
            }
        }

        /// <summary>
        /// 텔레메트리 데이터(dt/...)를 처리합니다.
        /// </summary>
        private Dictionary<string, object>? ProcessTelemetryData(string[] topicParts, string payload)
        {
            // 토픽 구조: dt/{site}/{area}/{line}/{plc_id}/{measurement}
            if (topicParts.Length != 6)
            {
                _logger.LogWarning("Invalid telemetry topic structure: {Topic}", string.Join("/", topicParts));
                return null;
            }

            var plcId = topicParts[4];
            var deviceAddress = topicParts[5];

            try
            {
                // =================================================================
                // [수정] 페이로드 전체를 객체로 변환하여 모든 정보를 보존합니다.
                // =================================================================
                var deviceData = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
                if (deviceData == null)
                {
                    _logger.LogWarning("Failed to deserialize payload: {Payload}", payload);
                    return null;
                }

                // =================================================================
                // [수정] Firestore에 저장할 데이터를 재구성합니다.
                // 점(.) 표기법을 사용하여 문서 내의 특정 필드를 안전하게 업데이트합니다.
                // =================================================================
                var firestoreUpdate = new Dictionary<string, object>
                {
                    // 예: "devices.Y0" 라는 키를 사용하여 devices 맵 안의 Y0 필드를 업데이트합니다.
                    // 이렇게 하면 다른 디바이스(X0)의 데이터를 덮어쓰지 않습니다.
                    { $"devices.{deviceAddress}", deviceData },
                    { "last_update", DateTime.UtcNow },
                    { "plc_id", plcId } // 문서 자체에 plc_id 필드도 추가합니다.
                };

                // Firestore 업데이트 경로 및 데이터 구성
                // Key는 업데이트할 문서의 경로입니다. (예: "plc_live_data/plc-001")
                var updates = new Dictionary<string, object>
                {
                    { $"plc_live_data/{plcId}", firestoreUpdate }
                };

                return updates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse telemetry payload: {Payload}", payload);
                return null;
            }
        }
    }
}
