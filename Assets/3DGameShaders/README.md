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

Phase 5（演示交互层）：
- DemoEffectsController：原教程的全套按键开关 + 屏幕控件面板 + 状态文字
- BaseLit 增加法线贴图 / 菲涅尔 / Rim / Phong-Blinn 运行时开关
- Kuwahara 水彩滤镜（Painterly）后处理 pass
- 定位音效（水车、水面）与烟囱烟雾粒子、太阳动画

## 如何开关效果

Project 窗口选中 Assets/3DGameShaders/Settings/3DGameShadersRenderer.asset，
Inspector 里展开 Post Processing Feature，勾选/取消对应的 Enabled 开关即可。
所有效果默认关闭（Bloom、LUT 除外）。

## 屏幕空间水面（Water.shader）

水面是挂在磨坊场景已有的 `water-diffuse` 网格上的一个透明材质
（Queue=Transparent、ZWrite Off，深度缓冲里始终保留水下的河床）。
它按原教程 base-combine.frag 的顺序合成：

1. 折射：背景色按水面法线偏移采样 `_CameraOpaqueTexture`，再按水深混向"深水色"。
   水深 = 场景深度 − 水面深度，吸收用指数形式 `1 - exp(-水深/_WaterDepth)`，
   再乘 `_WaterBodyStrength`。**不能用 `saturate(水深/_WaterDepth)`**：这条河到处都有
   3 个单位深，比值会直接饱和成 1，河床就被完全盖掉了
2. 深水色：水的本体漫反射（`water-diffuse.png` 是这条河的蓝色）经光照后，
   再按 `_TintStrength` 拉向教程的 `_TintColor`。
   注意这一步不能省：URP 的前向不透明纹理里**没有水面自己**，只靠折射得到的是河床色，
   水就会发灰
3. 反射：两层。
   - 环境反射打底：`GlossyEnvironmentReflection`，URP 每帧把
     `ReflectionProbe.defaultTexture`（Lighting 里由 **skybox** 生成的默认反射立方图）
     喂进来，所以天空盒会正确出现在水面上
   - 屏幕空间反射覆盖：沿反射方向在深度缓冲里步进（`_SSRMaxDistance` 8、
     `_SSRResolution` 0.3、`_SSRSteps` 5、`_SSRThickness` 0.5，与教程一致），
     命中磨坊/树/岸这些几何时按粗糙度混合模糊副本

   为什么必须两层：屏幕空间反射**永远看不到天空**——它沿深度缓冲步进，而天空没有深度。
   这也是俯视水面时看到的主要是环境反射、低视角掠过水面时才出现 SSR 的原因。
4. 泡沫：`1 - 水深/_FoamDepth` 经 ease-in/out 后乘上流动的泡沫图案，
   图案再用 `_FoamThreshold` 做 smoothstep 阈值化（原图平均亮度只有 0.26，
   不阈值化就是一层灰雾而不是泡沫）
5. 高光：Blinn-Phong，菲涅尔把高光颜色推向白色

### 这条河的现实约束

磨坊模型的河床是 y = -2.05 的一块**平整平面**，水面在 y = 1，水深恒为 3.05。所以：

- 泡沫只出现在**几何体穿过水面**的位置（水车、码头都穿过 y = 1），开阔水面恒为深水。
  想要整条河都铺流动泡沫，把 `_FoamDepth` 从 1.5 往上调；注意 ease 曲线在浅处近似
  平方，所以小步调没用，要调就调到 6 附近才明显。
- 俯视时水面反射的是天空那一侧，SSR 打不到，这部分由环境反射（skybox）负责；
  低视角掠过水面时才会看到磨坊和树的 SSR 倒影。

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
`_SSR*` 控制反射射程与步进。

`_FlowSpeed` 是流动速度，默认 **1.0**：教程的 `normal.frag` 用
`flow * osg_FrameTime` 平移 UV，`flowMapsEnabled` 只是 0/1 开关而不是倍率，
所以 1.0 才是教程的速率（up-flow.png 携带 (0, 0.25)，即每秒四分之一 UV）。
觉得太快就往下调，0.05 会慢二十倍。

> 注意：`Setup Screen Space Water` 现在只在材质**新建时**写入默认值，
> 手工调过的参数不会被覆盖。要回到基准值用
> `3DGameShaders -> Reset Water Material To Defaults`。

## 调参与验证

### 烟雾粒子

参数在场景里 `SmokeParticles` 对象的 **ParticleSystem** 组件上，常用几个：

- `Main -> Start Speed` 决定飘出烟囱的初速度（默认 1.2–2.2）
- `Main -> Gravity Modifier` 是负值（浮力），越大上升越持续
- `Main -> Start Lifetime` 决定飘多远才消失
- `Velocity over Lifetime` 里的 `x/y` 是持续的风与上升气流，默认 y=0.8、x=0.25，
  想让它斜着飘得更远就加大 x
- `Emission -> Rate over Time` 控制浓度

手工调过之后不要点 `Reset Smoke Particles`，那个菜单项会把这些值重置回默认。

### 渲染探针

`3DGameShaders -> Render Probe (writes report)`：Play 模式下执行，它会把相机渲染两张图
（一张正常、一张隐藏水面网格），比较两者的差异，然后把统计结果写到
`Assets/3DGameShaders/render-probe-report.txt`。报告里包括：

- 水面占画面的比例、水面平均色与"移除水面后同样像素"的平均色（也就是河床）
- 两者比值、平均差值（0 = 水面不可见，3 = 完全覆盖河床）、比河床亮的泡沫像素占比
- 当前水面材质与后处理的全部开关和关键参数

这张报告是纯文本，调参或改代码之后重跑一次就能看出前后差异。

## 使用步骤

1. 等 Unity 完成脚本编译（Console 无红色报错）
2. 首次使用：菜单栏 3DGameShaders -> Build Demo Scene
3. 若场景里没有模型：菜单栏 3DGameShaders -> Add Mill Scene Model
4. 水面：菜单栏 3DGameShaders -> Setup Screen Space Water（会自动保存场景）
5. 音效/粒子/交互：菜单栏 3DGameShaders -> Setup Demo Extras (Audio, Particles, Controls)
6. 点 Play，按 F1 显隐效果面板

## 教程控件对照

和原教程 `running-the-demo` 一致，Play 模式下可用：

| 按键 | 作用 |
|---|---|
| 左键拖拽 / 右键拖拽 / 滚轮 | 旋转 / 平移 / 缩放相机 |
| w a s d / z x / 方向键 | 旋转与移动相机（OrbitCamera） |
| 中键 | 设置色差焦点 |
| 1 / 2 / `/` | 正午 / 午夜 / 太阳动画 |
| 3 / 4 / 8 / O / 0 | 菲涅尔 / Rim / 卡通着色 / 法线贴图 / Phong⇄Blinn |
| Y U I P H J K L N | SSAO / 描边 / Bloom / 雾 / 景深 / 海报化 / 像素化 / 锐化 / 胶片颗粒 |
| 6 / 7 / 9 / `\` | 运动模糊 / Kuwahara 水彩 / LUT / 色差 |
| M / `,` / `.` | 水面反射 / 折射 / 流动贴图 |
| `-` `=`（含 Shift 反向） | 泡沫深度 / 折射偏移 |
| `[` `]`（含 Shift 反向） | 雾的 near / far |
| 5 / Delete | 烟雾粒子 / 声音 |
| F1 | 显隐屏幕控件面板 |

面板里每一项都可以直接用鼠标点选，参数用滑杆拖。

## 与原教程的差异

原教程有一台“帧缓冲查看器”（Tab 键循环 38 张中间缓冲）。Unity 侧没有照搬：
那条管线把水面、烟雾、反射、折射各自写进独立的 G-buffer 层，而我们是在前向渲染里
就地算完的，硬做出一堆同名缓冲只是假象。其余效果都已经对应实现。

另外这些没有移植：

- Deferred GBuffer 路径（架构差异，见水面一节）
- 百叶窗开合动画：原教程靠模型里烘焙好的 `open-shutters` / `close-shutters`
  两段动画驱动具名节点；我们手上的 `shutters.obj` 是静态网格，而且内部是
  **230 个互不相连的小板条孤岛**（每个 7–11 个顶点），没有"左扇/右扇"这种可绕铰链旋转的节点。
  想做得在 Blender 里把门与左右百叶拆成独立对象再导出，然后才能接上开合逻辑。
  作为替代，`DemoEffectsController` 已经实现了 `animateLights` 的实质内容：
  太阳随角度转红并熄灭、窗灯在夜间亮起（`pow(night, 0.4)`）。
- 原教程水面在雾里的特殊烟雾遮罩处理

注意：贴图仅用于本地学习（原仓库仅 .cxx/.vert/.frag 开放 BSD 许可）。
