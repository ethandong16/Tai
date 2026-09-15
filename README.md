# Tai

Tai 是一款运行在 Windows 上的本地时间统计工具，用来记录应用使用时长和网站浏览时长，帮助你了解时间实际花在了哪里。

![Tai](index.jpg)

## 功能

- 统计前台应用的使用时长，并按日期、周、月和年查看趋势。
- 通过 Chrome/Edge 浏览器扩展统计当前标签页的网站浏览时长。
- 提供概览、统计、详细记录、分类和设置页面。
- 支持应用白名单、应用和网址过滤、正则匹配、关联进程和睡眠监测。
- 支持按运行目录自动分类。
- 支持导入/导出统计数据和配置；统计数据可导出为 `.xlsx` 和 `.csv`。
- 使用本地 SQLite 数据库存储数据，不需要登录账号或云端服务。

## 下载使用

已发布的 Windows 版本可以在 [Releases](https://github.com/ethandong16/Tai/releases) 页面下载。下载压缩包后解压到合适的位置，再运行对应版本的 Tai 可执行文件。

首次使用时建议：

1. 以管理员身份运行 Tai，以便统计部分需要更高权限的应用。
2. 在 Tai 的“设置”中按需启用网站统计、睡眠监测、白名单或过滤规则。
3. 如果需要网站统计，请按下面的说明安装 Chrome 扩展。

### 安装浏览器扩展

仓库内的扩展位于 [`WebExtensions/Chrome`](WebExtensions/Chrome)。它适用于 Chrome、Microsoft Edge 以及支持 Chrome 扩展的 Chromium 浏览器。

1. 启动 Tai，并在“设置”中启用网站统计。
2. 打开浏览器的扩展管理页面，例如 Chrome 的 `chrome://extensions` 或 Edge 的 `edge://extensions`。
3. 开启“开发者模式”。
4. 选择“加载已解压的扩展”，选中仓库中的 `WebExtensions/Chrome` 文件夹。
5. 当扩展图标显示为已连接状态时，网站浏览数据会发送给本机运行的 Tai。

扩展需要浏览器允许 `tabs` 权限，并通过本机 WebSocket 服务 `ws://127.0.0.1:8908/TaiWebSentry` 与 Tai 通信。若扩展无法连接，请确认 Tai 正在运行且网站统计功能已启用。

## 从源码构建

### 环境要求

- Windows 10 版本 1809（内部版本 17763）或更高版本。
- Visual Studio 2022 17.10 或更高版本，并安装 Windows App SDK 相关工作负载；或者安装匹配的 .NET 8 SDK 和 MSBuild。
- Windows App SDK `1.6.240829007`。
- WinUI 3 项目当前以 `x64` 为目标平台。

### 构建 WinUI 3 版本

```powershell
msbuild Tai.WinUI/Tai.WinUI.csproj /t:Restore /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64
msbuild Tai.WinUI/Tai.WinUI.csproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:WindowsAppSDKSelfContained=true /p:EnableMsixTooling=true
```

### 发布独立运行版本

```powershell
msbuild Tai.WinUI/Tai.WinUI.csproj /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:WindowsAppSDKSelfContained=true /p:EnableMsixTooling=true
```

发布文件默认位于：

```text
Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish
```

也可以直接在 Visual Studio 中打开 [`Tai.sln`](Tai.sln)，选择 `Tai.WinUI` 项目和 `Release | x64` 配置后进行构建。

### 自动构建

GitHub Actions 工作流位于 [`.github/workflows/winui-build.yml`](.github/workflows/winui-build.yml)，会在推送、Pull Request 或手动触发时构建 WinUI 3 版本，并生成 `tai-winui-win-x64` 构建产物。发布前还会执行资源文件和启动冒烟测试。

## 项目结构

| 目录 | 说明 |
| --- | --- |
| `Tai.WinUI` | 当前 WinUI 3 前端，使用 Fluent 风格界面 |
| `Core.Modern` | 面向 .NET 8 Windows 的核心服务构建 |
| `Core` | 应用监测、网站服务、分类、配置和 SQLite 数据服务 |
| `UI` | 旧版 WPF 前端，保留用于兼容和迁移对照 |
| `WebExtensions/Chrome` | Chrome/Chromium 浏览器扩展 |
| `Updater` | 旧版更新程序 |
| `TaiBug` | 旧版崩溃处理程序 |
| `scripts` | 构建和启动验证脚本 |

WinUI 3 前端复用了 `Core` 中的统计和数据库逻辑。`Core` 仍面向 .NET Framework 4.8，`Core.Modern` 将同一套核心源码编译到 .NET 8 Windows，以供 `Tai.WinUI` 使用。

## 数据与隐私

- 统计数据保存在程序目录下的 `Data/data.db`。
- 配置保存在程序目录下的 `Data/AppConfig.json`。
- 启动错误记录在 `Log/startup.log`。
- 网站扩展只通过本机 WebSocket 将浏览器标签页信息发送给 Tai；项目不提供账号体系、云端同步或统计数据上传功能。
- Tai 可能根据浏览器提供的网站图标地址下载 favicon，并将图标缓存到本地，用于界面展示。

删除程序目录即可卸载。若要保留统计数据，请在删除前备份 `Data` 目录。

## 许可证

本项目采用 [MIT License](LICENSE) 发布。

## 反馈与贡献

欢迎通过 [Issues](https://github.com/ethandong16/Tai/issues) 报告问题，或通过 [Pull Requests](https://github.com/ethandong16/Tai/pulls) 提交改进。
