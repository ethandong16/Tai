using System;
using System.Collections.Generic;

namespace Core.Servicers.Instances
{
    internal static class DefaultWebSiteCategories
    {
        internal sealed class Definition
        {
            public Definition(string name, string color, params string[] domains)
            {
                Name = name;
                Color = color;
                Domains = domains;
            }

            public string Name { get; }
            public string Color { get; }
            public IReadOnlyList<string> Domains { get; }
        }

        internal static IReadOnlyList<Definition> All { get; } = new[]
        {
            new Definition("办公协作", "#4969D8", "docs.google.com", "drive.google.com", "mail.google.com", "outlook.live.com", "notion.so", "feishu.cn", "larksuite.com", "figma.com"),
            new Definition("开发技术", "#267B92", "github.com", "gitlab.com", "stackoverflow.com", "npmjs.com", "developer.mozilla.org", "learn.microsoft.com", "dev.to", "csdn.net"),
            new Definition("学习阅读", "#6B8D4B", "wikipedia.org", "wikibooks.org", "coursera.org", "edx.org", "arxiv.org", "zhihu.com"),
            new Definition("社交社区", "#7B65AA", "reddit.com", "x.com", "twitter.com", "weibo.com", "v2ex.com", "discord.com", "facebook.com", "instagram.com"),
            new Definition("影音娱乐", "#BF7C36", "bilibili.com", "youtube.com", "netflix.com", "twitch.tv", "spotify.com", "douyin.com", "iqiyi.com", "youku.com"),
            new Definition("购物生活", "#B55878", "taobao.com", "tmall.com", "jd.com", "amazon.com", "amazon.cn", "meituan.com", "ctrip.com"),
            new Definition("搜索资讯", "#328B78", "google.com", "bing.com", "baidu.com", "duckduckgo.com", "news.qq.com", "thepaper.cn")
        };

        internal static string Match(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return null;
            var host = domain.Trim().TrimEnd('.');
            foreach (var category in All)
                foreach (var knownDomain in category.Domains)
                    if (host.Equals(knownDomain, StringComparison.OrdinalIgnoreCase)
                        || host.EndsWith("." + knownDomain, StringComparison.OrdinalIgnoreCase))
                        return category.Name;
            return null;
        }
    }
}
