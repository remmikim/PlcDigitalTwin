/*
 * Server.Core/Services/IFirebaseService.cs
 * Firebase와의 통신을 담당하는 서비스의 인터페이스입니다.
 */
namespace Server.Core.Services
{
    /// <summary>
    /// Firebase Realtime Database와의 데이터 통신 및 명령 리스닝을 관리하는 서비스의 계약을 정의합니다.
    /// </summary>
    public interface IFirebaseService
    {
        /// <summary>
        /// Firebase 서비스를 초기화하고 명령 리스너를 설정합니다.
        /// </summary>
        /// <param name="commandHandler">Firebase에서 감지된 명령을 처리할 콜백 함수입니다.</param>
        void Initialize(Func<string, object, Task> commandHandler);

        /// <summary>
        /// 처리된 PLC 데이터를 Firebase에 비동기적으로 기록합니다.
        /// </summary>
        /// <param name="updates">업데이트할 경로와 데이터를 포함하는 딕셔너리입니다.</param>
        Task WriteDataAsync(Dictionary<string, object> updates);
    }
}
