/*
 * Server.Core/Services/DataProcessorService.cs
 * IDataProcessorService 인터페이스의 실제 구현체입니다.
 * MQTT 메시지를 파싱하고 Firestore에 쓸 데이터를 준비합니다.
 */
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Core.Services
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
                    "status" => ProcessStatusData(topicParts, payload),
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
            if (topicParts.Length != 6) return null;

            var plcId = topicParts[4];
            var measurement = topicParts[5];

            try
            {
                var data = JObject.Parse(payload);
                var value = data["value"];
                if (value == null) return null;

                // Firestore에 저장할 데이터 객체 생성
                var firestoreUpdate = new Dictionary<string, object>
                {
                    { measurement, value.ToObject<object>()! }, // 측정 항목을 필드로 사용
                    { "last_update", DateTime.UtcNow }           // 마지막 업데이트 타임스탬프
                };

                // Firestore 업데이트 경로 및 데이터 구성
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

        /// <summary>
        /// 상태 데이터(status/...)를 처리합니다.
        /// </summary>
        private Dictionary<string, object>? ProcessStatusData(string[] topicParts, string payload)
        {
            // 토픽 구조: status/{site}/{area}/{line}/{transmitter_id}/connection
            if (topicParts.Length != 6 || topicParts[5] != "connection") return null;

            var transmitterId = topicParts[4];

            try
            {
                var data = JObject.Parse(payload);
                var state = data["state"]?.ToString();
                if (state == null) return null;

                var firestoreUpdate = new Dictionary<string, object>
                {
                    { "state", state },
                    { "last_seen", DateTime.UtcNow }
                };

                var updates = new Dictionary<string, object>
                {
                    { $"transmitter_status/{transmitterId}", firestoreUpdate }
                };

                return updates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse status payload: {Payload}", payload);
                return null;
            }
        }
    }
}
