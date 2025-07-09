using Hermes.Helpers;

namespace Hermes.Models
{
    /// <summary>
    /// PLC의 개별 디바이스 데이터 항목을 나타내는 모델입니다.
    /// </summary>
    public class PlcDataItem : ObservableObject
    {
        private string _deviceAddress = string.Empty;
        public string DeviceAddress
        {
            get => _deviceAddress;
            set
            {
                _deviceAddress = value;
                OnPropertyChanged();
            }
        }

        private short _currentValue;
        public short CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                OnPropertyChanged();
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }
}
