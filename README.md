# HOI4 Mod Companion / 《钢铁雄心4》模组简报

一个面向《Hearts of Iron IV》的轻量 Windows 伴随窗口。它会读取当前实际启用的 MOD，并用中文快速说明每个模组是做什么的。

## 功能

- 不注入游戏进程，不修改 `hoi4.exe`。
- 以 `Documents\Paradox Interactive\Hearts of Iron IV\dlc_load.json` 为当前实际启用 MOD 的真源。
- 独立桌面窗口，可拖动、可调整大小、可放到第二块屏幕；位置和尺寸会自动保存。
- 随 `hoi4.exe` 启动而启动，游戏退出后自动关闭；切换到浏览器或桌面时不会隐藏。
- 后台扫描 Paradox Launcher 已注册的全部 MOD，提前补全说明，而不只处理当前 Playset。
- Workshop 模组优先读取 Steam Workshop 作者详情；没有中文说明时自动在线翻译为简体中文并缓存。
- 本地 MOD 优先读取自己的简体中文本地化、README 和 docs。
- 人工整理的高质量说明优先于自动生成缓存。
- 点击模组条目可以查看更详细说明和来源。

## 下载和安装

前往 GitHub Releases 下载最新的 Windows ZIP，解压到一个可写目录。

### 推荐：随 HOI4 自动启动

在解压目录打开 PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\install_watcher_task.ps1
```

它会注册 Windows 登录计划任务 `HOI4 Mod Overlay Watcher`。后台 Watcher 平时不显示窗口，只监测 `hoi4.exe`；无论从 Steam、Paradox Launcher、快捷方式还是直接运行游戏，只要检测到 HOI4，伴随窗口就会自动启动。

卸载自动启动：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\uninstall_watcher_task.ps1
```

### 手动运行

```powershell
.\Hoi4ModOverlay.exe
```

UI 预览：

```powershell
.\Hoi4ModOverlay.exe --preview
```

备用的“同时启动游戏”模式：

```powershell
.\Hoi4ModOverlay.exe --launch-game
```

如 HOI4 安装在非标准位置，可以设置环境变量 `HOI4_EXE` 指向 `hoi4.exe`；否则工具尝试标准 Steam 路径，最后回退到 Steam URI。

## 中文说明如何生成

说明获取有明确优先级：

1. 项目自带的人工中文缓存 `data/mod_summaries.zh-CN.json`。
2. 已生成的本地缓存 `data/generated_summaries.zh-CN.json`。
3. Workshop MOD：Steam Workshop 作者详情页。
4. 本地 MOD：简体中文本地化、README、docs。

当 Workshop 作者说明不是中文时，程序会把**公开的 Workshop 说明文本**发送给在线翻译服务转换成简体中文，然后将结果保存在本地缓存中。不会把你的存档或其他本地文件发送给翻译服务。

## 数据位置

HOI4 用户目录通常是：

```text
%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV
```

程序主要读取：

```text
dlc_load.json
mod\*.mod
```

窗口位置、自动生成说明和运行日志保存在程序目录下的 `data` / `logs` 中。

## 构建

要求：Windows + .NET 10 SDK。

```powershell
dotnet restore --configfile NuGet.Config
dotnet build -c Release --no-restore
dotnet publish -c Release --no-restore -o dist
```

项目也提供：

```text
tools\build_release.ps1
tools\build_release.cmd
```

## 项目结构

```text
MainWindow.xaml / .cs           独立伴随窗口
Services/ModCatalogService.cs   读取 dlc_load.json 与 MOD descriptor
Services/ModSummaryEnrichmentService.cs
                               Steam 详情 / 中文说明补全与缓存
Services/OverlaySettingsService.cs
                               窗口位置和尺寸持久化
tools/watch_hoi4.ps1            HOI4 进程监视器
tools/install_watcher_task.ps1  安装自动启动计划任务
tools/uninstall_watcher_task.ps1
                               卸载自动启动计划任务
data/mod_summaries.zh-CN.json   人工中文说明缓存
```

## 说明

这是一个非官方社区工具，与 Paradox Interactive / Steam 无隶属关系。模组名称和 Workshop 页面内容归各自作者所有。
