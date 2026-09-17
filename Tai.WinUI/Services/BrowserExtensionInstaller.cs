using System.Diagnostics;
using Microsoft.Win32;

namespace Tai.WinUI.Services;

internal sealed record ExtensionBrowser(string Name, string Executable, string ExtensionsUrl);

internal static class BrowserExtensionInstaller
{
    public static string ExtensionDirectory => Path.Combine(AppContext.BaseDirectory, "WebExtensions", "Chrome");

    public static IReadOnlyList<ExtensionBrowser> DetectBrowsers()
    {
        var definitions = new[]
        {
            ("Google Chrome", "chrome.exe", @"Google\Chrome\Application\chrome.exe", "chrome://extensions/"),
            ("Microsoft Edge", "msedge.exe", @"Microsoft\Edge\Application\msedge.exe", "edge://extensions/"),
            ("Brave", "brave.exe", @"BraveSoftware\Brave-Browser\Application\brave.exe", "brave://extensions/"),
            ("Vivaldi", "vivaldi.exe", @"Vivaldi\Application\vivaldi.exe", "vivaldi://extensions/"),
            ("Opera", "opera.exe", @"Programs\Opera\opera.exe", "opera://extensions/")
        };
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        var found = new List<ExtensionBrowser>();
        foreach (var (name, executable, relativePath, url) in definitions)
        {
            var path = FindRegisteredExecutable(executable) ?? roots
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Select(root => Path.Combine(root, relativePath)).FirstOrDefault(File.Exists);
            if (path != null) found.Add(new ExtensionBrowser(name, path, url));
        }
        return found;
    }

    private static string? FindRegisteredExecutable(string executable)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + executable);
                var path = (key?.GetValue(null) as string)?.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return null;
    }

    public static void ValidatePackage()
    {
        foreach (var file in new[] { "manifest.json", "service-worker.js", "icon48.png", "icon128.png",
                     "icons/socket-active.png", "icons/socket-inactive.png" })
            if (!File.Exists(Path.Combine(ExtensionDirectory, file)))
                throw new FileNotFoundException("扩展文件缺失，请重新解压完整的 Tai 发布包。", file);
    }

    public static void OpenInstallPage(ExtensionBrowser browser)
    {
        ValidatePackage();
        var start = new ProcessStartInfo(browser.Executable) { UseShellExecute = false };
        start.ArgumentList.Add(browser.ExtensionsUrl);
        using var process = Process.Start(start);
        if (process == null) throw new InvalidOperationException("无法启动浏览器。");
    }
}
