# EVE-O Preview 简体中文说明

---

## 概述

本应用程序旨在提供一种简单的方式，用于同时监控多个正在运行的 EVE Online 客户端，并可在它们之间快速切换。

运行时，程序会为每个活动中的 EVE Online 客户端显示一个实时缩略图。这些缩略图允许通过鼠标或可自定义快捷键快速切换到对应的 EVE Online 客户端。

本质上，它是一个任务切换器。它不会转发任何键盘或鼠标事件等输入行为。  
本程序支持 EVE、通过 Steam 启动的 EVE，或两者混合使用。

本程序 **不会（且永远不会）** 执行以下行为：

- 修改 EVE Online 界面
- 显示修改后的 EVE Online 界面
- 广播任何键盘或鼠标事件
- 以任何方式与 EVE Online 交互（除将其窗口置于前台或调整大小/最小化外）

---

**在任何情况下，请勿使用 EVE-O Preview 执行违反 EVE Online EULA 或 ToS 的操作。**

如果您发现 EVE-O Preview 的某些功能或功能组合可能导致违反 EULA 或 ToS 的行为，请将其视为一个 Bug，并通过游戏内邮件联系开发者（Aura Asuna）进行反馈。

---

## 安装与使用方法

1. 下载并解压 .zip 文件到任意目录（例如桌面、CCP 文件夹等）
   - **注意**：请勿安装到 *Program Files* 或 *Program Files (x86)* 目录。  
     这些目录通常不允许程序写入文件，而 EVE-O Preview 会将配置文件存储在可执行文件所在目录，因此需要写入权限。
2. 启动 EVE-O Preview 和您的 EVE 客户端（启动顺序不限）
3. 根据需要调整设置（详见下方说明）

视频教程：

- [Eve online , How To : EVE-O Preview (multiboxing; legal)](https://youtu.be/2r0NMKbogXU)

---

## 系统要求

- Windows 7、Windows 8/8.1、Windows 10
- Microsoft .NET Framework 4.6.2 及以上版本
- EVE 客户端显示模式必须设置为：
  - **Fixed Window（固定窗口）**
  - 或 **Window Mode（窗口模式）**
- 不支持 **Fullscreen（全屏）模式**

---

## 关于 EVE Online EULA/ToS

本应用程序在当前功能范围内符合 EULA/ToS 规定：

CCP FoxFour 表示：

> 该软件的合法性已经讨论过，无需再次讨论。  
> 只要软件功能不发生变化，其当前状态是被允许的。

CCP Grimmi 表示：

> 以只读方式显示完整、未修改的 EVE 客户端实例的叠加窗口（无论缩放大小），如当前 EVE-O Preview 所实现的方式，是允许的。  
> 这些叠加窗口不允许直接与客户端交互，必须将对应客户端窗口置于前台后才能进行操作。

---

## 应用程序选项

### General（常规）选项卡

| 选项 | 说明 |
|------|------|
| Minimize to System Tray | 关闭主窗口时是否最小化到系统托盘 |
| Track client locations | 激活或启动客户端时是否恢复窗口位置 |
| Hide preview of active EVE client | 是否隐藏当前激活客户端的缩略图 |
| Minimize inactive EVE clients | 自动最小化未激活客户端以节省 CPU/GPU |
| Previews always on top | 缩略图是否始终置顶 |
| Hide previews when EVE client is not active | 非激活状态时是否隐藏所有缩略图 |
| Unique layout for each EVE client | 是否为不同客户端使用不同缩略图布局 |

---

### Thumbnail（缩略图）选项卡

| 选项 | 说明 |
|------|------|
| Opacity | 非活动缩略图透明度（20% ~ 100%） |
| Thumbnail Width | 缩略图宽度（100~640） |
| Thumbnail Height | 缩略图高度（80~400） |

---

### Zoom（缩放）选项卡

| 选项 | 说明 |
|------|------|
| Zoom on hover | 鼠标悬停时是否放大缩略图 |
| Zoom factor | 放大倍数（2~10） |
| Zoom anchor | 缩放起始位置 |

---

### Overlay（叠加）选项卡

| 选项 | 说明 |
|------|------|
| Show overlay | 是否显示客户端名称 |
| Show frames | 是否显示窗口标题和边框 |
| Highlight active client | 是否高亮当前客户端 |
| Color | 高亮边框颜色 |

---

### Active Clients（活动客户端）选项卡

| 选项 | 说明 |
|------|------|
| Thumbnails list | 当前客户端缩略图列表。取消勾选可隐藏缩略图（仅当前会话有效） |

---

## 鼠标手势与操作

鼠标操作作用于当前悬停的缩略图窗口。

| 操作 | 手势 |
|------|------|
| 激活客户端 | 单击缩略图 |
| 最小化客户端 | Ctrl + 单击 |
| 切换到最后使用的非 EVE 应用 | Ctrl + Shift + 单击 |
| 移动缩略图 | 右键拖动 |
| 调整高度 | 同时按左右键并上下移动 |
| 调整宽度 | 同时按左右键并左右移动 |

---

## 仅配置文件可设置的选项

某些选项无法通过 GUI 设置，需直接编辑配置文件。

**注意：修改配置前请关闭 EVE-O Preview，并备份文件。**

- **ActiveClientHighlightThickness**  
  活动客户端高亮边框厚度（1~6，默认 3）

- **CompatibilityMode**  
  启用兼容渲染模式（默认 false）

- **EnableThumbnailSnap**  
  是否启用缩略图吸附（默认 true）

- **HideThumbnailsDelay**  
  隐藏延迟（默认 2 = 1 秒）

- **PriorityClients**  
  不自动最小化的客户端列表

- **ThumbnailMinimumSize**  
  最小尺寸（默认 "100, 80"）

- **ThumbnailMaximumSize**  
  最大尺寸（默认 "640, 400"）

- **ThumbnailRefreshPeriod**  
  刷新周期（300~1000ms，默认 500ms）

---

## 快捷键设置

可通过编辑配置文件设置快捷键。  
请在修改前备份文件。

示例：

```
"ClientHotkey": {
  "EVE - Phrynohyas Tig-Rah": "F1",
  "EVE - Ondatra Patrouette": "Control+Shift+F4"
}
```

⚠ 不要使用已在 EVE 中使用的快捷键组合。

---

## 客户端循环切换（Cycle Groups）

可设置循环分组与前后切换快捷键。

⚠ 每个 Description 必须唯一  
⚠ ClientsOrder 顺序编号必须唯一  

推荐使用非常规功能键（如 F14+），并绑定到游戏外设。

---

## 每个客户端独立边框颜色

可在配置文件中设置：

```
"PerClientActiveClientHighlightColor": {
  "EVE - Example Toon 1": "Red",
  "EVE - Example Toon 2": "Green"
}
```

未设置的客户端使用全局高亮颜色。

---

## 兼容模式

启用备用缩略图渲染模式（基于截图）。

优点：
- 支持远程桌面

缺点：
- 占用更多内存
- 刷新率仅 1 FPS
- 可能出现鼠标卡顿

---

## 致谢

### 当前维护者
- Aura Asuna

### 创建者
- StinkRay

### 贡献者
- CCP FoxFour

论坛链接：
https://forums.eveonline.com/t/4202

原始仓库：
https://bitbucket.org/ulph/eve-o-preview-git

---

## CCP 版权声明

EVE Online 及相关商标归 CCP hf. 所有。  
本程序与 CCP hf. 无官方关联或背书。