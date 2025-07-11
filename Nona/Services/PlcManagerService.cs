using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Nona.Services
{
    /// <summary>
    /// 여러 PLC 연결을 관리하는 중앙 서비스입니다.
    /// 스테이션 번호를 키로 사용하여 각 PLC 통신 서비스를 관리합니다.
    /// </summary>
    public class PlcManagerService : IDisposable
    {
        private readonly ConcurrentDictionary<int, IPlcCommunicationService> _plcServices = new();

        /// <summary>
        /// 특정 스테이션 번호의 PLC에 연결합니다.
        /// </summary>
        public Task<bool> ConnectAsync(int stationNumber)
        {
            var service = _plcServices.GetOrAdd(stationNumber, _ => new PlcCommunicationService());
            return service.ConnectAsync(stationNumber);
        }

        /// <summary>
        /// 특정 스테이션 번호의 PLC 연결을 해제합니다.
        /// </summary>
        public Task DisconnectAsync(int stationNumber)
        {
            if (_plcServices.TryGetValue(stationNumber, out var service))
            {
                return service.DisconnectAsync();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 특정 스테이션의 디바이스 블록을 읽습니다.
        /// </summary>
        public Task<short[]> ReadDeviceBlockAsync(int stationNumber, string device, int size)
        {
            if (_plcServices.TryGetValue(stationNumber, out var service))
            {
                return service.ReadDeviceBlockAsync(device, size);
            }
            throw new InvalidOperationException($"Station {stationNumber} is not connected or managed.");
        }

        /// <summary>
        /// 특정 스테이션의 디바이스 블록에 씁니다.
        /// </summary>
        public Task<bool> WriteDeviceBlockAsync(int stationNumber, string device, short[] data)
        {
            if (_plcServices.TryGetValue(stationNumber, out var service))
            {
                return service.WriteDeviceBlockAsync(device, data);
            }
            throw new InvalidOperationException($"Station {stationNumber} is not connected or managed.");
        }

        /// <summary>
        /// 관리되는 모든 PLC 통신 서비스를 정리합니다.
        /// </summary>
        public void Dispose()
        {
            foreach (var service in _plcServices.Values)
            {
                (service as IDisposable)?.Dispose();
            }
            _plcServices.Clear();
        }
    }
}
