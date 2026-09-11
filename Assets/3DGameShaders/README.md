# 3D Game Shaders For Beginners — Unity URP 移植骨架

从 lettier/3d-game-shaders-for-beginners 移植的起点工程（Unity 2022.3 LTS / URP 14）。
着色器逻辑移植自原仓库的 base.vert/base.frag、bloom.frag、box-blur.frag、
gamma-correction.frag、lookup-table.frag、pixelize.frag、dilation.frag、
sharpen.frag、chromatic-aberration.frag、posterize.frag、fog.frag、film-grain.frag，
以及水面的 normal.frag / base.frag / screen-space-refraction.frag / refraction.frag /
screen-space-reflection.frag / reflection-color.frag / foam.frag / foam-mask.frag /
base-combine.frag
（BSD 3-Clause，保留原版权声明）。

## 已实现

Phase 1（基础）：
- BaseLit：漫反射 + 法线贴图 + Blinn-Phong + Fresnel + Rim + Cel + 环境光 + 自发光
- Bloom / Gamma / LUT 后处理链

Phase 2（简单后处理，全部带开关）：
- Pixelize、Dilation、Sharpen、Chromatic Aberration、Posterize、Fog、Film Grain
- Fog 使用 URP 深度纹理（开启时自动请求 _CameraDepthTexture）

Phase 3（中级后处理）：
- SSAO、Outline、Depth Of Field、Motion Blur

Phase 4（屏幕空间水面）：
- Water.shader：菲涅尔高光、折射偏移、深度泡沫、流动法线、水面专用 SSR

## 如何开关效果

Project 窗口选中 Assets/3DGameShaders/Settings/3DGameShadersRenderer.asset，
Inspector 里展开 Post Processing Feature，勾选/取消对应的 Enabled 开关即可。
所有效果默认关闭（Bloom、LUT 除外）。

## 屏幕空间水面（Water.shader）

水面是挂在磨坊场景已有的 `water-diffuse` 网格上的一个透明材质
（Queue=Transparent、ZWrite Off，深度缓冲里始终保留水下的河床）。
它按原教程 base-combine.frag 的顺序合成：

1. 折射：背景色按水面法线偏移采样 `_CameraOpaqueTexture`，再按水深混向"深水色"
   （水深 = 场景深度 − 水面深度，`_WaterDepth` 默认 2.0，与教程 depthMax 一致）
2. 深水色：水的本体漫反射（`water-diffuse.png` 是这条河的蓝色）经光照后，
   再按 `_TintStrength` 拉向教程的 `_TintColor`。
   注意这一步不能省：URP 的前向不透明纹理里**没有水面自己**，只靠折射得到的是河床色，
   水就会发灰
3. 反射：沿反射方向在深度缓冲里步进（`_SSRMaxDistance` 8、`_SSRResolution` 0.3、
   `_SSRSteps` 5、`_SSRThickness` 0.5，与教程一致），命中后按粗糙度混合模糊副本；
   屏幕空间反射**看不到天空**（深度缓冲里没有天空），所以掠射角用 `_ReflectionFallback`
   （场景天空色）兜底
4. 泡沫：`1 - 水深/`_FoamDepth`` 经 ease-in/out 后乘上流动的泡沫图案
5. 高光：Blinn-Phong，菲涅尔把高光颜色推向白色

### 这条河的现实约束

磨坊模型的河床是 y = -2.05 的一块**平整平面**，水面在 y = 1，水深恒为 3.05。所以：

- 开阔水面不会出泡沫（`1 - 3.05/1.5` 恒小于 0）。泡沫只出现在**几何体穿过水面**的位置，
  水车和码头都穿过 y = 1，那里会出现泡沫环。想往开阔水面推泡沫就调大 `_FoamDepth`，
  但它会变成一层均匀白雾，不是岸线泡沫。
- 水面反射的是天空那一侧，SSR 打不到，主要靠 `_ReflectionFallback` 表现。

流动法线来自流动图（`water-flow.png`，即教程的 up-flow.png）：
法线贴图沿着流动图给出的方向随时间滚动，和教程 normal.frag 一样。

### 前置条件

水面的每一次屏幕空间采样都依赖相机纹理，缺一不可：

- 相机的 **Depth Texture** 必须开启（水深、泡沫、SSR 步进）
- 相机的 **Opaque Texture** 必须开启（折射、反射取色）

注意：只靠后处理 pass 的 `ConfigureInput` 请求深度是不够的。URP 会把那次深度拷贝
排到透明物体之后（`UniversalRenderer.cs` 里 `copyDepthPassEvent` 取
`min(AfterRenderingTransparents, earliestEvent - 1)`），而水面在透明队列里绘制，
拿不到那份深度。

后处理链里那个全屏 SSR 默认关掉了：它会给整帧所有像素加反射，和水面自己的 SSR 冲突。

### 用法

菜单栏 `3DGameShaders -> Setup Screen Space Water`。它会打开相机的两个纹理开关、
关闭全屏 SSR、创建 `Materials/WaterSurface.mat` 并赋给场景里的 `water-diffuse` 网格，
给水车加一个位于轮轴上的枢轴并挂上 `WaterWheel`（-90°/秒，同教程），然后保存场景。
它不会新建任何几何体——水面就是模型里那条河。

重复执行会把材质参数重置到上面这套基准值，方便回到已知状态。

可调参数在 `Materials/WaterSurface.mat`：`_WaterDepth`/`_TintStrength`/`_TintColor`
控制水色，`_FoamDepth`/`_FoamIntensity` 控制泡沫范围，`_RefractionStrength` 是折射偏移像素数，
`_SSR*` 控制反射射程与步进，`_FlowSpeed` 是流动速度。

## 使用步骤

1. 等 Unity 完成脚本编译（Console 无红色报错）
2. 首次使用：菜单栏 3DGameShaders -> Build Demo Scene
3. 若场景里没有模型：菜单栏 3DGameShaders -> Add Mill Scene Model
4. 水面：菜单栏 3DGameShaders -> Setup Screen Space Water（会自动保存场景）
5. 点 Play

## 下一阶段建议

- 水面 SSR 目前逐像素步进，可改成半分辨率 + 时域重投影降开销
- 折射是单次偏移采样，可升级成和反射同源的屏幕空间折射步进
- 需要水体回读时可补 Deferred GBuffer 路径

注意：贴图仅用于本地学习（原仓库仅 .cxx/.vert/.frag 开放 BSD 许可）。
