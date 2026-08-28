using System.Windows;

namespace XPathScanner.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Nếu có đối số dòng lệnh (cmd) → chạy chế độ CLI, KHÔNG mở cửa sổ WPF.
        // KHÔNG gọi base.OnStartup để tránh xử lý StartupUri / mở MainWindow.
        if (e.Args.Length > 0)
        {
            int exitCode = CliRunner.Run(e.Args);
            Shutdown(exitCode);
            return;
        }

        base.OnStartup(e);
    }
}
