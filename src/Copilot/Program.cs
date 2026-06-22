using System.Windows.Forms;
using System.Threading;

namespace Msfs2024Ai.Copilot;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
                "MSFS 2024 AI Copilot is already running. Close the existing instance before starting another.",
                "MSFS 2024 AI Copilot",
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
            service.Connect();
            if (showUi)
            {
                service.Show();
                service.Activate();
                Application.Run();
            }
            else
            {
                Application.Run();
            }
        }
        finally
        {
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
