using Microsoft.Win32;

namespace Msfs2024Ai.Copilot.Gsx;

internal sealed class GsxInstallation
{
    private const string RegistryKey = @"Software\Fsdreamteam";
    private const string RegistryValue = "root";
    private const string PanelRelativePath =
        @"MSFS\fsdreamteam-gsx-pro\html_ui\InGamePanels\FSDT_GSX_Panel";

    private GsxInstallation(string rootPath, string panelPath)
    {
        RootPath = rootPath;
        PanelPath = panelPath;
    }

    public string RootPath { get; }
    public string PanelPath { get; }
    public string MenuPath => Path.Combine(PanelPath, "menu");
    public string TooltipPath => Path.Combine(PanelPath, "tooltip");

    public static GsxInstallation? Discover()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            var root = key?.GetValue(RegistryValue) as string;
            return FromRoot(root);
        }
        catch
        {
            return null;
        }
    }

    internal static GsxInstallation? FromRoot(string? root)
    {
        var configuredRoot = root?.Trim();
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return null;
        }

        var fullRoot = Path.GetFullPath(configuredRoot!);
        var panel = Path.Combine(fullRoot, PanelRelativePath);
        return Directory.Exists(panel)
            ? new GsxInstallation(fullRoot, panel)
            : null;
    }
}
