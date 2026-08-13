using System.Windows.Forms;
using System.Threading;
using Msfs2024Ai.Copilot.Diagnostics;

namespace Msfs2024Ai.Copilot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Write($"Unhandled process exception: {eventArgs.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Write($"Unobserved task exception: {eventArgs.Exception}");
            eventArgs.SetObserved();
        };
        AppLog.Write($"Copilot process starting (PID {System.Diagnostics.Process.GetCurrentProcess().Id}).");

        using var instanceMutex = new Mutex(
            initiallyOwned: false,
            name: @"Local\MSFS2024_AI_Copilot");
        var ownsInstanceMutex = false;
        try
        {
            ownsInstanceMutex = instanceMutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsInstanceMutex = true;
        }
        if (!ownsInstanceMutex)
        {
            MessageBox.Show(
                "MSFS 2024 Virtual First Officer is already running. Close the existing instance before starting another.",
                "MSFS 2024 Virtual First Officer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var oneShotCommand = GetOption(args, "--command");
        var showUi = oneShotCommand == null
                     || args.Any(arg => string.Equals(arg, "--ui", StringComparison.OrdinalIgnoreCase));

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            using var service = new CopilotService(oneShotCommand, showUi);
            if (showUi)
            {
                service.Shown += (_, _) =>
                {
                    service.Activate();
                    service.BeginInvoke(new Action(service.Connect));
                };
                Application.Run(service);
            }
            else
            {
                service.Connect();
                Application.Run();
            }
        }
        catch (Exception exception)
        {
            AppLog.Write($"Copilot process terminating after fatal exception: {exception}");
            throw;
        }
        finally
        {
            AppLog.Write("Copilot application loop exited.");
            instanceMutex.ReleaseMutex();
        }
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}

