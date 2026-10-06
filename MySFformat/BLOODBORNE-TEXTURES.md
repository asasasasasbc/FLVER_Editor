# 血源 DCX 自动贴图

启动 `bin/Debug/MySFformat.exe`，保持原有 Load Texture 开启，直接打开 `.partsbnd.dcx`。无需 Yabber 解包或手工导出 DDS。

Viewer 选择 `TexOnly` 或 `Both` 贴图显示模式（`BothNoTex` 与 `Triangle` 不显示贴图）。

实现路径：BND4/DCX → 合并包内 TPF（含 `.tpf.dcx`）→ SoulsFormats `Texture.Headerize()` 重排 PS4 tiled 数据、补 DDS 头 → Pfim 解码 → MonoGame 纹理。

已有 DDS 直接传入，不重复转换。单张贴图失败不会终止其他纹理加载，错误进入 Trace/控制台；重复名称覆盖旧 GPU 纹理，名称比较不区分大小写。没有修改原始包或往游戏目录写文件。

这仍是编辑器现有的基础纹理预览，不是血源完整 PBR／法线／反射／游戏材质着色器。加载所有 TPF 纹理，但可见网格仍使用原有材质贴图选择逻辑。

## 验证

指定的原版 `Games/dvdroot_ps4/parts/bd_f_2200.partsbnd.dcx`：10 张纹理，12 个网格匹配。
指定的 Saber Mod `MOD/Saber-Hunter-v3/dvdroot_ps4/parts/bd_f_2200.partsbnd.dcx`：12 张纹理，27 个网格匹配。

两者正／侧／背面 GPU 渲染共 6 张，均无纹理解码错误；输入 SHA256 保持不变。

```bash
py -3.12 tests/verify_bloodborne_textures.py
```

解码测试：`tests/BloodborneTextureTests.cs`；渲染回执：`tests/hkx-work/bloodborne-texture-verification.json`。
HKX 原有同步回归 `py -3.12 tests/run_hkx_tests.py` 同样通过。

## 依赖来源

新增 DrSwizzler 1.0.8，为工程已有 SoulsFormats PS4 Headerizer 所需依赖。
NuGet: https://www.nuget.org/packages/DrSwizzler/1.0.8
上游: https://github.com/Shadowth117/DrSwizzler
作者: Shadowth117
包记录源码 commit: 06cbda610e508bff28787223e1fc54cf54c27ff2

包中未附 license 文件，上游当前根目录也未找到独立 LICENSE；此处用于本机测试，公开再分发前应确认其许可。未宣称拥有该依赖的再分发授权。
