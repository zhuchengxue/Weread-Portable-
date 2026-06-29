# 微信读书贴边阅读器

轻量、便携的 Windows WebView2 微信读书小工具。

## 下载与运行

下载 `WeReadSideReader-Portable.zip`，解压后双击：

```text
WeReadReader.exe
```

系统需要 Microsoft Edge WebView2 Runtime，Windows 10/11 通常已经自带。

建议右键 `WeReadReader.exe`，固定到任务栏：

- 第一次点击：打开阅读器。
- 窗口显示时再次点击任务栏图标：最小化，任务栏图标不会消失。
- 最小化后再点击任务栏图标：恢复原窗口。
- 始终只有一个程序实例，不会重复打开多个窗口。

## 功能

- 使用微信读书官方网页和官方图标。
- 使用自绘标题栏：左侧微信读书图标和名称，中间区域可拖动窗口。
- 右侧只保留三横线菜单、最小化和关闭。
- 返回、首页、刷新、字号和自动隐藏统一放在三横线菜单中。
- 阅读时向下滚动会自动隐藏标题栏，向上滚动会重新显示。
- `A-` / `A+`：缩放字号并自动记忆。
- `Auto`：支持多显示器上的左侧、右侧、顶部贴边自动隐藏；隐藏时仅平移窗口，不改变阅读区域尺寸。
- `Esc`：最小化到任务栏。
- `Ctrl+Shift+Y`：全局最小化/恢复。
- 自动保存窗口位置、尺寸、缩放、Auto 状态和上次阅读地址。
- 托盘左键：显示/最小化切换。
- 托盘右键菜单支持显示、最小化、首页、刷新和退出。

## 任务栏

新版使用固定的 Windows AppUserModelID。升级旧版本后，请先取消固定旧图标，再右键新版 `WeReadReader.exe` 重新固定到任务栏，否则 Windows 可能继续使用旧快捷方式和旧图标缓存。

## Portable 数据

- `profile/`：登录状态和网页缓存。
- `settings.json`：窗口及阅读设置。
- `lib/`：精简 WebView2 控件依赖。

整个文件夹可以复制到其他位置使用。删除 `settings.json` 可重置界面设置；删除 `profile/` 可清除登录状态。

## 开发

- `WeReadReader.cs`：C# 主程序源码。
- `Build-Portable.ps1`：重新编译 EXE。
- `Build-Portable.ps1 -Package`：编译并生成干净的 portable 压缩包。
- `Install-Dependencies.ps1`：重新下载固定版本的 WebView2 编译依赖。
