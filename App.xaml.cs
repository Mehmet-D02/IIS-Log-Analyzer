using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace IISLogAnalyzer_WPF;

public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogToCrashFile("UI Thread Exception", e.Exception);
        e.Handled = true; 
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogToCrashFile("AppDomain Exception", e.ExceptionObject as Exception);
    }

    private void LogToCrashFile(string title, Exception? ex)
    {
        try 
        {
             var sb = new StringBuilder();
             sb.AppendLine($"--- CRASH REPORT: {DateTime.Now} ---");
             sb.AppendLine($"Title: {title}");
             
             var current = ex;
             int depth = 0;
             while (current != null)
             {
                 sb.AppendLine($"\n[Level {depth}] {current.GetType().Name}:");
                 sb.AppendLine($"Message: {current.Message}");
                 sb.AppendLine($"StackTrace: {current.StackTrace}");
                 current = current.InnerException;
                 depth++;
             }

             string logContent = sb.ToString();
             File.WriteAllText("crash_detail.txt", logContent);
             
             MessageBox.Show($"Application crashed.\nDetails saved to 'crash_detail.txt'.\n\nTop Error: {ex?.Message}", "Crash Details", MessageBoxButton.OK, MessageBoxImage.Error);
        } 
        catch 
        {
            // Failsafe
            MessageBox.Show(ex?.ToString() ?? "Unknown fatal error", "Fatal Error");
        }
    }
}
