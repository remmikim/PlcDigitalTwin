using System.Threading.Tasks;

namespace Nona.Services
{
    /// <summary>
    /// PLC 통신 서비스의 기능을 정의하는 인터페이스입니다.
    /// 모든 상호작용은 비동기적으로 처리되어 UI 응답성을 보장합니다.
    /// </summary>
    public interface IPlcCommunicationService
    {
        /// <summary>
        /// PLC에 연결되었는지 여부를 나타내는 속성입니다.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// PLC와의 연결을 시작합니다.
        /// </summary>
        /// <param name="stationNumber">연결할 PLC의 스테이션 번호입니다.</param>
        /// <returns>연결 성공 시 true, 실패 시 false를 반환하는 Task.</returns>
        Task<bool> ConnectAsync(int stationNumber);

        /// <summary>
        /// PLC와의 연결을 종료합니다.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// PLC 디바이스 메모리에서 데이터를 읽습니다.
        /// </summary>
        /// <param name="device">읽을 디바이스 주소 (예: "D1000").</param>
        /// <param name="size">읽을 워드(word) 개수.</param>
        /// <returns>읽어온 데이터 (short 배열)를 포함하는 Task.</returns>
        Task<short[]> ReadDeviceBlockAsync(string device, int size);

        /// <summary>
        /// PLC 디바이스 메모리에 데이터를 씁니다.
        /// </summary>
        /// <param name="device">쓸 디바이스 주소 (예: "D2000").</param>
        /// <param name="data">쓸 데이터 (short 배열).</param>
        /// <returns>쓰기 성공 시 true, 실패 시 false를 반환하는 Task.</returns>
        Task<bool> WriteDeviceBlockAsync(string device, short[] data);
    }
}
