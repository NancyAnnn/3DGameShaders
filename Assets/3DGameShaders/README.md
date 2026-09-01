# 3D Game Shaders For Beginners — Unity URP 移植骨架

从 lettier/3d-game-shaders-for-beginners 移植的起点工程（Unity 2022.3 LTS / URP 14）。
着色器逻辑移植自原仓库的 base.vert/base.frag、bloom.frag、box-blur.frag、
gamma-correction.frag、lookup-table.frag、pixelize.frag、dilation.frag、
sharpen.frag、chromatic-aberration.frag、posterize.frag、fog.frag、film-grain.frag
（BSD 3-Clause，保留原版权声明）。

## 已实现

Phase 1（基础）：
- BaseLit：漫反射 + 法线贴图 + Blinn-Phong + Fresnel + Rim + Cel + 环境光 + 自发光
- Bloom / Gamma / LUT 后处理链

Phase 2（简单后处理，全部带开关）：
- Pixelize、Dilation、Sharpen、Chromatic Aberration、Posterize、Fog、Film Grain
- Fog 使用 URP 深度纹理（开启时自动请求 _CameraDepthTexture）

## 如何开关效果

Project 窗口选中 Assets/3DGameShaders/Settings/3DGameShadersRenderer.asset，
Inspector 里展开 Post Processing Feature，勾选/取消对应的 Enabled 开关即可。
所有效果默认关闭（Bloom、LUT 除外）。

## 使用步骤

1. 等 Unity 完成脚本编译（Console 无红色报错）
2. 首次使用：菜单栏 3DGameShaders -> Build Demo Scene
3. 点 Play

## 下一阶段建议

Phase 3（中级）：SSAO、Outlining、Depth Of Field、Motion Blur（深度/法线/速度纹理）
Phase 4（高级）：Deferred GBuffer、SSR/SSRefraction、Foam/Flow Mapping

注意：贴图仅用于本地学习（原仓库仅 .cxx/.vert/.frag 开放 BSD 许可）。