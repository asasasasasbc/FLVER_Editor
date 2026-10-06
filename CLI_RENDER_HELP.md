# FLVER Editor：相机 JSON 与同步渲染 CLI

已修改本地源码，保留原有编辑器的 Mono3D/Program.updateVertices 渲染管线。
新增源码：MySFformat/src/ViewerCameraSettings.cs、RenderCli.cs。

## 运行

在命令行中调用（Windows 路径可用正斜线）：

```text
D:/FLVER_Editor/MySFformat/bin/Debug/MySFformat.Cli.exe --render G:/bloodborne_pc-win/MOD/work/editor-preview/Saber-v3-direction-preview.flver --camera G:/bloodborne_pc-win/MOD/work/editor-preview/cli-shots/front-solid.view.json --screenshot G:/bloodborne_pc-win/MOD/work/editor-preview/cli-shots/front-solid.png --width 1080 --height 1080
```

- --render：FLVER，或能由当前 SoulsFormatsNEXT 正确读取的 BND4/DCX。
- --screenshot：输出 PNG，必填；旁边生成同名 .png.json 渲染记录。
- --camera：相机 JSON；省略则读取模型旁的相机文件，找不到使用默认正面相机。
- --width、--height：64～4096，默认1200×1200。
- --member：多 FLVER binder 的成员名称，不弹选择框。
- --pose：编辑器原生 pose-node JSON；数量必须与模型骨架一致。
- --frames：1～30，默认2帧；完成截图后退出。
- --help：参数帮助。

此模式**同步运行**，不启动后台任务/工作线程，不打开节点编辑界面，不读取键鼠，不改输入模型。PNG 直接来自 GPU RenderTarget，而非抓屏或拉伸桌面图像。Windows/MonoGame 仍需可用的桌面图形环境及隐藏 native window，不能等同于无显示服务的服务器渲染器。CLI 当前使用无贴图灰模/线框检查模式，不代表 PS4 TPF 贴图验证。

## 相机文件

优先：完整模型路径 + `.view.json`。
其次：模型所在目录的 `viewer-camera.json`。

```json
{
  "camera": [0, 1.05, -3.4],
  "target": [0, 1.05, 0],
  "renderMode": "Triangle",
  "showBones": false,
  "showDummies": false
}
```

坐标使用 FLVER 空间：X 横向、Y 向上；本次 Bloodborne 原生角色正面为 −Z。内部 viewer 的 Y/Z 交换只用于显示，不改变模型。

renderMode 可选 Line、Triangle、Both、BothNoTex、TexOnly；本 CLI 禁用贴图载入，建议 Triangle 或 BothNoTex。

普通 GUI 模式增加 Camera Helper 菜单，可重载相机或保存当前视图。相机文件内容变化每0.5秒同步检查一次，不需要 watcher/background task；无效 JSON 保留当前视图并在标题栏显示错误。成功加载后写 `.view.json.applied.json`，供读回核验。

## 长宽比修复

不再使用 GraphicsDeviceManager.PreferredBackBufferWidth/Height（受窗口/DPI改变影响）。投影使用当前 GraphicsDevice.Viewport.AspectRatio，即当前绑定的 GPU RenderTarget 的实际宽高比。

实测修复前：1080×1080 使用投影比1.147715；1920×1080使用2.040383。修复后分别为1和1.777778。

相同竖向FOV、相机、输出高度1080下：1080×1080、1920×1080、900×1080三图的人物灰模包围范围均为548×705像素，画布宽度变化不再改变人体比例。此前的旧截图不可用于评估腰围和体型。

## 已运行测试

- CameraSettingsTests.exe：8项相机输入断言。
- RenderCliTests.exe：6项 CLI 参数断言。
- verify_render_cli.py：六个不同视角PNG、三条失败路径、输入模型SHA-256不变。
- verify_render_aspect.py：方形/宽屏/竖屏投影比和实际像素比例回归测试。

测试均位于 D:/FLVER_Editor/helper-tests/。
正确比例截图：G:/bloodborne_pc-win/MOD/work/editor-preview/cli-shots/ 与 aspect-check/。
修复前长宽比证据保存在 aspect-check-before-fix/。

## 备份与回退

源码和原始二进制：D:/FLVER_Editor/helper-backup-before-camera/。
CLI修改前源码：D:/FLVER_Editor/helper-backup-before-cli/。
已额外保留 MySFformat.Cli.exe 入口，不需要对模型或游戏进行任何写入。

本工具通过不等于 Saber 蒙皮通过；肩臂、披肩、腰部和后续动作尚需单独验收。目前不进游戏。
