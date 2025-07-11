/*
 * Decima/Services/IMqttService.cs
 * MQTT 브로커와의 통신을 담당하는 서비스의 인터페이스입니다.
 */
namespace Decima.Services
{
    // <summary>
    // MQTT 브로커와의 연결, 메시지 발행 및 구독을 관리하는 서비스의 계약을 정의합니다.
    // </summary>
    public interface IMqttService
    {
        // <summary>
        // MQTT 브로커에 비동기적으로 연결하고 구독을 시작합니다.
        // </summary>
        // <param name="messageHandler">수신된 메시지를 처리할 콜백 함수입니다.</param>
        // <param name="cancellationToken">취소 토큰입니다.</param>
        Task StartAsync(Func<string,string,Task> messageHandler, CancellationToken cancellationToken);

        // <summary>
        // 지정된 토픽으로 명령 메시지를 발행합니다.
        // </summary>
        // <param name="topic">메시지를 발행할 토픽입니다.</param>
        // <param name="payload">전송할 데이터입니다.</param>
        // <param name="retain">Retain 플래그 설정 여부입니다.</param>
        Task PublishCommandAsync(string topic, string payload, bool retain = false);

        // <summary>
        // 서비스의 연결을 비동기적으로 중지합니다.
        // </summary>
        Task StopAsync();
    }
}
