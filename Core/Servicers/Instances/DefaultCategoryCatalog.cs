using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Servicers.Instances
{
    internal static class DefaultCategoryCatalog
    {
        internal sealed class Definition
        {
            internal string Key { get; }
            internal string Name { get; }
            internal string Color { get; }
            internal string[] Processes { get; }

            internal Definition(string key, string name, string color, params string[] processes)
            {
                Key = key;
                Name = name;
                Color = color;
                Processes = processes;
            }
        }

        internal static readonly Definition[] Categories =
        {
            new Definition("browsing", "浏览器", "#328B78", "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "Arc", "360chrome", "360se", "SogouExplorer"),
            new Definition("office", "办公文档", "#4969D8", "WINWORD", "EXCEL", "POWERPNT", "ONENOTE", "MSPUB", "wps", "et", "wpp", "notepad", "notepad++", "Obsidian", "Typora", "siyuan", "Notion"),
            new Definition("development", "开发工具", "#267B92", "Code", "Code - Insiders", "devenv", "rider64", "idea64", "pycharm64", "webstorm64", "goland64", "clion64", "datagrip64", "AndroidStudio", "studio64", "eclipse", "git-bash", "GitHubDesktop", "postman", "insomnia", "WindowsTerminal", "wezterm-gui", "cursor", "windsurf"),
            new Definition("communication", "沟通协作", "#7B65AA", "WeChat", "Weixin", "QQ", "WXWork", "DingTalk", "Feishu", "Lark", "Teams", "ms-teams", "Slack", "Discord", "Zoom", "Telegram", "WhatsApp", "Signal"),
            new Definition("design", "设计创作", "#BD675C", "Photoshop", "Illustrator", "InDesign", "AfterFX", "Premiere Pro", "Adobe XD", "Figma", "blender", "Canva", "Krita", "GIMP", "AffinityPhoto2", "AffinityDesigner2", "AffinityPublisher2", "DaVinci Resolve"),
            new Definition("learning", "学习阅读", "#6B8D4B", "Anki", "Calibre", "SumatraPDF", "AcroRd32", "Acrobat", "Kindle", "Zotero", "JabRef", "FoxitPDFReader"),
            new Definition("media", "影音娱乐", "#BF7C36", "Spotify", "cloudmusic", "QQMusic", "KuGou", "PotPlayerMini64", "PotPlayerMini", "vlc", "mpv", "iTunes", "Bilibili"),
            new Definition("gaming", "游戏", "#B55878", "steam", "EpicGamesLauncher", "GalaxyClient", "Battle.net", "RiotClientServices", "UbisoftConnect", "EADesktop", "WeGame"),
            new Definition("utilities", "系统工具", "#647481", "7zFM", "WinRAR", "Everything", "PowerToys", "ShareX", "SnippingTool", "KeePass", "KeePassXC", "Bitwarden", "qBittorrent", "uTorrent", "AnyDesk", "TeamViewer", "rustdesk")
        };

        private static readonly Dictionary<string, string> ProcessCategories = Categories
            .SelectMany(category => category.Processes.Select(process => new { process, category.Key }))
            .ToDictionary(item => item.process, item => item.Key, StringComparer.OrdinalIgnoreCase);

        internal static CategoryModel Find(string processName, IDictionary<string, int> ids, IEnumerable<CategoryModel> categories)
        {
            if (string.IsNullOrWhiteSpace(processName) || ids == null) return null;
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4) : processName;
            if (!ProcessCategories.TryGetValue(name, out var key) || !ids.TryGetValue(key, out var id)) return null;
            return categories.FirstOrDefault(category => category.ID == id);
        }
    }
}
