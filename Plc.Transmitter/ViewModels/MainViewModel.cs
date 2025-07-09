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
        private readonly PlcManagerService _plcManager;
        private readonly IMqttClientService _mqttService;
        private CancellationTokenSource? _pollingCts;

        #region Properties

        public ObservableCollection<PlcConnection> PlcConnections { get; } = new();

        private PlcConnection? _selectedPlc;
        public PlcConnection? SelectedPlc
        {
            get => _selectedPlc;
            set
            {
                if (_selectedPlc != null && _selectedPlc.IsConnected) StopPolling();
                _selectedPlc = value;
                OnPropertyChanged();
                if (_selectedPlc != null && _selectedPlc.IsConnected) StartPolling(_selectedPlc);

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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((RelayCommand)ConnectMqttCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DisconnectMqttCommand).RaiseCanExecuteChanged();
                });
            }
        }

        public AppConfig Config { get; } = new AppConfig();

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

        public ObservableCollection<LogEntry> Logs { get; } = new();

        #endregion

        #region Commands
        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand WritePlcValueCommand { get; }
        public ICommand ConnectMqttCommand { get; }
        public ICommand DisconnectMqttCommand { get; }
        #endregion

        public MainViewModel(PlcManagerService plcManager, IMqttClientService mqttService)
        {
            _plcManager = plcManager ?? throw new ArgumentNullException(nameof(plcManager));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));

            ConnectPlcCommand = new RelayCommand(async _ => await ExecuteConnectPlcAsync(), _ => SelectedPlc != null && !SelectedPlc.IsConnected);
            DisconnectPlcCommand = new RelayCommand(async _ => await ExecuteDisconnectPlcAsync(), _ => SelectedPlc != null && SelectedPlc.IsConnected);
            WritePlcValueCommand = new RelayCommand(async _ => await ExecuteWritePlcValueAsync(), _ => SelectedPlc != null && SelectedPlc.IsConnected);

            ConnectMqttCommand = new RelayCommand(async _ => await ExecuteConnectMqttAsync(), _ => !IsMqttConnected);
            DisconnectMqttCommand = new RelayCommand(async _ => await ExecuteDisconnectMqttAsync(), _ => IsMqttConnected);

            LoadPlcStations();
            AddLog("Info", "애플리케이션이 시작되었습니다.");
        }

        // ... (PLC 관련 메서드는 이전과 동일) ...
        private void LoadPlcStations()
        {
            try
            {
                string filePath = "device_map.json";
                if (!File.Exists(filePath))
                {
                    AddLog("Warning", "device_map.json 설정 파일을 찾을 수 없습니다.");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var stationConfigs = JsonConvert.DeserializeObject<Dictionary<int, PlcStationConfig>>(json);

                PlcConnections.Clear();
                if (stationConfigs == null) return;

                foreach (var (stationNumber, config) in stationConfigs)
                {
                    var plcConnection = new PlcConnection
                    {
                        StationNumber = stationNumber,
                        Description = config.Description
                    };

                    foreach (var device in config.Devices)
                    {
                        plcConnection.DataItems.Add(new PlcDataItem
                        {
                            DeviceAddress = device.DeviceAddress,
                            Description = device.Description
                        });
                    }
                    PlcConnections.Add(plcConnection);
                }
                AddLog("Info", $"{PlcConnections.Count}개의 PLC 스테이션을 설정 파일에서 로드했습니다.");
            }
            catch (Exception ex)
            {
                AddLog("Error", $"PLC 스테이션 설정 파일 로드 실패: {ex.Message}");
            }
        }

        private async Task ExecuteConnectPlcAsync()
        {
            if (SelectedPlc == null) return;

            var plc = SelectedPlc;
            AddLog("Info", $"PLC 연결 시도 (스테이션: {plc.StationNumber} - {plc.Description})...");
            try
            {
                bool success = await _plcManager.ConnectAsync(plc.StationNumber);
                plc.IsConnected = success;

                if (success)
                {
                    AddLog("Success", $"스테이션 {plc.StationNumber}에 성공적으로 연결되었습니다.");
                    StartPolling(plc);
                }
                else
                {
                    AddLog("Error", $"스테이션 {plc.StationNumber} 연결에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                plc.IsConnected = false;
                AddLog("Error", $"스테이션 {plc.StationNumber} 연결 중 예외 발생: {ex.Message}");
            }
        }

        private async Task ExecuteDisconnectPlcAsync()
        {
            if (SelectedPlc == null) return;

            var plc = SelectedPlc;
            AddLog("Info", $"PLC 연결 해제 시도 (스테이션: {plc.StationNumber})...");
            try
            {
                StopPolling();
                await _plcManager.DisconnectAsync(plc.StationNumber);
                plc.IsConnected = false;
                AddLog("Info", $"스테이션 {plc.StationNumber} 연결이 해제되었습니다.");
            }
            catch (Exception ex)
            {
                AddLog("Error", $"스테이션 {plc.StationNumber} 연결 해제 중 예외 발생: {ex.Message}");
            }
        }

        private async Task ExecuteWritePlcValueAsync()
        {
            if (SelectedPlc == null || string.IsNullOrWhiteSpace(WriteAddress)) return;

            var plc = SelectedPlc;
            AddLog("Info", $"PLC 쓰기 시도: 스테이션 {plc.StationNumber}의 {WriteAddress}에 {WriteValue} 값 전송...");
            try
            {
                bool success = await _plcManager.WriteDeviceBlockAsync(plc.StationNumber, WriteAddress, new short[] { WriteValue });
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

        #region MQTT Methods
        private async Task ExecuteConnectMqttAsync()
        {
            AddLog("Info", $"MQTT 브로커 연결 시도: {Config.MqttBrokerAddress}:{Config.MqttBrokerPort}");
            try
            {
                // LWT 토픽: 이 Transmitter의 모든 PLC 연결 상태를 대표하는 토픽
                // 실제 환경에서는 site/area/line 등을 설정 파일에서 읽어와야 함
                string lwtTopic = $"status/factory-01/assembly/line-a/transmitter-01/connection";
                bool success = await _mqttService.ConnectAsync(Config.MqttBrokerAddress, Config.MqttBrokerPort, lwtTopic);
                IsMqttConnected = success;

                if (success)
                {
                    AddLog("Success", "MQTT 브로커에 성공적으로 연결되었습니다.");
                    // 연결 성공 시 온라인 상태 발행
                    await _mqttService.PublishAsync(lwtTopic, "{\"state\": \"online\"}", true);
                }
                else
                {
                    AddLog("Error", "MQTT 브로커 연결에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                IsMqttConnected = false;
                AddLog("Error", $"MQTT 연결 중 예외 발생: {ex.Message}");
            }
        }

        private async Task ExecuteDisconnectMqttAsync()
        {
            AddLog("Info", "MQTT 브로커 연결 해제...");
            await _mqttService.DisconnectAsync();
            IsMqttConnected = false;
            AddLog("Info", "MQTT 브로커 연결이 해제되었습니다.");
        }
        #endregion

        #region Polling
        private void StartPolling(PlcConnection plc)
        {
            _pollingCts?.Cancel();
            _pollingCts = new CancellationTokenSource();

            Task.Run(() => PollingLoopAsync(plc, _pollingCts.Token), _pollingCts.Token);
            AddLog("Info", $"스테이션 {plc.StationNumber} 데이터 폴링을 시작합니다.");
        }

        private void StopPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts = null;
            AddLog("Info", "데이터 폴링을 중지합니다.");
        }

        private async Task PollingLoopAsync(PlcConnection plc, CancellationToken token)
        {
            while (!token.IsCancellationRequested && plc.IsConnected)
            {
                try
                {
                    foreach (var item in plc.DataItems)
                    {
                        if (token.IsCancellationRequested) break;
                        try
                        {
                            var data = await _plcManager.ReadDeviceBlockAsync(plc.StationNumber, item.DeviceAddress, 1);
                            Application.Current.Dispatcher.Invoke(() => item.CurrentValue = data[0]);

                            // [MQTT 발행 로직 추가]
                            if (_mqttService.IsConnected)
                            {
                                // dt/{site}/{area}/{line}/{plc_id}/{measurement}
                                string topic = $"dt/factory-01/assembly/line-a/plc-{plc.StationNumber:D3}/{item.DeviceAddress}";
                                string payload = JsonConvert.SerializeObject(new { value = data[0], timestamp = DateTime.UtcNow });
                                await _mqttService.PublishAsync(topic, payload);
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog("Error", $"스테이션 {plc.StationNumber}의 '{item.DeviceAddress}' 읽기/발행 실패: {ex.Message}");
                            await Task.Delay(2000, token);
                        }
                    }
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    AddLog("Critical", $"폴링 루프 오류 (스테이션: {plc.StationNumber}): {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() => plc.IsConnected = false);
                    break;
                }
            }
            Debug.WriteLine($"PollingLoopAsync for Station {plc.StationNumber} finished.");
        }
        #endregion

        private void AddLog(string level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, new LogEntry { Timestamp = DateTime.Now, Level = level, Message = message });
                if (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
            });
        }
    }
}
