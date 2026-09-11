# Tai.WinUI

WinUI 3 frontend for Tai. This project replaces the WPF presentation layer with a Fluent-style shell while the existing `UI` project remains available during migration.

## Pages

- 概览：今日统计卡片、常用应用和网站、本周活动
- 统计：周期切换、趋势图、应用排名和分类占比
- 详细：日期筛选、类型筛选、使用记录列表
- 分类：分类占比和自动分类设置
- 设置：启动、外观、数据和版本信息

## Requirements

- Windows 10 1809 (17763) or later
- Visual Studio 2022 17.10 or later with the Windows App SDK workload, or a matching .NET 8 SDK
- Windows App SDK 1.6.240829007

The original `Core` project still targets .NET Framework 4.8 for compatibility with the legacy WPF app. `Core.Modern` recompiles the same source for .NET 8 Windows and is referenced by this project. The overview, statistics and detail list use an `IUsageDataProvider` adapter backed by the existing SQLite services, with demo rows as a fallback when no database is available. The statistics page shares the same refreshable view model and the detail page supports application/website filtering. Category editing and the remaining settings actions are the next service-integration steps.

## GitHub Actions

`.github/workflows/winui-build.yml` builds on `windows-2022` with Visual Studio MSBuild and .NET 8, then uploads a self-contained `win-x64` artifact named `tai-winui-win-x64`. This avoids depending on the local machine's Visual Studio or SDK installation.
