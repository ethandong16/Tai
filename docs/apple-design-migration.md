# Apple Design → WinUI 3

2026-09-21：把已确认的独立网页 demo 迁移为原生 WinUI 3 界面。

## 已迁移

- Acrylic 侧栏、Tai 品牌区、页面标题、16px 内容卡片和浅色 / 深色配色。
- 概览的使用摘要、趋势与分类双栏、应用和网站列表；窄窗口切换到纵向布局。
- 原生柱状趋势图，支持键盘聚焦、点击查看时长；未来时段不显示为零时长。
- 原生分类环图，基于实际分类数据绘制，提供无记录状态与辅助技术摘要。
- 统计周期分段控件、记录表面、分类管理表面、设置与详情卡片。
- 记录页的名称 / 分类搜索；概览直接打开应用或网站详情，“查看全部”保留来源筛选。
- 修复详情导航时侧栏同步选中项导致二次导航的问题，以及返回记录页时的标题同步。

## 原生适配

保留 Windows 标题栏、原 Tai 图标、五个设置分区，以及现有采集、数据库、分类规则、导入导出业务。没有引入 WebView、网页运行时或 demo 数据。

分类沿用现有“应用与网站合计”口径，并明确显示该说明；概览主时长仍仅统计应用，不把网站重复计入。Acrylic 的透明度回退和标准控件的交互由 WinUI / Windows 管理。页面主导航不增加大幅位移动画。

文本使用对比度更高的蓝色 #2463CC，图表使用 #628AF0。深色模式分别使用 #8EB6FF 和 #346FD1；现有文字和按钮对比度检查继续执行。

## 构建和验证

```powershell
dotnet publish Tai.WinUI/Tai.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:EnableMsixTooling=true --no-restore -o artifacts/apple-design-winui
./scripts/Test-WinUIStartup.ps1 -PublishDirectory artifacts/apple-design-winui
```

启动检查在新的临时副本及空数据库中运行，不使用已有用户数据。覆盖七个页面、1240 / 920 / 700 / 480 窗口宽度、主题、对比度、日期范围、周期选择、详情导航、来源筛选、标题恢复、图标和数据库查询。通过后截图及启动标记保存在测试副本；本次交付的截图副本位于 `artifacts/apple-design-qa`。截图中的空记录来自新建测试数据库。

运行入口为 `artifacts/apple-design-winui/Tai.WinUI.exe`，需保留同目录依赖文件。构建保留原有 SQLite.Linq / WebSocketSharp 的 NU1701 兼容性警告。
