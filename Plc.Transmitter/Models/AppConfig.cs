using Hermes.Helpers;

namespace Hermes.Models
{
    /// <summary>
    /// 애플리케이션의 설정 값을 관리하는 데이터 모델입니다.
    /// </summary>
    public class AppConfig : ObservableObject
    {
        private int _plcStationNumber = 1;
        public int PlcStationNumber
        {
            get => _plcStationNumber;
            set
            {
                _plcStationNumber = value;
                OnPropertyChanged();
            }
        }

        private string _mqttBrokerAddress = "127.0.0.1";
        public string MqttBrokerAddress
        {
            get => _mqttBrokerAddress;
            set
            {
                _mqttBrokerAddress = value;
                OnPropertyChanged();
            }
        }

        private int _mqttBrokerPort = 1883;
        public int MqttBrokerPort
        {
            get => _mqttBrokerPort;
            set
            {
                _mqttBrokerPort = value;
                OnPropertyChanged();
            }
        }
        private string _mqttUsername = "hermes_user";
        public string MqttUsername
        {
            get => _mqttUsername;
            set
            {
                _mqttUsername = value;
                OnPropertyChanged();
            }
        }
        private string _mqttPassword = "1234";
        public string MqttPassword
        {
            get => _mqttPassword;
            set
            {
                _mqttPassword = value;
                OnPropertyChanged();
            }
        }
    }
}
