using Nona.Helpers;
using Nona.Models;
using Nona.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Nona.ViewModels
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

                ((RelayCommand)ConnectPlcCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DisconnectPlcCommand).RaiseCanExecuteChanged();
                ((RelayCommand)WritePlcValueCommand).RaiseCanExecuteChanged();
            }
        }

        // MQTT 연결 상태를 서비스로부터 직접 바인딩하기 위한 속성
        public bool IsMqttConnected => _mqttService.IsConnected;

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
        public ICommand StartMqttServiceCommand { get; }
        public ICommand StopMqttServiceCommand { get; }
        #endregion

        public MainViewModel(PlcManagerService plcManager, IMqttClientService mqttService)
        {
            _plcManager = plcManager ?? throw new ArgumentNullException(nameof(plcManager));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));

            // 서비스에서 발생하는 로그를 UI에 표시하기 위해 이벤트 핸들러 등록
            _mqttService.LogMessageGenerated += (level, message) => AddLog(level, message);

            ConnectPlcCommand = new RelayCommand(async _ => await ExecuteConnectPlcAsync(), _ => SelectedPlc != null && !SelectedPlc.IsConnected);
            DisconnectPlcCommand = new RelayCommand(async _ => await ExecuteDisconnectPlcAsync(), _ => SelectedPlc != null && SelectedPlc.IsConnected);
            WritePlcValueCommand = new RelayCommand(async _ => await ExecuteWritePlcValueAsync(), _ => SelectedPlc != null && SelectedPlc.IsConnected);

            // UI에서 MQTT 서비스를 시작하고 중지하기 위한 커맨드
            StartMqttServiceCommand = new RelayCommand(async _ => await ExecuteStartMqttServiceAsync());
            StopMqttServiceCommand = new RelayCommand(async _ => await _mqttService.StopAsync());

            LoadPlcStations();
            AddLog("Info", "애플리케이션이 시작되었습니다.");

            // 앱 시작 시 MQTT 서비스 자동 시작
            _ = ExecuteStartMqttServiceAsync();
        }

        private async Task ExecuteStartMqttServiceAsync()
        {
            string lwtTopic = $"status/factory-01/assembly/line-a/transmitter-01/connection";
            await _mqttService.StartAsync(
                Config.MqttBrokerAddress,
                Config.MqttBrokerPort,
                Config.MqttUsername,
                Config.MqttPassword,
                lwtTopic
            );
        }

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

                if (stationConfigs == null) return;
                PlcConnections.Clear();
                foreach (var (stationNumber, config) in stationConfigs)
                {
                    var plcConnection = new PlcConnection
                    {
                        StationNumber = stationNumber,
                        Description = config.Description,
                        Site = config.Site,
                        Area = config.Area,
                        Line = config.Line
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
                            short newValue = data[0];

                            if (item.CurrentValue != newValue)
                            {
                                Application.Current.Dispatcher.Invoke(() => item.CurrentValue = newValue);

                                // =================================================================
                                // [최종 수정] 이제 ViewModel은 연결 상태를 걱정하지 않고 발행만 요청합니다.
                                // =================================================================
                                string topic = $"dt/{plc.Site}/{plc.Area}/{plc.Line}/plc-{plc.StationNumber:D3}/{item.DeviceAddress}";

                                char deviceTypeChar = item.DeviceAddress.FirstOrDefault();
                                string deviceType = deviceTypeChar != default(char) ? deviceTypeChar.ToString().ToUpper() : "Unknown";
                                object valueToSend = ("XYM".Contains(deviceType)) ? (object)(newValue != 0) : newValue;

                                var payloadObject = new
                                {
                                    site = plc.Site,
                                    area = plc.Area,
                                    line = plc.Line,
                                    station = plc.StationNumber,
                                    address = item.DeviceAddress,
                                    description = item.Description,
                                    deviceType = deviceType,
                                    value = valueToSend,
                                    timestamp = DateTime.UtcNow
                                };

                                string payload = JsonConvert.SerializeObject(payloadObject);
                                AddLog("MQTT-TX", $"발행 요청: {topic}");

                                // 서비스에 발행을 요청합니다. 서비스가 알아서 전송을 보장합니다.
                                await _mqttService.PublishAsync(topic, payload);
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog("Error", $"폴링 중 오류: '{item.DeviceAddress}' 읽기 실패: {ex.Message}");
                            await Task.Delay(2000, token);
                        }
                    }
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    AddLog("Critical", $"폴링 루프 중단 (스테이션: {plc.StationNumber}): {ex.Message}");
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
