using Hermes.Helpers;
using Hermes.Models;
using Hermes.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Hermes.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IPlcCommunicationService _plcService;
        private Timer? _simulationTimer;

        #region Properties

        private bool _isPlcConnected;
        public bool IsPlcConnected
        {
            get => _isPlcConnected;
            set
            {
                _isPlcConnected = value;
                OnPropertyChanged();
                // UI 스레드에서 커맨드 상태 갱신
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((RelayCommand)ConnectPlcCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DisconnectPlcCommand).RaiseCanExecuteChanged();
                });
            }
        }

        private bool _isMqttConnected;
        public bool IsMqttConnected
        {
            get => _isMqttConnected;
            set
            {
                _isMqttConnected = value;
                OnPropertyChanged();
            }
        }

        public AppConfig Config { get; } = new AppConfig();
        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        // PLC 데이터 모니터링을 위한 컬렉션
        public ObservableCollection<PlcDataItem> PlcDataItems { get; } = new ObservableCollection<PlcDataItem>();

        // PLC에 값을 쓰기 위한 속성
        private string _writeAddress = "D2000";
        public string WriteAddress
        {
            get => _writeAddress;
            set { _writeAddress = value; OnPropertyChanged(); }
        }

        private short _writeValue;
        public short WriteValue
        {
            get => _writeValue;
            set { _writeValue = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands
        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand WritePlcValueCommand { get; }

        #endregion

        public MainViewModel(IPlcCommunicationService plcService)
        {
            _plcService = plcService ?? throw new ArgumentNullException(nameof(plcService));

            ConnectPlcCommand = new RelayCommand(async _ => await ExecuteConnectPlcAsync(), _ => !IsPlcConnected);
            DisconnectPlcCommand = new RelayCommand(async _ => await ExecuteDisconnectPlcAsync(), _ => IsPlcConnected);
            WritePlcValueCommand = new RelayCommand(async _ => await ExecuteWritePlcValueAsync(), _ => IsPlcConnected);

            InitializePlcDataItems();
            AddLog("Info", "애플리케이션이 시작되었습니다.");
        }

        /// <summary>
        /// 모니터링할 PLC 디바이스 목록을 초기화합니다.
        /// </summary>
        private void InitializePlcDataItems()
        {
            PlcDataItems.Add(new PlcDataItem { DeviceAddress = "D1000", Description = "컨베이어 속도" });
            PlcDataItems.Add(new PlcDataItem { DeviceAddress = "D1001", Description = "교반기 온도" });
            PlcDataItems.Add(new PlcDataItem { DeviceAddress = "M100", Description = "자동/수동 모드" });
            PlcDataItems.Add(new PlcDataItem { DeviceAddress = "Y0", Description = "메인 램프" });
        }

        private async Task ExecuteConnectPlcAsync()
        {
            AddLog("Info", $"PLC 연결 시도 (스테이션 번호: {Config.PlcStationNumber})...");
            try
            {
                bool success = await _plcService.ConnectAsync(Config.PlcStationNumber);
                IsPlcConnected = success;

                if (success)
                {
                    AddLog("Success", "PLC에 성공적으로 연결되었습니다.");
                    StartSimulation();
                }
                else
                {
                    AddLog("Error", "PLC 연결에 실패했습니다. MX Component 설정 및 PLC 상태를 확인하세요.");
                }
            }
            catch (Exception ex)
            {
                IsPlcConnected = false;
                AddLog("Error", $"PLC 연결 중 예외 발생: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        private async Task ExecuteDisconnectPlcAsync()
        {
            AddLog("Info", "PLC 연결 해제 시도...");
            try
            {
                StopSimulation();
                await _plcService.DisconnectAsync();
                IsPlcConnected = false;
                AddLog("Info", "PLC 연결이 해제되었습니다.");
            }
            catch (Exception ex)
            {
                AddLog("Error", $"PLC 연결 해제 중 예외 발생: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        private async Task ExecuteWritePlcValueAsync()
        {
            if (string.IsNullOrWhiteSpace(WriteAddress))
            {
                AddLog("Warning", "값을 쓸 PLC 디바이스 주소를 입력하세요.");
                return;
            }

            AddLog("Info", $"PLC 쓰기 시도: {WriteAddress}에 {WriteValue} 값 전송...");
            try
            {
                bool success = await _plcService.WriteDeviceBlockAsync(WriteAddress, new short[] { WriteValue });
                if (success)
                {
                    AddLog("Success", $"쓰기 성공: {WriteAddress} = {WriteValue}");
                }
                else
                {
                    AddLog("Error", "PLC 쓰기에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog("Error", $"PLC 쓰기 중 예외 발생: {ex.Message}");
            }
        }

        private void StartSimulation()
        {
            AddLog("Info", "PLC 데이터 폴링을 시작합니다.");
            // 1초마다 UpdatePlcDataAsync 메서드를 실행하는 타이머 생성
            _simulationTimer = new Timer(UpdatePlcDataAsync, null, 0, 1000);
        }

        private void StopSimulation()
        {
            AddLog("Info", "PLC 데이터 폴링을 중지합니다.");
            _simulationTimer?.Change(Timeout.Infinite, 0);
            _simulationTimer?.Dispose();
            _simulationTimer = null;
        }

        /// <summary>
        /// 타이머에 의해 주기적으로 호출되어 PLC 데이터를 읽고 UI를 업데이트합니다.
        /// </summary>
        private async void UpdatePlcDataAsync(object? state)
        {
            if (!IsPlcConnected) return;

            try
            {
                // 각 모니터링 항목의 값을 PLC에서 읽어와 업데이트
                foreach (var item in PlcDataItems)
                {
                    var data = await _plcService.ReadDeviceBlockAsync(item.DeviceAddress, 1);
                    // UI 스레드에서 속성 값 변경
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.CurrentValue = data[0];
                    });
                }
            }
            catch (Exception ex)
            {
                // 통신 중 오류 발생 시 시뮬레이션 중지 및 연결 해제
                AddLog("Critical", $"데이터 읽기 실패: {ex.Message}. 폴링을 중지합니다.");
                await ExecuteDisconnectPlcAsync();
            }
        }

        private void AddLog(string level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                });

                if (Logs.Count > 200)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            });
        }
    }
}
