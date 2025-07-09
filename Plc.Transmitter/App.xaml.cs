using Hermes.Services;
using Hermes.ViewModels;
using Hermes.Views;
using System.Windows;

namespace Hermes
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IPlcCommunicationService? _plcService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 서비스 인스턴스 생성
            _plcService = new PlcCommunicationService();

            // 2. 메인 ViewModel 생성 및 서비스 주입
            var mainViewModel = new MainViewModel(_plcService);

            // 3. 메인 윈도우 생성
            var mainWindow = new MainWindow
            {
                // 4. 윈도우의 DataContext에 ViewModel 할당
                DataContext = mainViewModel
            };

            // 5. 메인 윈도우 표시
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 애플리케이션 종료 시 서비스 리소스 정리
            (_plcService as IDisposable)?.Dispose();
            base.OnExit(e);
        }
    }
}
