# HKX 动画预览（血源）

## 使用

启动 `bin\Debug\MySFformat.exe`，先加载角色／装备 FLVER 或 partsbnd.dcx。

在 **3D Viewer** 的菜单选择 **Animation → Load HKX / Animation Player...**。

1. `Skeleton HKX…` 选择对应角色的 skeleton.hkx。默认寻找：
   `G:\bloodborne_pc-win\Games\dvdroot_ps4\chr\c0000-anibnd-dcx\chr\c0000\hkx\skeleton.hkx`
2. `Load HKX…` 选择动作文件。默认打开：
   `G:\bloodborne_pc-win\Games\dvdroot_ps4\chr\c0000_a00_hi-anibnd-dcx\chr\c0000\hkx\a000`
3. `Play / Pause` 播放／暂停；`Stop` 停止并回第 0 帧；左右箭头逐帧；滑块或数字框定位。
4. `Speed` 为 0.05–4 倍，`Loop` 控制循环。时间轴采用 **60 Hz 采样帧**，不是宣称游戏原文件一定是 60 FPS。
5. `Bind Pose` 清除 HKX 覆盖并显示原有静态预览状态；关闭播放器同样清除动画覆盖。

已有的 JSON Load/Edit/Export Pose 不会导出 HKX 的矩阵姿态；使用旧静态姿态功能前请关闭 HKX 播放器。

## 范围与限制

- 支持本次实测的血源 Havok 2014.1 packfile 骨架与 spline 动画，通过本地 HavokLib/HavokToolset 解码。无需安装 3ds Max。
- 选择的是**解包后的 HKX**，不是 anibnd.dcx；DCX/BND 仍用 Yabber 解包。
- HKX 骨骼与 FLVER 按**精确骨名**匹配，不假定两者索引相同；无匹配会拒绝载入。未匹配的辅助骨保留 FLVER 本地变换、跟随其父骨骼，窗口显示匹配统计。
- 这是蒙皮动画预览，不包含 Havok 布料、碰撞、游戏运行时 IK、动作混合或 TAE 事件；裙摆／披风不等于游戏中的物理效果。
- 转换保留旋转四元数以及 INDEPENDENT 缩放轨道；不经欧拉角往返转换。当前为单动画文件预览，不写回 HKX。
- 源 HKX、骨架及 FLVER 只读。转换使用独立临时目录，完成后清理；不会在游戏目录旁生成 GLB。
- 随程序携带 `tools\HavokToolset`，请勿只拷贝单个 exe。整个运行路径不需要网络。

## 本次修复的 MonoGame 窗口兼容问题

旧构造函数在 GraphicsDevice 尚未创建时设置 `Window.AllowUserResizing=true`，可引发 `WinFormsGameWindow.UpdateBackBufferSize()` 空引用。现在延迟到 `Initialize()` 完成后再启用。

播放器使用现有 `nodeUIForm.BeginInvoke` 在主编辑窗口 STA 线程创建，由主窗口持有；不使用 MonoGame 窗口作为 owner，不修改原 Viewer 线程的 apartment。主 UI Timer 发布姿态快照，MonoGame `Update` 合并并消费刷新请求，避免 Timer 线程直接重建 Viewer 顶点数组。

## 同步验证

在项目目录运行：

```bat
py -3.12 tests\run_hkx_tests.py
```

该脚本同步构建、执行 HKX 解码／骨名绑定／时间轴测试、真实 WinForms 按钮交互、真实双线程 Viewer 的菜单／播放／Resize 回归、18 张正侧背面 GPU 截图、错误输入及源文件哈希检查。依赖本机现有血源资源、两个测试 FLVER 和 Python Pillow；路径定义在脚本中。没有后台测试任务。

结果：`tests\hkx-work\test-summary.json`、`verification.json`、`player.png`、`native-contact-sheet.png`、`mod-contact-sheet.png`。

也可使用 GUI exe 的同步渲染参数：

```bat
MySFformat.exe --render model.flver --screenshot frame.png --hkx-skeleton skeleton.hkx --hkx-animation a000_104252.hkx --hkx-frame 35
```

`--hkx-frame` 是 60 Hz 采样位置，超出动作末尾时按末帧取样。CLI 不写回模型。

## 第三方组件

- PredatorCZ HavokLib / HavokToolset：https://github.com/PredatorCZ/HavokLib
- 使用官方 `HavokToolset-v1.10.19-win64.7z`，内含 `hk_to_gltf.1.10.11.spk`。
- 下载包 SHA-256：`38bc1c20263aba5385a8f1073ec42076f19b4e9e6297160489a5b2172d802d7b`（已与 GitHub release 元数据核对）。
- 许可证 GPL v3，保留于 `tools\HavokToolset\LICENSE`。FLVER Editor 原项目也是 GPL v3。
- HavokMax 是同作者的 3ds Max 插件参考；本实现使用其后续的 HavokLib 工具，不依赖 Max。

若对外发布二进制，应同时履行 GPL 对相应源码与许可证的要求，不要打包测试目录中的游戏资产。
