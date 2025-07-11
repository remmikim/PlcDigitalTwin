using System;
using System.Threading.Tasks;

namespace Nona.Services
{
    /// <summary>
    /// MQTT 클라이언트 서비스의 기능을 정의하는 인터페이스입니다.
    /// 자동 재연결을 포함한 안정적인 백그라운드 연결 관리를 목표로 합니다.
    /// </summary>
    public interface IMqttClientService : IDisposable
    {
        /// <summary>
        /// MQTT 브로커에 연결되었는지 여부를 나타냅니다.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// MQTT 서비스의 상태(연결, 재연결 시도 등)에 대한 로그 메시지가 발생할 때 호출됩니다.
        /// </summary>
        event Action<string, string> LogMessageGenerated;

        /// <summary>
        /// MQTT 클라이언트 서비스를 시작하고 백그라운드에서 브로커 연결을 시도합니다.
        /// 연결이 끊어지면 자동으로 재연결을 시도합니다.
        /// </summary>
        /// <param name="address">브로커 주소</param>
        /// <param name="port">브로커 포트</param>
        /// <param name="username">사용자 이름</param>
        /// <param name="password">비밀번호</param>
        /// <param name="lwtTopic">LWT(Last Will and Testament) 토픽</param>
        Task StartAsync(string address, int port, string username, string password, string lwtTopic);

        /// <summary>
        /// MQTT 서비스의 연결을 정상적으로 중지하고 리소스를 해제합니다.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 지정된 토픽으로 메시지를 발행합니다.
        /// 클라이언트가 일시적으로 연결이 끊긴 경우, 메시지는 큐에 저장되었다가 재연결 시 전송됩니다.
        /// </summary>
        /// <param name="topic">발행할 토픽</param>
        /// <param name="payload">전송할 데이터</param>
        /// <param name="retain">Retain 플래그 설정 여부</param>
        Task PublishAsync(string topic, string payload, bool retain = false);
    }
}
