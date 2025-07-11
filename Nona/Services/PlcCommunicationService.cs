using ActUtlType64Lib; //미쓰비시 라이브러리
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Services
{
    /// <summary>
    /// ActUtlType64.dll COM 컴포넌트와 안정적으로 통신하기 위한 서비스 클래스.
    /// 모든 PLC 통신은 전용 STA 스레드에서 직렬화되어 처리됩니다.
    /// </summary>
    public class PlcCommunicationService : IPlcCommunicationService, IDisposable
    {
        // ActUtlType64 COM 객체 인스턴스. STA 스레드에서만 접근해야 합니다.
        private ActUtlType64Class? _actUtlType;

        // PLC 통신을 전담하는 STA(Single-Threaded Apartment) 스레드.
        private readonly Thread _plcThread;

        // 다른 스레드로부터의 작업 요청을 저장하는 스레드 안전 큐.
        // 각 작업은 TaskCompletionSource를 통해 비동기적으로 결과를 반환합니다.
        private readonly BlockingCollection<Func<Task>> _workQueue = new BlockingCollection<Func<Task>>();

        // 서비스 종료 시 스레드를 안전하게 중지시키기 위한 CancellationTokenSource.
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private volatile bool _isConnected = false;
        public bool IsConnected => _isConnected;

        public PlcCommunicationService()
        {
            _plcThread = new Thread(PlcProcessingLoop)
            {
                IsBackground = true,
                Name = "PlcStaThread"
            };
            _plcThread.SetApartmentState(ApartmentState.STA);
            _plcThread.Start();
        }

        private async void PlcProcessingLoop()
        {
            _actUtlType = new ActUtlType64Class();

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var workItem = _workQueue.Take(_cancellationTokenSource.Token);
                    await workItem();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("PlcProcessingLoop was cancelled.");
            }
            finally
            {
                if (_actUtlType != null)
                {
                    if (_actUtlType.Close() == 0)
                    {
                        Debug.WriteLine("PLC connection closed on exit.");
                    }
                    _actUtlType = null;
                }
                Debug.WriteLine("PlcProcessingLoop finished.");
            }
        }

        private Task<T> EnqueueTask<T>(Func<T> work)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            _workQueue.Add(async () =>
            {
                try
                {
                    var result = work();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                await Task.CompletedTask;
            });

            return tcs.Task;
        }

        private Task EnqueueTask(Action work)
        {
            return EnqueueTask(() => {
                work();
                return true; // 더미 반환값
            });
        }

        // --- IPlcCommunicationService 인터페이스 구현 ---

        public Task<bool> ConnectAsync(int stationNumber)
        {
            return EnqueueTask(() =>
            {
                if (_actUtlType == null) return false;
                if (_isConnected) return true;

                _actUtlType.ActLogicalStationNumber = stationNumber;
                int result = _actUtlType.Open();

                _isConnected = (result == 0);
                Debug.WriteLine($"PLC Connect attempt result: {result}. IsConnected: {_isConnected}");
                return _isConnected;
            });
        }

        public Task DisconnectAsync()
        {
            return EnqueueTask(() =>
            {
                if (_actUtlType == null || !_isConnected) return;

                int result = _actUtlType.Close();
                _isConnected = false;
                Debug.WriteLine($"PLC Disconnect attempt result: {result}. IsConnected: {_isConnected}");
            });
        }

        /// <summary>
        /// [구현 완료] PLC 디바이스 메모리에서 데이터를 읽습니다.
        /// </summary>
        public Task<short[]> ReadDeviceBlockAsync(string device, int size)
        {
            return EnqueueTask(() =>
            {
                if (_actUtlType == null || !_isConnected)
                {
                    throw new InvalidOperationException("PLC is not connected.");
                }

                var data = new short[size];
                int result = _actUtlType.ReadDeviceBlock2(device, size, out data[0]);

                if (result == 0)
                {
                    return data;
                }
                else
                {
                    // 오류 코드를 포함하는 예외를 발생시켜 호출자에게 상세 정보를 전달합니다.
                    throw new Exception($"Failed to read device block. PLC Error Code: 0x{result:X}");
                }
            });
        }

        /// <summary>
        /// [구현 완료] PLC 디바이스 메모리에 데이터를 씁니다.
        /// </summary>
        public Task<bool> WriteDeviceBlockAsync(string device, short[] data)
        {
            return EnqueueTask(() =>
            {
                if (_actUtlType == null || !_isConnected)
                {
                    throw new InvalidOperationException("PLC is not connected.");
                }

                int size = data.Length;
                int result = _actUtlType.WriteDeviceBlock2(device, size, ref data[0]);

                return result == 0;
            });
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _plcThread.Join(1000);
            _workQueue.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}
