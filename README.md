# BootUptime - 电脑启动时间

轻量级 Windows 桌面小工具，实时显示系统开机运行时长，数据源与任务管理器一致。

无边框半透明挂件，纯 C# / WinForms 实现，**零第三方依赖**。

## 功能

### 显示

- 实时显示系统运行时间，数据源为 `Environment.TickCount64`，与任务管理器「性能」选项卡完全一致
- 格式自适应：`3天 12小时 28分钟 15秒` → 不足一天则省略「天」，依此类推
- 每秒刷新一次

### 窗口

- 无边框 + 半透明（Opacity 0.85）
- **任意位置**左键按住即可拖动（含时间文字区域）
- 拖动窗口边缘 6px 内可缩放，手感与原生窗口一致
- 窗口置顶可切换，默认开启
- 窗口位置与置顶状态自动记忆，下次启动原样恢复
- 高 DPI 适配：缩放热区、最小尺寸按当前 DPI 换算

### 系统集成

- 开机自启（写入「启动」文件夹快捷方式，**无需管理员权限**，不写注册表）
- 一键创建 / 移除桌面快捷方式
- 单实例保护：反复双击不会开出一堆挂件
- 崩溃日志：任何未捕获异常都会落盘，不会"双击没反应"

## 使用方法

1. 下载并运行 `BootUptime.exe`
2. **点击左上角齿轮 ⚙ 图标**弹出菜单（窗口无边框，没有系统标题栏）
3. 左键拖动可调整位置，拖动边缘可缩放

## 菜单

| 菜单项 | 说明 |
| --- | --- |
| 开机自启 | 勾选状态，写入 / 移除启动文件夹快捷方式 |
| 窗口置顶 | 勾选状态，关闭后窗口不再压在其他窗口之上 |
| 桌面快捷方式 | 勾选状态，在桌面创建 / 删除快捷方式 |
| 关闭程序 | 彻底退出 |

## 依赖

- Windows 10 / 11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- 无需其他依赖

## 构建

```bash
# 框架依赖（体积小，需装 .NET 9 Desktop Runtime）
dotnet build -c Release

# 自包含单文件（体积大，免运行时依赖）
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true
```

产物：`bin/Release/net9.0-windows/BootUptime.exe`

### 冒烟自检

改完 UI 代码后建议跑一次（窗口仅闪现数十毫秒，退出码 0 表示通过）：

```bash
BootUptime.exe --selftest
```

输出示例（本机 125% 缩放实测）：

```
SELFTEST OK
  DeviceDpi   = 120
  ClientSize  = 300 x 52
  Location    = 1610, 60
  Opacity     = 0.85
  TopMost     = True
  Controls    = ⚙ 33x23 @4,4 | 4小时 35分钟 47秒 218x32 @41,10
```

> 齿轮图标在控制台里可能显示为 `?`（编码所致），界面内显示正常。

## 项目结构

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | 入口；单实例互斥、崩溃日志、`--selftest` 自检 |
| `MainForm.cs` | 主窗口：外观、拖拽缩放、齿轮菜单、位置记忆 |
| `UptimeService.cs` | 系统运行时间获取与格式化 |
| `StartupManager.cs` | 开机自启 / 桌面快捷方式（WScript.Shell） |

## 配置文件

| 路径 | 说明 |
| --- | --- |
| `%LOCALAPPDATA%\BootUptime\settings.json` | 窗口位置、置顶状态 |
| `%LOCALAPPDATA%\BootUptime\error.log` | 崩溃日志（程序正常时不生成） |

## 许可

本项目基于 MIT 许可证开源，具体条款见 [LICENSE](LICENSE) 文件。
使用前请阅读 [免责声明](DISCLAIMER.md)。
