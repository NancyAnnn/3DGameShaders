using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PostProcessingFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        [Header("Bloom")]
        public bool bloomEnabled = true;
        public float bloomThreshold = 0.6f;
        public float bloomAmount = 0.6f;
        public float blurSize = 3f;

        [Header("Pixelize")]
        public bool pixelizeEnabled = false;
        public float pixelSize = 8f;

        [Header("Dilation")]
        public bool dilationEnabled = false;
        public float dilationSize = 1f;
        public float dilationSeparation = 1f;

        [Header("Sharpen")]
        public bool sharpenEnabled = false;
        public float sharpenAmount = 0.3f;

        [Header("Chromatic Aberration")]
        public bool chromaticAberrationEnabled = false;
        public float caAmount = 1f;
        public Vector2 caFocusPoint = new Vector2(0.5f, 0.5f);

        [Header("Posterize")]
        public bool posterizeEnabled = false;
        public float posterizeLevels = 6f;

        [Header("Fog")]
        public bool fogEnabled = false;
        public float fogNear = 20f;
        public float fogFar = 60f;
        public Color fogColor = new Color(0.78f, 0.83f, 0.9f, 1f);

        [Header("Film Grain")]
        public bool filmGrainEnabled = false;
        public float filmGrainAmount = 0.01f;

        [Header("Gamma")]
        public bool gammaEnabled = false;
        public float gamma = 1.0f;

        [Header("Lookup Table")]
        public bool lutEnabled = true;
        public float sunPosition = 0.5f;
        public Texture2D lut0;
        public Texture2D lut1;

        public Material material;
    }

    public Settings settings = new Settings();

    private PostPass m_Pass;

    public override void Create()
    {
        m_Pass = new PostPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings == null || settings.material == null)
        {
            return;
        }
        renderer.EnqueuePass(m_Pass);
    }

    private sealed class PostPass : ScriptableRenderPass
    {
        private const int PassBright = 0;
        private const int PassBlur = 1;
        private const int PassCombine = 2;
        private const int PassPixelize = 3;
        private const int PassDilation = 4;
        private const int PassSharpen = 5;
        private const int PassChromaticAberration = 6;
        private const int PassPosterize = 7;
        private const int PassFog = 8;
        private const int PassFilmGrain = 9;
        private const int PassFinal = 10;

        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");

        private readonly Settings m_Settings;
        private RTHandle m_Bright;
        private RTHandle m_Blur;
        private RTHandle m_A;
        private RTHandle m_B;

        public PostPass(Settings settings)
        {
            m_Settings = settings;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(ref m_Bright, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostBright");
            RenderingUtils.ReAllocateIfNeeded(ref m_Blur, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostBlur");
            RenderingUtils.ReAllocateIfNeeded(ref m_A, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostA");
            RenderingUtils.ReAllocateIfNeeded(ref m_B, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostB");

            if (m_Settings.fogEnabled)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Material material = m_Settings.material;
            if (material == null)
            {
                return;
            }

            material.SetFloat("_UseBloom", m_Settings.bloomEnabled ? 1f : 0f);
            material.SetFloat("_BloomThreshold", m_Settings.bloomThreshold);
            material.SetFloat("_BloomAmount", m_Settings.bloomEnabled ? m_Settings.bloomAmount : 0f);
            material.SetFloat("_BlurSize", Mathf.Max(0f, m_Settings.blurSize));

            material.SetFloat("_UsePixelize", m_Settings.pixelizeEnabled ? 1f : 0f);
            material.SetFloat("_PixelSize", Mathf.Max(1f, m_Settings.pixelSize));

            material.SetFloat("_UseDilation", m_Settings.dilationEnabled ? 1f : 0f);
            material.SetFloat("_DilationSize", Mathf.Max(1f, m_Settings.dilationSize));
            material.SetFloat("_DilationSeparation", Mathf.Max(0f, m_Settings.dilationSeparation));

            material.SetFloat("_UseSharpen", m_Settings.sharpenEnabled ? 1f : 0f);
            material.SetFloat("_SharpenAmount", Mathf.Max(0f, m_Settings.sharpenAmount));

            material.SetFloat("_UseChromaticAberration", m_Settings.chromaticAberrationEnabled ? 1f : 0f);
            material.SetFloat("_CAAmount", Mathf.Max(0f, m_Settings.caAmount));
            material.SetVector("_CAFocusPoint", m_Settings.caFocusPoint);

            material.SetFloat("_UsePosterize", m_Settings.posterizeEnabled ? 1f : 0f);
            material.SetFloat("_PosterizeLevels", Mathf.Max(2f, m_Settings.posterizeLevels));

            material.SetFloat("_UseFog", m_Settings.fogEnabled ? 1f : 0f);
            material.SetFloat("_FogNear", m_Settings.fogNear);
            material.SetFloat("_FogFar", m_Settings.fogFar);
            material.SetColor("_FogColor", m_Settings.fogColor);

            material.SetFloat("_UseFilmGrain", m_Settings.filmGrainEnabled ? 1f : 0f);
            material.SetFloat("_FilmGrainAmount", Mathf.Max(0f, m_Settings.filmGrainAmount));

            material.SetFloat("_UseGamma", m_Settings.gammaEnabled ? 1f : 0f);
            material.SetFloat("_Gamma", m_Settings.gamma);
            material.SetFloat("_UseLUT", m_Settings.lutEnabled ? 1f : 0f);
            material.SetFloat("_SunPosition", m_Settings.sunPosition);
            material.SetTexture("_LUT0", m_Settings.lut0);
            material.SetTexture("_LUT1", m_Settings.lut1);

            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            RTHandle source = renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get("3DGameShaders.PostProcessing");

            // Chain: scene -> bright -> blur -> combine(+bloom)
            //        -> pixelize -> dilation -> sharpen -> CA -> posterize -> fog -> film grain
            //        -> final (gamma + LUT) -> camera target
            Blitter.BlitCameraTexture(cmd, source, m_Bright, material, PassBright);
            Blitter.BlitCameraTexture(cmd, m_Bright, m_Blur, material, PassBlur);

            cmd.SetGlobalTexture(BloomTextureId, m_Blur);
            Blitter.BlitCameraTexture(cmd, source, m_A, material, PassCombine);

            RTHandle src = m_A;
            RTHandle dst = m_B;
            BlitAndSwap(cmd, ref src, ref dst, material, PassPixelize);
            BlitAndSwap(cmd, ref src, ref dst, material, PassDilation);
            BlitAndSwap(cmd, ref src, ref dst, material, PassSharpen);
            BlitAndSwap(cmd, ref src, ref dst, material, PassChromaticAberration);
            BlitAndSwap(cmd, ref src, ref dst, material, PassPosterize);
            BlitAndSwap(cmd, ref src, ref dst, material, PassFog);
            BlitAndSwap(cmd, ref src, ref dst, material, PassFilmGrain);

            Blitter.BlitCameraTexture(cmd, dst, renderer.cameraColorTargetHandle, material, PassFinal);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        private static void BlitAndSwap(CommandBuffer cmd, ref RTHandle src, ref RTHandle dst, Material material, int pass)
        {
            Blitter.BlitCameraTexture(cmd, src, dst, material, pass);
            RTHandle temp = src;
            src = dst;
            dst = temp;
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // The RTHandles are pooled by RenderingUtils.ReAllocateIfNeeded.
        }
    }
}