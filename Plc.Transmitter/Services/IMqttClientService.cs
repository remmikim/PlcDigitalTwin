using MQTTnet.Client;
using System;
using System.Threading.Tasks;

namespace Hermes.Services
{
    /// <summary>
    /// MQTT 클라이언트 서비스의 기능을 정의하는 인터페이스입니다.
    /// </summary>
    public interface IMqttClientService
    {
        bool IsConnected { get; }

        /// <summary>
        /// 메시지 수신 시 호출될 콜백 액션입니다.
        /// </summary>
        Action<MqttApplicationMessageReceivedEventArgs>? MessageReceivedHandler { get; set; }

        /// <summary>
        /// MQTT 브로커에 비동기적으로 연결합니다.
        /// </summary>
        /// <param name="address">브로커 주소</param>
        /// <param name="port">브로커 포트</param>
        /// <param name="lwtTopic">LWT(Last Will and Testament) 토픽</param>
        /// <param name="username">사용자 이름</param>
        /// <param name="password">비밀번호</param>
        Task<bool> ConnectAsync(string address, int port, string lwtTopic, string username, string password);

        /// <summary>
        /// MQTT 브로커와의 연결을 비동기적으로 해제합니다.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 지정된 토픽으로 메시지를 발행합니다.
        /// </summary>
        /// <param name="topic">발행할 토픽</param>
        /// <param name="payload">전송할 데이터</param>
        /// <param name="retain">Retain 플래그 설정 여부</param>
        Task PublishAsync(string topic, string payload, bool retain = false);

        /// <summary>
        /// 지정된 토픽을 구독합니다.
        /// </summary>
        /// <param name="topic">구독할 토픽</param>
        Task SubscribeAsync(string topic);
    }
}
