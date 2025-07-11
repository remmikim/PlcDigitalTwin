/*
 * Decima/Program.cs
 * 애플리케이션의 주 진입점입니다.
 * 서비스 설정, 의존성 주입, 애플리케이션 실행을 담당합니다.
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Decima.Models;
using Decima.Services;
using System.IO;

namespace Decima
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // .NET Generic Host를 사용하여 애플리케이션을 구성하고 실행합니다.
            // 이 방식은 로깅, 설정 관리, 의존성 주입(DI)을 쉽게 통합할 수 있게 해줍니다.
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // 기본 appsettings.json 및 환경 변수 외에 추가 설정 소스를 구성합니다.
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // =================================================================
                    // [수정] 주석 처리되었던 서비스들을 모두 등록합니다.
                    // =================================================================

                    // 1. 설정(Configuration) 바인딩: appsettings.json 파일의 내용을 ServerConfig 클래스와 연결합니다.
                    services.Configure<ServerConfig>(hostContext.Configuration);

                    // 2. 서비스 의존성 주입 (Singleton으로 등록하여 앱 전체에서 단일 인스턴스 사용)
                    services.AddSingleton<IMqttService, MqttService>();
                    services.AddSingleton<IFirebaseService, FirebaseService>();
                    services.AddSingleton<IDataProcessorService, DataProcessorService>();

                    // 3. 메인 워커 서비스를 백그라운드 서비스로 등록합니다.
                    // 이 서비스가 등록되어야 애플리케이션이 바로 종료되지 않고 계속 실행됩니다.
                    services.AddHostedService<ServerWorker>();
                })
                .Build();

            Console.WriteLine("Decima application starting...");
            await host.RunAsync();
            Console.WriteLine("Decima application stopped.");
        }
    }
}
