using System;

namespace Nona.Models
{
    /// <summary>
    /// 로그 메시지를 나타내는 데이터 모델입니다.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
