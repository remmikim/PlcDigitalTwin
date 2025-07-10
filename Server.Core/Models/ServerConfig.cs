/*
 * Server.Core/Models/ServerConfig.cs
 * appsettings.json 파일의 설정을 바인딩하기 위한 C# 클래스입니다.
 */
namespace Server.Core.Models
{
    public class ServerConfig
    {
        public MqttBrokerConfig MqttBroker { get; set; } = new();
        public FirebaseConfig Firebase { get; set; } = new();
    }

    public class MqttBrokerConfig
    {
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        // [추가] 사용자 이름과 비밀번호 속성
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class FirebaseConfig
    {
        public string ServiceAccountKeyPath { get; set; } = string.Empty;
    }
}
