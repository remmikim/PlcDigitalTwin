using System;
using System.Diagnostics;
using System.Windows;

namespace Nona.Views
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // =================================================================
            // [추가] 윈도우가 닫힐 때 이벤트를 로깅하여 생명주기 추적
            // =================================================================
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // 디버그 출력 창에서 이 메시지가 언제 나타나는지 확인하여
            // 윈도우가 예기치 않게 닫히는지 추적할 수 있습니다.
            Debug.WriteLine("### Main window has been closed. Application will now exit. ###");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
