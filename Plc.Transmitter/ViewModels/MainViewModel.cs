using Hermes.Helpers;
using Hermes.Models;
using Hermes.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Hermes.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IPlcCommunicationService _plcService;

        private CancellationTokenSource? _pollingCts;

        #region Properties

        private bool _isPlcConnected;
        public bool IsPlcConnected
        {
            get => _isPlcConnected;
            set
            {
                _isPlcConnected = value;
                OnPropertyChanged();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((RelayCommand)ConnectPlcCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DisconnectPlcCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)WritePlcValueCommand).RaiseCanExecuteChanged();
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

        public ObservableCollection<PlcDataItem> PlcDataItems { get; } = new ObservableCollection<PlcDataItem>();

        private string _writeAddress = "D０";
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

            LoadDeviceMap();
            AddLog("Info", "애플리케이션이 시작되었습니다.");
        }

        /// <summary>
        /// [수정] device_map.json 파일에서 모니터링할 디바이스 목록을 로드합니다.
        /// </summary>
        private void LoadDeviceMap()
        {
            try
            {
                string filePath = "device_map.json";
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var items = JsonConvert.DeserializeObject<List<PlcDataItem>>(json);
                    PlcDataItems.Clear();
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            PlcDataItems.Add(item);
                        }
                    }
                    AddLog("Info", $"{PlcDataItems.Count}개의 디바이스를 설정 파일에서 로드했습니다.");
                }
                else
                {
                    AddLog("Warning", "device_map.json 설정 파일을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog("Error", $"디바이스 설정 파일 로드 실패: {ex.Message}");
            }
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
                    StartPolling();
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
                StopPolling();
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
                    // 이 경우는 거의 발생하지 않음. 보통 예외가 발생함.
                    AddLog("Error", "PLC 쓰기에 실패했습니다. 반환값이 false입니다.");
                }
            }
            catch (Exception ex)
            {
                // [수정] 더 상세한 오류 로깅
                AddLog("Error", $"PLC 쓰기 중 예외 발생: {ex.Message}. PLC 설정을 확인하세요.");
                Debug.WriteLine($"[WRITE ERROR] {ex}");
            }
        }

        private void StartPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts = new CancellationTokenSource();

            Task.Run(() => PollingLoopAsync(_pollingCts.Token), _pollingCts.Token);
            AddLog("Info", "PLC 데이터 폴링을 시작합니다.");
        }

        private void StopPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts = null;
            AddLog("Info", "PLC 데이터 폴링을 중지합니다.");
        }

        private async Task PollingLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var item in PlcDataItems)
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            var data = await _plcService.ReadDeviceBlockAsync(item.DeviceAddress, 1);
                            Application.Current.Dispatcher.Invoke(() => item.CurrentValue = data[0]);
                        }
                        catch (Exception ex)
                        {
                            AddLog("Error", $"'{item.DeviceAddress}' 읽기 실패: {ex.Message}");
                        }
                    }

                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog("Critical", $"폴링 루프에서 심각한 오류 발생: {ex.Message}");
                    await ExecuteDisconnectPlcAsync();
                    break;
                }
            }
            Debug.WriteLine("PollingLoopAsync finished.");
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
