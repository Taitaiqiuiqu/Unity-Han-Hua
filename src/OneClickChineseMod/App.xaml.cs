using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show(
            $"应用程序发生未处理的异常:\n\n{e.Exception.Message}\n\n详细信息已写入日志文件。",
            "错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
        if (e.IsTerminating)
        {
            MessageBox.Show(
                $"应用程序遇到严重错误即将关闭:\n\n{(e.ExceptionObject as Exception)?.Message ?? "未知错误"}\n\n详细信息已写入日志文件。",
                "致命错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        foreach (var ex in e.Exception.InnerExceptions)
        {
            LogException(ex);
        }
        e.SetObserved();
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var logEntry = $"[UNHANDLED] {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\n";
            if (ex.InnerException != null)
            {
                logEntry += $"[Inner] {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";
            }
            ConsoleUtils.WriteError(logEntry.TrimEnd());
        }
        catch { }
    }
}
