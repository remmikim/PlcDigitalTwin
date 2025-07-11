using System.Collections.Generic;

namespace Nona.Models
{
    /// <summary>
    /// device_map.json 파일의 각 스테이션 설정을 나타냅니다.
    /// </summary>
    public class PlcStationConfig
    {
        public string Description { get; set; } = string.Empty;
        public List<PlcDataItem> Devices { get; set; } = new List<PlcDataItem>();
    }
}
