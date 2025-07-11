/*
 * Decima/Services/IFirebaseService.cs
 * Cloud Firestore와의 통신을 담당하는 서비스의 인터페이스입니다. (버전 업데이트)
 */
namespace Decima.Services
{
    /// <summary>
    /// Cloud Firestore와의 데이터 통신 및 명령 리스닝을 관리하는 서비스의 계약을 정의합니다.
    /// </summary>
    public interface IFirebaseService
    {
        /// <summary>
        /// Firestore 서비스를 초기화하고 명령 리스너를 설정합니다.
        /// </summary>
        /// <param name="commandHandler">Firestore에서 감지된 명령을 처리할 콜백 함수입니다. 
        /// 첫 번째 파라미터는 명령 문서 ID, 두 번째는 명령 데이터입니다.</param>
        void Initialize(Func<string, Dictionary<string, object>, Task> commandHandler);

        /// <summary>
        /// 처리된 PLC 데이터를 Firestore에 비동기적으로 기록합니다.
        /// 키는 "컬렉션/문서ID" 형식이어야 합니다. (예: "plc_live_data/plc-001")
        /// </summary>
        /// <param name="updates">업데이트할 경로와 데이터를 포함하는 딕셔너리입니다.</param>
        Task WriteDataAsync(Dictionary<string, object> updates);
    }
}
