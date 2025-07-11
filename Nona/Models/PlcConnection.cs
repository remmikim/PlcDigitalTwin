using Nona.Helpers;
using System.Collections.ObjectModel;

namespace Nona.Models
{
    /// <summary>
    /// 단일 PLC 연결에 대한 모든 정보를 관리하는 모델 클래스입니다.
    /// </summary>
    public class PlcConnection : ObservableObject
    {
        public int StationNumber { get; set; }

        public string Description { get; set; } = string.Empty;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PlcDataItem> DataItems { get; set; }

        public PlcConnection()
        {
            DataItems = new ObservableCollection<PlcDataItem>();
        }
    }
}
