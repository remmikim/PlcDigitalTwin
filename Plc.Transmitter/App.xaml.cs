using Hermes.Services;
using Hermes.ViewModels;
using Hermes.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Hermes
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private PlcManagerService? _plcManager;
        private IMqttClientService? _mqttService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // =================================================================
            // [수정] 전역 예외 처리기 설정
            // 애플리케이션의 모든 처리되지 않은 예외를 잡아 로깅하여,
            // 예기치 않은 종료를 방지하고 원인을 추적합니다.
            // =================================================================
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 1. 서비스 인스턴스 생성
            _plcManager = new PlcManagerService();
            _mqttService = new MqttClientService();

            // 2. 메인 ViewModel 생성 및 서비스 주입
            var mainViewModel = new MainViewModel(_plcManager, _mqttService);

            // 3. 메인 윈도우 생성 및 DataContext 할당
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // =================================================================
            // [수정] MainWindow를 애플리케이션의 주 윈도우로 명시적 설정
            // 이 설정을 통해 MainWindow가 닫힐 때까지 애플리케이션이 종료되지 않습니다.
            // 이것이 조기 종료 문제의 핵심 해결책입니다.
            // =================================================================
            Application.Current.MainWindow = mainWindow;


            // 4. 메인 윈도우 표시
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 애플리케이션 종료 시 모든 서비스 리소스 정리
            _plcManager?.Dispose();
            (_mqttService as IDisposable)?.Dispose();

            // 정상 종료 로그 추가
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_lifecycle_log.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] - Application is exiting cleanly.\n");

            base.OnExit(e);
        }

        #region Global Exception Handlers

        /// <summary>
        /// 예외 정보를 파일에 로깅하는 중앙 핸들러
        /// </summary>
        private void LogException(string eventName, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] - {eventName}\n{ex.ToString()}\n\n";
                File.AppendAllText(logPath, message);
                MessageBox.Show($"치명적인 오류가 발생했습니다. 프로그램 실행 폴더의 error_log.txt 파일을 확인하세요.\n\n오류: {ex.Message}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception logEx)
            {
                // 로깅 자체에서 오류가 발생할 경우를 대비
                Debug.WriteLine($"Failed to log exception: {logEx}");
            }
        }

        /// <summary>
        /// UI 스레드에서 발생하는 예외를 처리합니다.
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // 앱이 즉시 종료되는 것을 방지
        }

        /// <summary>
        /// 백그라운드 Task에서 발생하는 예외 (await되지 않은 Task)를 처리합니다.
        /// </summary>
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        /// <summary>
        /// 애플리케이션 도메인 전반에서 발생하는 모든 예외를 처리하는 최후의 보루입니다.
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("AppDomain.CurrentDomain.UnhandledException", (Exception)e.ExceptionObject);
        }

        #endregion
    }
}
