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

        [Header("SSAO")]
        public bool ssaoEnabled = false;
        public float ssaoIntensity = 1.0f;
        public float ssaoRadius = 0.35f;
        public float ssaoBias = 0.02f;
        public float ssaoContrast = 1.2f;
        public float ssaoBlurSize = 2f;

        [Header("Motion Blur")]
        public bool motionBlurEnabled = false;
        public float motionBlurSamples = 6f;
        public float motionBlurSeparation = 1f;

        [Header("Outline")]
        public bool outlineEnabled = false;
        public float outlineDepthThreshold = 0.6f;
        public float outlineNormalThreshold = 0.8f;
        public Color outlineColor = new Color(0.42f, 0.36f, 0.28f, 1f);

        [Header("Depth Of Field")]
        public bool dofEnabled = false;
        public float dofFocusDistance = 8f;
        public float dofRange = 5f;
        public float dofBlurSize = 4f;

        [Header("Screen Space Reflection")]
        public bool ssrEnabled = false;
        public float ssrIntensity = 0.35f;
        public float ssrMaxDistance = 6f;
        public float ssrThickness = 0.5f;
        public float ssrResolution = 0.3f;
        public float ssrSteps = 5f;

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
        private const int PassOutline = 10;
        private const int PassSSAO = 11;
        private const int PassSSAOApply = 12;
        private const int PassMotionBlur = 13;
        private const int PassDOFMix = 14;
        private const int PassSSR = 16;
        private const int PassFinal = 15;

        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
        private static readonly int SSAOTextureId = Shader.PropertyToID("_SSAOTexture");
        private static readonly int DOFBlurTextureId = Shader.PropertyToID("_DOFBlurTexture");

        private readonly Settings m_Settings;
        private RTHandle m_Bright;
        private RTHandle m_Blur;
        private RTHandle m_A;
        private RTHandle m_B;
        private RTHandle m_SSAO1;
        private RTHandle m_SSAO2;
        private RTHandle m_DOFBlur;

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
            RenderingUtils.ReAllocateIfNeeded(ref m_SSAO1, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostSSAO1");
            RenderingUtils.ReAllocateIfNeeded(ref m_SSAO2, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostSSAO2");
            RenderingUtils.ReAllocateIfNeeded(ref m_DOFBlur, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PostDOFBlur");

            ScriptableRenderPassInput inputs = ScriptableRenderPassInput.None;
            if (m_Settings.fogEnabled || m_Settings.ssaoEnabled || m_Settings.outlineEnabled || m_Settings.dofEnabled || m_Settings.ssrEnabled)
            {
                inputs |= ScriptableRenderPassInput.Depth;
            }
            if (m_Settings.ssaoEnabled || m_Settings.outlineEnabled || m_Settings.ssrEnabled)
            {
                inputs |= ScriptableRenderPassInput.Normal;
            }
            if (m_Settings.motionBlurEnabled)
            {
                inputs |= ScriptableRenderPassInput.Motion;
            }
            if (inputs != ScriptableRenderPassInput.None)
            {
                ConfigureInput(inputs);
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

            material.SetFloat("_UseSSAO", m_Settings.ssaoEnabled ? 1f : 0f);
            material.SetFloat("_SSAOIntensity", Mathf.Max(0f, m_Settings.ssaoIntensity));
            material.SetFloat("_SSAORadius", Mathf.Max(0.001f, m_Settings.ssaoRadius));
            material.SetFloat("_SSAOBias", Mathf.Max(0f, m_Settings.ssaoBias));
            material.SetFloat("_SSAOContrast", Mathf.Max(0f, m_Settings.ssaoContrast));
            material.SetFloat("_SSAOBlurSize", Mathf.Max(0f, m_Settings.ssaoBlurSize));

            material.SetFloat("_UseMotionBlur", m_Settings.motionBlurEnabled ? 1f : 0f);
            material.SetFloat("_MotionBlurSamples", Mathf.Max(1f, m_Settings.motionBlurSamples));
            material.SetFloat("_MotionBlurSeparation", Mathf.Max(0f, m_Settings.motionBlurSeparation));

            material.SetFloat("_UseOutline", m_Settings.outlineEnabled ? 1f : 0f);
            material.SetFloat("_OutlineDepthThreshold", Mathf.Max(0.001f, m_Settings.outlineDepthThreshold));
            material.SetFloat("_OutlineNormalThreshold", Mathf.Clamp01(m_Settings.outlineNormalThreshold));
            material.SetColor("_OutlineColor", m_Settings.outlineColor);

            material.SetFloat("_UseDOF", m_Settings.dofEnabled ? 1f : 0f);
            material.SetFloat("_DOFFocusDistance", Mathf.Max(0f, m_Settings.dofFocusDistance));
            material.SetFloat("_DOFRange", Mathf.Max(0.01f, m_Settings.dofRange));
            material.SetFloat("_DOFBlurSize", Mathf.Max(0f, m_Settings.dofBlurSize));

            material.SetFloat("_UseSSR", m_Settings.ssrEnabled ? 1f : 0f);
            material.SetFloat("_SSRIntensity", Mathf.Max(0f, m_Settings.ssrIntensity));
            material.SetFloat("_SSRMaxDistance", Mathf.Max(0.01f, m_Settings.ssrMaxDistance));
            material.SetFloat("_SSRThickness", Mathf.Max(0.01f, m_Settings.ssrThickness));
            material.SetFloat("_SSRResolution", Mathf.Clamp(m_Settings.ssrResolution, 0.01f, 1f));
            material.SetFloat("_SSRSteps", Mathf.Max(1f, m_Settings.ssrSteps));

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

            RTHandle cur = m_A;
            RTHandle tmp = m_B;

            // Bloom: scene -> bright -> blur
            Blitter.BlitCameraTexture(cmd, source, m_Bright, material, PassBright);
            Blitter.BlitCameraTexture(cmd, m_Bright, m_Blur, material, PassBlur);

            // Combine scene + bloom into the working target.
            cmd.SetGlobalTexture(BloomTextureId, m_Blur);
            Blitter.BlitCameraTexture(cmd, source, cur, material, PassCombine);

            // Phase 3: SSAO (multiplies the whole image by ambient occlusion).
            if (m_Settings.ssaoEnabled)
            {
                Blitter.BlitCameraTexture(cmd, cur, m_SSAO1, material, PassSSAO);
                material.SetFloat("_BlurSize", Mathf.Max(0f, m_Settings.ssaoBlurSize));
                Blitter.BlitCameraTexture(cmd, m_SSAO1, m_SSAO2, material, PassBlur);
                cmd.SetGlobalTexture(SSAOTextureId, m_SSAO2);
                BlitAndSwap(cmd, ref cur, ref tmp, material, PassSSAOApply);
            }

            // Phase 3: motion blur (needs camera motion vectors).
            if (m_Settings.motionBlurEnabled)
            {
                BlitAndSwap(cmd, ref cur, ref tmp, material, PassMotionBlur);
            }

            // Phase 4: screen space reflection.
            if (m_Settings.ssrEnabled)
            {
                BlitAndSwap(cmd, ref cur, ref tmp, material, PassSSR);
            }

            // Phase 2 chain.
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassPixelize);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassDilation);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassSharpen);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassChromaticAberration);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassPosterize);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassFog);
            BlitAndSwap(cmd, ref cur, ref tmp, material, PassFilmGrain);

            // Phase 3: outline.
            if (m_Settings.outlineEnabled)
            {
                BlitAndSwap(cmd, ref cur, ref tmp, material, PassOutline);
            }

            // Phase 3: depth of field (blur a copy, then mix by circle of confusion).
            if (m_Settings.dofEnabled)
            {
                material.SetFloat("_BlurSize", Mathf.Max(0f, m_Settings.dofBlurSize));
                Blitter.BlitCameraTexture(cmd, cur, m_DOFBlur, material, PassBlur);
                cmd.SetGlobalTexture(DOFBlurTextureId, m_DOFBlur);
                BlitAndSwap(cmd, ref cur, ref tmp, material, PassDOFMix);
            }

            // Restore the bloom blur size for next frame.
            material.SetFloat("_BlurSize", Mathf.Max(0f, m_Settings.blurSize));

            // Gamma + LUT to camera target.
            Blitter.BlitCameraTexture(cmd, cur, renderer.cameraColorTargetHandle, material, PassFinal);

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