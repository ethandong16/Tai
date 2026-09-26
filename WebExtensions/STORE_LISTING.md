# Tai WinUI3 网页统计助手：商店上架资料

本扩展配合 Tai WinUI3 修改版工作。它是 Tai 独立 fork 的配套扩展，并非原 Tai 项目的官方发行版。

## Chrome Web Store

- 名称：`Tai WinUI3 网页统计助手`
- 简短说明：`配合 Tai WinUI3 修改版，统计当前活动网页的本地浏览时长；数据仅发送到本机 Tai。`
- 类别：生产力工具
- 默认语言：中文（简体）
- 官方网站：`https://github.com/ethandong16/Tai`
- 隐私政策：`https://github.com/ethandong16/Tai/blob/master/docs/browser-extension-privacy.md`

详细说明：

> Tai WinUI3 网页统计助手用于连接 Chromium 浏览器和 Tai WinUI3 修改版桌面应用。扩展在浏览器处于前台时记录当前活动标签页的 URL、标题、网站图标地址和停留时长，并只通过本机回环地址发送给 Tai，用于生成本地网站使用时间统计。无需账号，不提供广告，不将浏览记录上传到开发者服务器。使用前请先安装并运行 Tai WinUI3 修改版，并在设置中开启“网站浏览统计”。本项目是 Tai 的独立 fork，并非原 Tai 项目的官方发行版。

单一用途说明：

> 将当前活动标签页的本地使用时长发送给同一台电脑上运行的 Tai WinUI3 修改版，以生成网站使用时间统计。

`tabs` 权限说明：

> 扩展需要识别当前活动标签页及标签页切换，并读取 URL、标题和网站图标地址，才能按网站统计前台停留时长。扩展不读取网页正文、表单、密码、Cookie 或下载内容。

数据披露建议：

- 网站历史记录：是（URL、标题和停留时长）。
- 网站内容：否（不读取网页正文或表单内容）。
- 身份验证、个人身份、财务、健康、位置、通信：否。
- 数据用途：核心功能。
- 数据出售、广告、信用评估：否。
- 数据传输：只发送到本机 `127.0.0.1` 上的 Tai。

## Microsoft Edge Add-ons

可上传同一个 ZIP。商店名称、说明、权限理由和隐私政策保持一致，并在描述中保留“独立 fork、非原项目官方发行版”的说明。

## 发布前清单

1. 运行 `powershell -ExecutionPolicy Bypass -File scripts/Pack-BrowserExtension.ps1`。
2. 使用新生成的 `artifacts/Tai-WinUI3-Browser-Extension-1.1.0.zip` 上传，不要上传整个仓库。
3. 上传至少一张清楚展示 Tai 网站统计结果的商店截图；截图不得暗示这是原 Tai 官方发行版。
4. 在开发者控制台填写上面的权限理由、数据披露和隐私政策 URL。
5. 审核通过后，在桌面端只引导用户打开已审核的 Chrome Web Store 页面，不提供未打包扩展的手动加载或自动安装功能。
6. 以后更新时递增 `manifest.json` 的 `version`，并更新同一个商店条目，不要创建新的条目，否则扩展 ID 会改变。
