# Tai WinUI3 网页统计助手隐私政策

最后更新：2026 年 9 月 17 日

“Tai WinUI3 网页统计助手”是供 [Tai WinUI3 修改版](https://github.com/ethandong16/Tai) 使用的浏览器扩展。本项目是 Tai 的独立 fork，并非原 Tai 项目的官方发行版。

## 收集和使用的数据

扩展在浏览器窗口处于前台时读取当前活动标签页的 URL、网页标题、网站图标地址、开始时间和停留时长。这些数据仅用于在本机生成网站使用时间统计。

扩展使用 `tabs` 权限识别当前活动标签页以及标签页切换。扩展不读取网页正文、表单内容、密码、Cookie、下载内容或浏览器账户信息。

## 数据传输与存储

扩展只通过 `ws://127.0.0.1:8908/TaiWebSentry` 将统计数据发送给同一台电脑上运行的 Tai。扩展不会将浏览数据发送到开发者或其他远程服务，也不会出售、共享或用于广告和用户画像。

连接本机 Tai 失败时，扩展可能在浏览器进程内存中暂存尚未发送的记录，以便重连后补发；这些临时数据不会由扩展写入磁盘。Tai 会把收到的统计记录存入用户电脑上的本地数据库。为显示网站图标，Tai 可能从标签页提供的网站图标地址发起网络请求并在本机缓存图标。

## 数据控制与保留

用户可以在 Tai 设置中关闭网站浏览统计、按时间范围删除统计数据，或卸载浏览器扩展。数据保留时间由用户控制；删除 Tai 的本地数据库及网站图标缓存也会删除对应的本地数据。

## 第三方披露

除用户访问的网站在正常浏览及网站图标请求中获得的常规网络信息外，本扩展不会向第三方披露所处理的数据。法律要求另有规定时除外。

## 政策变更与联系

本政策如有变更，将在本页面更新日期和内容。问题可通过 [GitHub Issues](https://github.com/ethandong16/Tai/issues) 联系维护者。

---

# Privacy Policy for Tai WinUI3 Web Statistics Companion

Last updated: September 17, 2026

Tai WinUI3 Web Statistics Companion is a browser extension for the [Tai WinUI3 modified edition](https://github.com/ethandong16/Tai). This project is an independent fork of Tai and is not an official release of the original Tai project.

## Data collected and used

While the browser window is in the foreground, the extension reads the active tab's URL, page title, favicon URL, start time, and time spent. This data is used only to produce website usage statistics on the user's computer.

The `tabs` permission is used to identify the active tab and tab changes. The extension does not read page body content, form entries, passwords, cookies, downloads, or browser account information.

## Data transmission and storage

The extension sends statistics only to Tai running on the same computer through `ws://127.0.0.1:8908/TaiWebSentry`. Browsing data is not sent to the developer or any remote service, and it is not sold, shared, used for advertising, or used for profiling.

If the local Tai connection is unavailable, unsent records may be held temporarily in browser process memory and retried after reconnection. The extension does not persist this temporary queue to disk. Tai stores received statistics in a local database on the user's computer. To display website icons, Tai may request the favicon URL supplied by the tab and cache that icon locally.

## Data control and retention

Users can disable website tracking in Tai, delete statistics for a selected date range, or uninstall the extension. Retention is controlled by the user. Deleting Tai's local database and favicon cache also removes the corresponding local data.

## Third-party disclosure

The extension does not disclose processed data to third parties, apart from ordinary network information exposed to websites during normal browsing and favicon requests, unless disclosure is required by law.

## Changes and contact

Changes to this policy will be reflected by updating this page and its revision date. Contact the maintainer through [GitHub Issues](https://github.com/ethandong16/Tai/issues).
