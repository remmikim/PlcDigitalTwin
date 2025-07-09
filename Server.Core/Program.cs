/*
 * Server.Core/Program.cs
 * 애플리케이션의 주 진입점입니다.
 * 서비스 설정, 의존성 주입, 애플리케이션 실행을 담당합니다.
 */
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Core.Services; // 인터페이스 네임스페이스 추가

namespace Server.Core
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // .NET Generic Host를 사용하여 애플리케이션을 구성하고 실행합니다.
            // 이 방식은 로깅, 설정 관리, 의존성 주입(DI)을 쉽게 통합할 수 있게 해줍니다.
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // 여기에 서비스들을 등록할 예정입니다.
                    // 예: services.AddSingleton<IMqttService, MqttService>();
                    // 예: services.AddSingleton<IFirebaseService, FirebaseService>();
                    // 예: services.AddSingleton<IDataProcessorService, DataProcessorService>();
                    // 예: services.AddHostedService<ServerWorker>(); // 메인 워커 서비스
                })
                .Build();

            Console.WriteLine("Server.Core application starting...");
            await host.RunAsync();
            Console.WriteLine("Server.Core application stopped.");
        }
    }
}
