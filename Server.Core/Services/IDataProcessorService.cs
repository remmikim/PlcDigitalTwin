/*
 * Server.Core/Services/IDataProcessorService.cs
 * 수신된 MQTT 메시지를 처리하는 서비스의 인터페이스입니다.
 */
namespace Server.Core.Services
{
    /// <summary>
    /// 수신된 MQTT 메시지를 파싱, 처리하고 다른 서비스로 전달하는 로직을 담당하는 서비스의 계약을 정의합니다.
    /// </summary>
    public interface IDataProcessorService
    {
        /// <summary>
        /// 수신된 MQTT 메시지를 비동기적으로 처리합니다.
        /// </summary>
        /// <param name="topic">메시지가 수신된 토픽입니다.</param>
        /// <param name="payload">수신된 데이터입니다.</param>
        Task ProcessMqttMessageAsync(string topic, string payload);
    }
}
