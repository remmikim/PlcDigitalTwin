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
        private PlcManagerService? _plcManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. PLC 매니저 서비스 인스턴스 생성
            _plcManager = new PlcManagerService();

            // 2. 메인 ViewModel 생성 및 매니저 서비스 주입
            var mainViewModel = new MainViewModel(_plcManager);

            // 3. 메인 윈도우 생성 및 DataContext 할당
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // 4. 메인 윈도우 표시
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 애플리케이션 종료 시 매니저 서비스를 통해 모든 리소스 정리
            _plcManager?.Dispose();
            base.OnExit(e);
        }
    }
}
