# FLVER Editor 开发交接（2026-10-13）

本文是后续 AI 在本仓库工作的项目约束和交接记录。实际源码优先于过期记录；开始修改前先看 `git status`、相关代码及 `cloth-and-animation-README.md`。不要把建议中的后续任务当成用户已授权的全部工作。

## 1. 用户目标与不可破坏的边界

- 当前重点：把独立 **Cloth** 窗口从查看器提升为可用的布料编辑器，并提供真实的原生物理预览；最终希望装备能在 FS 游戏间适配。离线预览通过不等于跨游戏无损或游戏验证通过。
- 保留原始模型、HKX、CLM2、MOD、存档、历史候选和用户修改。普通导出写新文件，不覆盖来源或已存在文件。
- **绝不自动启动或操作游戏**，不注入游戏进程，不修改存档。游戏效果由用户自己测试。
- 不杀用户的 FLVER Editor。如果构建遇到文件锁，交互时请用户退出；用户明确要求不打扰时，将结果留在独立构建目录并说明未部署，不强制关闭进程。
- 用户不喜欢每次小改动都跑一整套测试。做与改动对应的定向检查；不要把没跑完的全套回归说成通过。
- 不回滚或删除非本次任务产生的工作，不主动提交/推送，除非用户要求。
- UI 保持英文，重要控件提供英中双语 tooltip。Cloth 独立窗口及主界面 Material/Mesh/Dummy 旁的 Cloth 按钮保持；**第二行 CLM2 操作保留**。
- Java 历史文件、机器专属测试、证据和非发行工具留在 Git 忽略的 `helper-ext`；不要重新塞入发行目录。必要 schema、许可、credits 和源码不能删。正常读写/构建不依赖 Java、Python 或 HCT DLL。
- 主 README 链接 `cloth-and-animation-README.md`，credits 保持最后，X2.7 同时说明动画和 Cloth。不要添加多余 Java 讨论。
- 若另启动 Vite/网页预览服务，默认在可见前台终端运行，不隐藏到后台。

## 2. 接手时仓库状态

2026-10-13 写本文前观察到：

```text
HEAD: 61dc7a8 update more cloth inspector feature
previous: 01277f8 X2.7 Cloth and animation update
```

当时源代码不再显示为未提交；仍有以下已有修改，应保护：

```text
MySFformat/.vs/MySFformat.csproj.dtbcache.json
MySFformat/tests/run_hkx_tests.py
MySFformat/tests/verify_bloodborne_textures.py
MySFformat/tests/verify_hkx_render.py
```

这是状态快照，不代表它们可丢弃，也不代表 AI 完成过提交。后续请重新检查。

## 3. 关键源码与文档

| 路径 | 职责 |
|---|---|
| `MySFformat/src/ClothInspectorForm.cs` | 独立窗口、布局、加载/导出、CLM2 自动配套、播放生命周期、ClothCanvas 基础绘制 |
| `MySFformat/src/Cloth/Editing/ClothEditing.cs` | partial Form：暂存 XML、可编辑表格、批量修改、Apply、Undo/Redo、相似选择 |
| `MySFformat/src/Cloth/Editing/ClothSelection.cs` | partial Canvas：单选/框选/圆形刷选、三角面遮挡判定、平移/旋转/聚焦 |
| `MySFformat/src/ClothDocument.cs` | HKX/XML/Binder 加载、domain/reference/rest 数据、语义检查、另存 |
| `MySFformat/src/HkxPack/HkxPackfileCodec.cs` | BB/DS3 原生 C# 编解码；Pack 内含完整字段 readback gate |
| `MySFformat/src/Clm2/Clm2Document.cs` | CLM2 文档、LoadCompanion、保护源文件、布局/绑定检查 |
| `MySFformat/src/Cloth/Preview/ClothPreviewSession.cs` | 编辑器端快照、builder/host 启动、命名管道、拥有的子进程回收 |
| `MySFformat/src/Cloth/Preview/NativeSimulation.cs` | 独立宿主内的原生加载/绑定/初始化/解算/位置读回 |
| `MySFformat/src/Cloth/Preview/HostEnvironment.cs` | 原生 HCT 风控件包装调用、参数读回、退出恢复 |
| `MySFformat/src/Cloth/Preview/PreviewEnvironment.cs` | 预览环境参数及校验 |
| `MySFformat/src/Cloth/Preview/PreviewAssetBuilder.cs` | 临时 scene/运行 HKX/FLVER/CLM2 适配；重力覆写只改临时副本 |
| `MySFformat/src/Cloth/Preview/HostNetworkGuard.cs` | 子进程内拒绝厂商 socket；不改厂商文件/系统防火墙 |
| `MySFformat/src/Cloth/Preview/PreviewBuild.targets` | C# helper/schema 构建及部署，不依赖 Python |
| `MySFformat/src/Cloth/Preview/README.md` | 宿主/协议/风修正的技术说明 |
| `MySFformat/src/HkxPlaybackForm.cs` | 现有动画播放；未来 Cloth 动画输入的潜在联动点 |
| `MySFformat/src/Program.cs` | FLVER 编辑/刷新时使预览快照失效的 hook |
| `MySFformat/MySFformat.csproj` | partial 编辑代码、客户端及 PreviewBuild.targets 接入 |
| `MySFformat/tools/Cloth/` | BB/DS3 schema 等发行所需资源 |
| `cloth-and-animation-README.md` | 用户操作、支持范围、编辑和风行为说明 |

新的 HKX 代码放 `src/HkxPack`，CLM2 放 `src/Clm2`，Cloth 编辑放 `src/Cloth/Editing`，宿主/预览放 `src/Cloth/Preview`。

## 4. 已实现的交互设计（用户明确认可）

### 节点、镜头、界面

- 左键：Click / Box / **Circle Brush**。用户要的是圆形刷选，不是沿拓扑的一圈 edge-loop。
- 刷选半径可调；Ctrl/Shift 添加，Alt 移除。可见节点选择按 Cloth 三角面的深度遮挡判定；关闭 Visible only 则穿透选背面，参考碰撞体不遮挡节点选择。
- All / Invert / Clear / Focus；画布与粒子表格多选双向同步。粒子表格禁排序，避免行号与粒子 ID 混淆。
- **中键拖动平移**、右键拖动旋转、滚轮缩放、F 聚焦。
- Select Similar：按任一选中 seed 的固定/动态状态、Range、逆质量、作者 Y 高度或共同约束集选择；容差是绝对值；限定当前模拟域，不猜逐粒子蒙皮权重。共同约束集可能选中很大一片，不等于拓扑邻接/环选。
- 环境移到 Preview Environment 页；高级按钮叫 **Edit HKX XML…**；导出 Range 倍率控件移到 Raw XML 页。CLM2 行仍保留。

### 作者编辑和 Apply

- `stagedXml` 是待提交副本；表格编辑与批量 Modify 不立即改 `Document.Xml`。
- Parameters / Binding：作者重力 `(x y z 0)`、阻尼/碰撞容差、支持的布尔项、solver 次数/substeps 等。SimulationInfo 的 subSteps=0 是允许的默认值，不能与 Solver 的最小次数混为一谈。
- Particles / Range：动态粒子的 mass（同步 reciprocal invMass）、radius、friction、已有 LocalRange maximumDistance。设置 Range 会更新该粒子所有已有 LocalRange 记录，不凭空创建记录。
- Constraints：点上方记录，下面编辑已知 stiffness/bendStiffness/restLength/compressionLength/maximumDistance；索引、派生 bend 权重/曲率及未知字段只读。
- 固定粒子的质量、固定状态、inverse mass、collision masks、绑定、拓扑当前只读。Colliders 暂仍主要是查看/Raw XML，不要宣称已实现拖动编辑。
- **Apply Changes**：验证暂存文档、原生 Pack 全字段读回，再提交内存；失败不改作者/源文件。Discard Pending 丢弃未 Apply 修改；Undo/Redo 恢复已应用作者快照。
- **播放中 Apply 自动重建并继续播放；暂停中 Apply 重建后保持暂停；停止中 Apply 只更新静态视图、不启动模拟。** 相机与选中节点保留。
- 作者 Apply 重启时保留已经生效的预览环境，不偷偷应用另一组尚未 Apply Environment 的控件值。`StartClothPreviewAsync(PreviewEnvironment forcedEnvironment=null)` 支持该路径。
- 有待提交修改时阻止切换 domain/加载/高级 XML/导出等，防止悄悄丢失或导出旧值。Apply 期间禁用相关编辑控件，关闭窗口不能复活宿主。
- Undo/Redo 不是全项目历史；高级 XML 重新 SetDocument 会清空表格编辑历史，CLM2 是独立文档。

## 5. 原生预览架构与必须保留的风修正

### 运行边界

- 普通 HKX/CLM2 读写纯 C#，严格 fail-closed。HCT 物理是可选的 **独立 x64 .NET Framework 子进程**，不加载进 Editor 主进程。
- 快照当前内存 XML、FLVER、CLM2，包括未保存修改；不改原始 rest 数组或源文件。
- 当前只是恒定作者参考骨架下的粒子/线框预览，**不变形主 Viewer 网格，不读游戏施力，不接动画时间线**。碰撞体/Range 中心也是作者参考数据。
- 当前用户专属命名管道，一次仅一个请求在途，最多四个 1/60 子步；准备和 IPC 不阻塞 UI。
- 协议 v3 hello `0x33565043`；Step=1 / Quit=2 / response=3；Step 携带 sequence/substeps 及 wind enabled、xyz、speed、maxDrag。旧 `0x32565043` 不是当前协议。
- Pause 保留历史；Stop 回收本窗口拥有的宿主、恢复静态视图，不重新启动；Reset 重建并启动。
- CLM2 加载匹配的 HKX/XML/Binder 时自动载入；保留手工比较文档或未保存 CLM2 修改。
- HCT Runtime 使用现代 OpenFileDialog 选择 `hctPreviewPlugin.dll`，从 `tools` 的父目录得到 runtime root。

### 环境与风：重要，不要退回旧实现

- UI 默认 **Wind Enabled (Native HCT)**，方向 +X、speed=3、maxDrag=1。关闭它就是没有预览风，不存在另一个“普通风”后端。
- Apply Environment 验证后重建当前宿主，并保持播放/暂停；停止状态只准备参数。Wind Test (+X) 是 speed=20/maxDrag=5 的诊断预设，需要 Apply；不是默认游戏参数。
- 重力覆写只改临时 runtime HKX 的 `hclSimClothData.simulationInfo.gravity`，交 DLL 解算；不是猜内部 setGravity 地址，不写作者/导出参数。
- WindForm 来自 `m_clothWindForm`：get/set enabled、direction、min/max speed、maxDrag、applyWindSettings；恒定风 min=max。校验原生读回是否被 clamp，退出恢复原设置。不伪造粒子偏移。
- **旧版仅调用低层 world solver 的路径虽然接受风控件，但风响应几乎不可见。现版本初始化及正常帧均调用 `PreviewPlugin.hctPreviewControl.step(Single)`，然后保留已测试的位置读回/world post 顺序。**
- 新增实际入口：`Boolean step(Single)`，MetadataToken **`0x0600049B`**；这是托管方法，不是原生 RVA。传参 `1f/60`。
- 显式 world post 仍为 RVA **`0x733DD0`**。旧 scratch-size `0x733210` / pre `0x733710` / world step `0x733A20` 是历史直接调用，不再是当前直接调用入口。其他 asset/state/buffer 绑定仍在源码内。
- 混用旧初始化和 wrapper 并省略 post 的隔离实验发生过宿主 native failure，**没有部署**。不要未经定向验证混用、双步进或删除 post。没有反编译包装函数来确认内部全部生命周期；当前依据是受控行为实验。
- BB 原 `8,1,1,1` 不能直接给这份 CompatFormats。临时运行副本按已审核 schema 转为 `8,1,0,1`，含 sphere/gather 布局；不是只改包头字节。普通 BB writer/profile 不变。
- v10 缺初始化 states，使用已验证的临时 CopyVertices 路径、rest 重建容差 0.0005、复制历史并恢复算子；不删这些安全门。

### 本机 Runtime、IPC 工作目录

```text
Runtime root:
D:\FLVER_Editor\helper-ext\cloth\preview-work\20261012-203558-85c85421\runtime-subset

Vendor plugin SHA-256 (严格版本门):
83E33363FBD320BB89613521633E5EDD4F5169D7501012426A850F982F025F75

部署的自有 helpers:
D:\FLVER_Editor\MySFformat\bin\Debug\tools\ClothPreview
```

六个本机厂商 DLL：`tools/hctPreviewPlugin.dll`、`tools/gac/Havok.Tool.UI.dll`、`Havok.Tool.Interfaces.dll`、`Havok.Graphics.dll`、`Havok.Graphics.Bridge.dll`、`utils/hkCompatFormats.dll`。HCT DLL 不随源码/发行包分发，不改原安装。

- Runtime 来源优先 `FLVER_CLOTH_RUNTIME`，然后 `%LOCALAPPDATA%\FLVER_Editor\ClothPreview\runtime-path.txt`。
- GUI 工作目录 `%LOCALAPPDATA%\FLVER_Editor\ClothPreview\work`，测试可用 `FLVER_CLOTH_WORK` 覆盖。
- 厂商网络功能通过子进程内 Winsock 导入绑定拒绝 socket；不改厂商二进制、系统防火墙、不提权。原先所谓“管理员提示”实际是防火墙 TCP/UDP 提示，不能靠管理员启动解决。

## 6. 构建、部署、定向检查

默认 Debug 部署到原路径 `D:\FLVER_Editor\MySFformat\bin\Debug`。PowerShell，在 `D:\FLVER_Editor\MySFformat`：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' MySFformat.csproj -p:Configuration=Debug -p:CopyRetryCount=0 -v:quiet -nologo
```

最近构建成功，仅有已有 unused/duplicate-using 等警告。修改 docs 后核对 Debug 的 `cloth-and-animation-README.md` 与 `tools/ClothPreview/README.md` 同步。

本机测试工具（Git 忽略，不是发行依赖）：

- `helper-ext/cloth/tests/ClothEditingChecks.cs` → `helper-ext/test-binaries/ClothEditingChecks.exe`：DS3/BB 作者编辑、批量 Range、重力/约束/mass、导出、Undo/Redo、选择同步、平移/刷选、Apply 生命周期和源哈希。
- `helper-ext/cloth/tests/ClothSelectionChecks.cs` → 同目录 exe：合成前后节点测试可见/穿透刷选、框选、聚焦；不启动原生宿主。
- `helper-ext/cloth/tests/ClothEnvironmentGuiChecks.cs` / `ClothPreviewGuiTests.cs`：较早预览检查。UI 重排后老控件可见性断言可能要相应更新，不要为旧测试把界面退回去。
- C# 小测试可用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 编译；GUI 测试引用 `System.Windows.Forms`、`System.Drawing`、`Microsoft.CSharp`，selection 测试另引用 `System.Numerics`。
- 编辑 GUI exe 参数依次是：Editor exe、runtime root、cloth HKX、FLVER、CLM2、**新的输出目录**。使用 STA/DoEvents，并正确设置 WinForms synchronization context；先前错误的 pool-thread timer 曾造成“宿主启动但 GUI 不进帧”。
- 最终 DS3 GUI 证据：`helper-ext/cloth/editing-checks/ds3-final-03`；BB GUI 证据：`bb-01`。BB GUI 是较早修订检查；最终新增“作者 Apply 保留尚未提交的环境控件”已在 DS3 最终检查通过。
- 当前新风路径 BB v10 和 DS3 都有 600 帧有限输出与源哈希保护检查；没有宣称完整的后集成动画/Viewer 全套回归。
- `git diff --check` 最近通过（有 CRLF 提示）。自动测试宿主已退出，没留下常驻服务。

### 最新已部署版本的风验收（最重要证据）

在 `D:\FLVER_Editor`：

```powershell
py -3.12 helper-ext/cloth/check_deployed_wind.py
```

这个脚本**直接运行部署的 helpers，不重新编译、不操作用户 Editor**；每次新建 UUID 证据目录。两份模型各跑 off/default/reverse/strong/default-repeat，600 帧，相同文件作者重力、不开重力覆写。

完整最新证据：
`helper-ext/cloth/wind-acceptance/e5140be620d2426ba67804555160b4f0/summary.json`。

| 模型 | 关风→默认风最大粒子距离 | 默认→反向最大距离 | 关风→强风最大距离 | 同设置重复差 |
|---|---:|---:|---:|---:|
| DS3（699 粒子） | 0.00864721 | 0.01736513 | 0.22647240 | 0 |
| BB v10（1180 粒子） | 0.66527235 | 1.23218111 | 0.66516787 | 0 |

这是模型坐标单位的**最终粒子欧氏位置差**，不是先前另一个报告的“最大分量差”。默认为 speed=3/drag=1，反向仅改 X 为 -1，strong 为 speed=20/drag=5。全部有限、原文件哈希不变，同设置重复完全一致。DS3 默认风较弱；BB 强风不继续增大最大位移可能受约束限制，但**原因未确证，不能写成已证明**。这些结果证明预览风有响应，不证明游戏风/单位/内部算法等价。

## 7. 模型与外部资料

DS3 fixtures：
`G:\bloodborne_pc-win\MOD\DS3装备测试\bd_m_3000-partsbnd-dcx\BD_M_3000_c.hkx`、`BD_M_3000.flver`、`BD_M_3000_c.clm2`。

BB v10 fixtures：
`G:\bloodborne_pc-win\MOD\workCLO\cloth-native-repro\candidates\cape-shawl-range-v10\mod\dvdroot_ps4\parts\bd_f_2200-partsbnd-dcx\BD_F_2200_c.hkx`、`BD_F_2200.flver`、`BD_F_2200_c.clm2`。

- 当前游戏测试 launcher 仍为 `G:\bloodborne_pc-win\MOD\start_Clorinde_ClothTest.bat` → v10。保留 `start_Clorinde_Cape.bat`，没有新游戏候选就不要改 launcher。
- Clorinde 必须使用自己的几何，历史 v1–v10 等候选保留。
- 教程 DOCX/工具放 `G:\bloodborne_pc-win\MOD\血源布料教程`，复现记录直接放 MOD。外部教程/发布镜像尚未完整同步这次编辑和风修正，不声称已同步。
- 原生调用的公开参考：`G:\bloodborne_pc-win\MOD\动态骨骼教程\DSAnimStudio-feat-root-motion-bullet-physics-fx-preview\NativePhysicsHost`；原下载中的 SoulsAssetPipeline submodule 曾为空。不要以它或 Blender Tlarok 的导入流程替代完整兼容性证明。
- HCT 互操作交接包：`G:\bloodborne_pc-win\MOD\动态骨骼教程\Hct逆向`。先读 README，再读 **`更新-20261013-原生风调用路径.md`**。2026-10-12 的 oracle/source/manifest 是历史快照，**未覆盖成新风版本**；不能拿旧清单验证新文件。
- Ghidra 标注脚本只准备过，**没有在 Ghidra 实机执行**。所谓“公益 mod”不自动解除 EULA/资产许可限制。严格 clean-room 需隔离 DLL 分析组与独立实现组，不把反编译代码换写后宣称 clean-room。
- 本次旧 G 盘 oracle 曾遇到 .NET 网络位置加载限制；不要擅自放宽系统策略。迁移时先将受信任、已核验的研究包放目标电脑本地目录。

## 8. 可继续开发的方向（需按用户新指令定范围）

1. 碰撞体直接编辑及视图 gizmo：先确认局部/世界坐标、形状派生缓存、骨绑定和 shared-object 影响，不能仅改显示端点或半径假装改了 native shape。
2. 固定节点编辑、蒙皮/显示/粒子绑定：先建立明确映射和一致性验证，不把整个 Cloth 的骨 subset 当逐粒子权重，不随便只改 invMass。
3. 更多约束参数/批量操作、快捷键、编辑错误与待提交状态体验；保留未应用数据、原生验证及 Undo 语义。
4. 动画输入、rewind/root motion/history reset、移动碰撞体和主 Viewer 输出联动；当前都未实现，先做可验证的接口与生命周期。
5. tutorial/复现发布记录同步；不要覆盖历史快照。
6. 游戏效果仍需用户验收；全局定向披风力、真正弯曲、头发整合与静态裙子穿模等旧问题没有被这次 GUI/预览工作自动解决。
