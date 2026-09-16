# Tai.WinUI

`Tai.WinUI` 是 Tai 当前唯一维护和发布的 Windows 桌面端，基于 .NET 8、WinUI 3 和 Windows App SDK 构建。

传统 `UI` WPF 工程已经停止维护，只保留历史源码和尚未迁移的资源；所有新功能、修复、设计调整和发布流程都应以本项目为准。

## 页面

- 概览：今日应用时长、常用应用、网站状态和本周趋势。
- 统计：日、周、月、年趋势，应用排名和分类占比。
- 详细：日期、周期、来源筛选，以及应用和网站详情。
- 分类：分类创建和自动匹配规则。
- 设置：启动、主题模式、记录规则、数据管理和版本信息。

## 架构

`Tai.WinUI` 引用 `Core.Modern`。`Core.Modern` 将 `Core` 中的共享统计和数据库源码编译为 .NET 8 Windows 程序集，从而继续使用现有 SQLite 数据、应用监测、网站统计和分类能力，而无需依赖传统 WPF 界面。

主题仅支持浅色、深色和跟随 Windows。强调色由应用固定管理，并针对两种主题分别设置前景色和控件填充色，以保持文字、按钮、选中状态和图表的可视性。

## 要求

- Windows 10 1809（17763）或更高版本。
- .NET 8 SDK。
- Windows App SDK `1.6.240829007`。
- `x64` 构建目标。

## 构建

```powershell
dotnet restore Tai.WinUI/Tai.WinUI.csproj -r win-x64

dotnet publish Tai.WinUI/Tai.WinUI.csproj `
  -c Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:SelfContained=true `
  -p:WindowsAppSDKSelfContained=true `
  -p:EnableMsixTooling=true `
  --no-restore
```

发布后执行仓库根目录下的启动测试：

```powershell
powershell -ExecutionPolicy Bypass `
  -File scripts/Test-WinUIStartup.ps1 `
  -PublishDirectory "Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish"
```

GitHub Actions 工作流 [`.github/workflows/winui-build.yml`](../.github/workflows/winui-build.yml) 会生成名为 `tai-winui-win-x64` 的自包含构建产物。
