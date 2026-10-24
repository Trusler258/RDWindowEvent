# RDWindowEvent for A Dance of Fire and Ice

将 Rhythm Doctor 的窗口事件移植到 A Dance of Fire and Ice 的关卡编辑器中

安装后，编辑器会出现独立的「窗口事件」分类，可以像添加原生事件一样添加：

- `Window Move`：移动 / 摇摆 / 缠绕 / 椭圆 / 震动
- `Window Resize`：缩放窗口，支持枢轴、边缘锚点和镜头缩放

事件参数与 Rhythm Doctor 保持一致，支持中文与英文界面

## 技术栈

| <img src="assets/dotnet.svg" width="28" height="28" alt=".NET"><br>C# | <img src="assets/unity.svg" width="28" height="28" alt="Unity"><br>Unity | <img src="assets/windows.svg" width="28" height="28" alt="Windows"><br>Windows | <img src="assets/deepseek.svg" width="28" height="28" alt="DeepSeek"><br>DeepSeek |
| :---: | :---: | :---: | :---: |
| .NET Framework 4.8 | UnityModManager 0.32.5 | Win32 API | V4.1 Flash |

> **与 7th Beat Games 的关系**
>
> 本项目是非官方社区模组，与 7th Beat Games 无隶属、授权或合作关系
> Rhythm Doctor 及其窗口事件设计归 7th Beat Games 所有
> 本项目不包含 Rhythm Doctor 的代码、图片或音频资源

## 功能

### Window Move

| 预设 | 说明 | 主要参数 |
| --- | --- | --- |
| Move | 移动到指定位置 | position、reference、duration、ease |
| Sway | 沿指定角度往返摆动 | amplitude、angle、frequency、subEase、easeType |
| Wrap | 在屏幕边缘循环漂移 | speed、amplitude、angle、frequency |
| Ellipse | 圆周或椭圆轨道 | speed、useCircle、amplitude、amplitudeVector、angle |
| ShakePer | 周期性随机震动 | amplitudeVector、frequency、period、subEase |

- `samePresetBehavior` 为 `Keep` 时，沿用同预设的速度与频率，与 Rhythm Doctor 行为一致
- `reference` 为 `Edge` 时，窗口边缘对齐屏幕边缘
- 支持自定义缓动曲线（Modern 菜单风格）

### Window Resize

- 独立的 X/Y 缩放比例，范围 0.1 到 2.0
- `zoomMode`：`None`、`Fill`、`Fit`
- `pivotMode`：`Default`（自定义枢轴百分比）或 `AnchorEdge`（左 / 右 / 下 / 上边缘固定）
- 缩放过程可与移动、摇摆、缠绕等预设叠加
- 支持自定义缓动曲线（Modern 菜单风格）

### 其他

- 独立「窗口事件」分类，不占用原生分类的 11 个按钮上限
- 事件保存、重载、撤销重做、事件标签、多语言
- 全屏状态下播放时会自动切换为窗口模式
- 关卡开始前捕获窗口基准尺寸，暂停或重开时恢复原窗口
- 游戏内修改分辨率后自动更新缩放基准
- 完整图形曲线编辑器，`Ctrl+Shift+E` 打开

## 环境要求

- Windows
- A Dance of Fire and Ice（在 r149 环境测试）
- UnityModManager 0.32.5 或更高
- .NET Framework 4.8

## 安装

1. 安装 UnityModManager
2. 下载 Release 中的 `RDWindowEvent-0.1.1-beta.zip`
3. 解压到游戏的 `Mods` 目录，确保路径为：

```text
A Dance of Fire and Ice/Mods/RDWindowEvent/RDWindowEvent.dll
A Dance of Fire and Ice/Mods/RDWindowEvent/Info.json
```

4. 启动游戏，在 UMM 中确认 `RDWindowEvent` 已启用

## 使用

1. 打开关卡编辑器
2. 在左侧事件栏选择「窗口事件」分类
3. 点击 `Window Move` 或 `Window Resize` 添加事件
4. 在右侧检查器调整参数

### UMM 设置

| 设置 | 说明 |
| --- | --- |
| Language | Auto / 中文 / English |
| Event Menu | RD compact（精简）/ Modern（完整） |
| Resize Base | 关卡开始时的窗口尺寸 / 固定分辨率 |
| Base Width / Base Height | 固定分辨率基准 |
| Rebase On Resolution Change | 游戏内改分辨率后更新缩放基准 |
| Min Window | 最小窗口尺寸，默认 704x396（取自 Rhythm Doctor） |

语言和事件菜单风格修改后需要重启游戏

`RD compact` 会隐藏 ADOFAI 专属字段、说明文字和自定义曲线，参数数量更接近 Rhythm Doctor；`Modern` 显示全部字段与曲线编辑器

### 自定义曲线

在 Modern 风格下：

1. 选中 Window Move 或 Window Resize 事件
2. 点击 `Edit Ease Curve`，或按 `Ctrl+Shift+E`
3. 双击添加控制点，拖动调整，右键删除中间点
4. 保存后将曲线写入 `customEaseData`

RD compact 风格下不显示曲线编辑器，并按 `ease` 参数执行


## 从源码构建

项目需要引用游戏自身的托管程序集
通过环境变量或 MSBuild 属性指定游戏目录：

```powershell
$env:ADOFAI_GAME_DIR = "C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice"
dotnet build -c Release
```

也可以直接传参：

```powershell
dotnet build -c Release -p:GameDir="D:\Games\A Dance of Fire and Ice"
```

构建完成后会自动复制 DLL 与 `Info.json` 到 `Mods\RDWindowEvent`

### 打包

```powershell
dotnet build -c Release -p:GameDir="游戏目录"
```

将 `bin\Release\RDWindowEvent.dll` 与 `Info.json` 放入 `RDWindowEvent` 文件夹后压缩即可

## 实现说明

- ADOFAI 的 `LevelEventType` 枚举不可扩展
- `Window Move` 使用枚举范围外的值 `101`，`Window Resize` 使用 `100`
- 保存时自动写入 `requiredMods`，缺少模组时由游戏提示需要第三方模组
- 窗口操作使用 Win32 `SetWindowPos`，由 `WindowDanceController` 每帧计算目标位置与尺寸
- 分类页签、事件按钮、检查器面板、事件图标均通过 Harmony 补丁接入编辑器
- 不覆盖原生 MiscSettings 事件定义，避免影响原版事件

## 已知限制

- 仅支持 Windows，依赖 Win32 API
- 高速 Wrap 预设下窗口可能短暂移出屏幕边缘，这是循环漂移的预期行为
- 自定义曲线只在 Modern 风格生效
- 多显示器环境下，窗口在当前显示器范围内移动
- 语言和事件菜单风格的切换需要重启游戏

## 许可证

MIT License，详见 [LICENSE](LICENSE)

## 致谢

- Rhythm Doctor 的窗口事件设计来自 7th Beat Games
- 本项目为独立实现的 ADOFAI 移植版本，不包含 Rhythm Doctor 的代码或资源

<img src="https://raw.githubusercontent.com/Trusler258/RDWindowEvent/main/assets/credit.svg" width="420" alt="当前版本：Beta v0.1.1 · Trusler &amp; DeepSeek V4.1 Flash">
