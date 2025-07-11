using System.Collections.Generic;

namespace Nona.Models
{
    /// <summary>
    /// device_map.json 파일의 각 스테이션 설정을 나타냅니다.
    /// </summary>
    public class PlcStationConfig
    {
        // [추가] 사이트 정보 (예: factory-01)
        public string Site { get; set; } = string.Empty;
        // [추가] 공정 정보 (예: assembly)
        public string Area { get; set; } = string.Empty;
        // [추가] 라인 정보 (예: line-a)
        public string Line { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PlcDataItem> Devices { get; set; } = new List<PlcDataItem>();
    }
}
