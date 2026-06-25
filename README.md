# 微信读书贴边阅读器

轻量、便携的 Windows WebView2 微信读书小工具。

## 下载与运行

下载 `WeReadSideReader-Portable.zip`，解压后双击：

```text
WeReadReader.exe
```

系统需要 Microsoft Edge WebView2 Runtime，Windows 10/11 通常已经自带。

## 功能

- 使用微信读书官方网页，登录状态保存在本地 `profile/`。
- 记忆窗口位置、尺寸、字号、自动隐藏状态和上次阅读地址。
- 支持左侧、右侧、顶部贴边自动隐藏。
- 贴边隐藏只平移窗口，不改变 WebView 尺寸，降低闪烁和阅读进度丢失风险。
- 下滑时自动隐藏顶部工具栏，上滑时恢复。
- 三横线菜单提供返回、首页、刷新、字号调整和自动隐藏设置。
- 任务栏图标支持原生显示、最小化和恢复。
- 托盘左键以及 `Ctrl+Shift+Y` 支持显示/最小化切换。
- 单实例运行，重复启动会切换现有窗口。

## Portable 数据

- `profile/`：登录状态和网页缓存，由程序首次运行时创建。
- `settings.json`：窗口与阅读设置，由程序首次运行时创建。
- `lib/`：WebView2 控件依赖。

删除 `settings.json` 可重置界面设置；删除 `profile/` 可清除登录状态。

## 从源码构建

```powershell
.\Build-Portable.ps1
```

编译环境为 Windows、.NET Framework 4.8 和 Microsoft Edge WebView2 Runtime。
