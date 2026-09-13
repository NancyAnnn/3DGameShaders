using System.Collections.Generic;
using UnityEngine;

// Runtime control layer for the demo, mirroring the original's keyboard scheme
// (see the tutorial's "Running The Demo" page) and adding an on screen panel so
// the effects can also be switched with the mouse.
//
// The original drives everything through Panda3D events and a status label; this
// drives the same set through PostProcessingFeature settings, the water material
// and the BaseLit material toggles.
[DisallowMultipleComponent]
public sealed class DemoEffectsController : MonoBehaviour
{
    [Header("Scene references")]
    public Light sunLight;
    public ParticleSystem smokeParticles;
    public AudioSource[] ambientSounds;
    public Renderer waterRenderer;

    [Header("Sun animation")]
    [Tooltip("Seconds for a full day/night turn. The tutorial uses 64.")]
    public float sunDayLength = 64f;
    public bool animateSun = true;

    [Header("Screen space water")]
    public float waterFoamDepthStep = 0.2f;
    public float waterRefractionStep = 2f;

    [Header("HUD")]
    public bool showPanel = true;
    public KeyCode togglePanelKey = KeyCode.F1;

    private const float StatusHoldSeconds = 2.5f;

    private readonly List<Material> m_BaseLitMaterials = new List<Material>();
    private Material m_WaterMaterial;

    private float m_StatusUntil;
    private string m_Status = "Ready";

    private bool m_SoundEnabled = true;
    private bool m_FlowMapping = true;

    // Effect states the panel and the keys share.
    private bool m_Fresnel = true;
    private bool m_Rim = true;
    private bool m_NormalMap = true;
    private bool m_BlinnPhong = true;
    private bool m_CelShading;
    private bool m_Particles = true;

    private float m_SunAngle = 260f;
    private float m_WaterFoamDepth;
    private float m_WaterRefraction;
    private float m_WaterRefractionDefault;
    private float m_WaterFoamDepthDefault;

    private Camera m_Camera;
    private Vector3 m_InitialCameraPosition;
    private Quaternion m_InitialCameraRotation;
    private float m_InitialFogNear;
    private float m_InitialFogFar;

    private void Start()
    {
        CollectMaterials();

        if (m_WaterMaterial != null)
        {
            m_WaterFoamDepth = m_WaterMaterial.GetFloat("_FoamDepth");
            m_WaterFoamDepthDefault = m_WaterFoamDepth;
            m_WaterRefraction = m_WaterMaterial.GetFloat("_RefractionStrength");
            m_WaterRefractionDefault = m_WaterRefraction;
        }

        m_Camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (m_Camera != null)
        {
            m_InitialCameraPosition = m_Camera.transform.position;
            m_InitialCameraRotation = m_Camera.transform.rotation;
        }

        PostProcessingFeature.Settings initial = CurrentSettings();
        if (initial != null)
        {
            m_InitialFogNear = initial.fogNear;
            m_InitialFogFar = initial.fogFar;
        }

        if (smokeParticles != null)
        {
            m_Particles = smokeParticles.gameObject.activeSelf;
        }

        SetSunAngle(m_SunAngle);
        PushBaseLitState();
        PushSoundState();
    }

    private void CollectMaterials()
    {
        m_BaseLitMaterials.Clear();

        foreach (MeshRenderer renderer in FindObjectsOfType<MeshRenderer>(true))
        {
            Material shared = renderer.sharedMaterial;
            if (shared == null)
            {
                continue;
            }

            if (shared.shader != null && shared.shader.name == "3DGameShaders/BaseLit")
            {
                // .material gives this renderer its own instance, so the switches
                // never write back into the project asset.
                m_BaseLitMaterials.Add(renderer.material);
            }
            else if (shared.shader != null && shared.shader.name == "3DGameShaders/WaterSurface")
            {
                m_WaterMaterial = renderer.material;
            }
        }
    }

    private void Update()
    {
        PostProcessingFeature.Settings settings = CurrentSettings();

        if (Input.GetKeyDown(togglePanelKey))
        {
            showPanel = !showPanel;
            ShowStatus(showPanel ? "Panel On" : "Panel Off");
        }

        // ---------------------------------------------------------------
        // Sun
        // ---------------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetSunAngle(90f);
            animateSun = false;
            ShowStatus("Midday");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetSunAngle(-90f);
            animateSun = false;
            ShowStatus("Midnight");
        }
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            animateSun = !animateSun;
            ShowStatus(animateSun ? "Sun Animation On" : "Sun Animation Off");
        }
        if (animateSun)
        {
            SetSunAngle(m_SunAngle - 360f * Time.deltaTime / Mathf.Max(sunDayLength, 1f));
        }

        // ---------------------------------------------------------------
        // Shading switches (BaseLit)
        // ---------------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Alpha3)) { m_Fresnel    = !m_Fresnel;    PushBaseLitState(); ShowStatus("Fresnel", m_Fresnel); }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { m_Rim        = !m_Rim;        PushBaseLitState(); ShowStatus("Rim Lighting", m_Rim); }
        if (Input.GetKeyDown(KeyCode.Alpha8)) { m_CelShading = !m_CelShading; PushBaseLitState(); ShowStatus("Cel Shading", m_CelShading); }
        if (Input.GetKeyDown(KeyCode.O))       { m_NormalMap  = !m_NormalMap;  PushBaseLitState(); ShowStatus("Normal Mapping", m_NormalMap); }
        if (Input.GetKeyDown(KeyCode.Alpha0))  { m_BlinnPhong = !m_BlinnPhong; PushBaseLitState(); ShowStatus(m_BlinnPhong ? "Blinn-Phong" : "Phong", m_BlinnPhong); }

        // ---------------------------------------------------------------
        // Post processing switches
        // ---------------------------------------------------------------
        if (settings != null)
        {
            if (Input.GetKeyDown(KeyCode.Y)) { settings.ssaoEnabled = !settings.ssaoEnabled; ShowStatus("SSAO", settings.ssaoEnabled); }
            if (Input.GetKeyDown(KeyCode.U)) { settings.outlineEnabled = !settings.outlineEnabled; ShowStatus("Outlining", settings.outlineEnabled); }
            if (Input.GetKeyDown(KeyCode.I)) { settings.bloomEnabled = !settings.bloomEnabled; ShowStatus("Bloom", settings.bloomEnabled); }
            if (Input.GetKeyDown(KeyCode.P)) { settings.fogEnabled = !settings.fogEnabled; ShowStatus("Fog", settings.fogEnabled); }
            if (Input.GetKeyDown(KeyCode.H)) { settings.dofEnabled = !settings.dofEnabled; ShowStatus("Depth Of Field", settings.dofEnabled); }
            if (Input.GetKeyDown(KeyCode.J)) { settings.posterizeEnabled = !settings.posterizeEnabled; ShowStatus("Posterization", settings.posterizeEnabled); }
            if (Input.GetKeyDown(KeyCode.K)) { settings.pixelizeEnabled = !settings.pixelizeEnabled; ShowStatus("Pixelization", settings.pixelizeEnabled); }
            if (Input.GetKeyDown(KeyCode.L)) { settings.sharpenEnabled = !settings.sharpenEnabled; ShowStatus("Sharpen", settings.sharpenEnabled); }
            if (Input.GetKeyDown(KeyCode.N)) { settings.filmGrainEnabled = !settings.filmGrainEnabled; ShowStatus("Film Grain", settings.filmGrainEnabled); }
            if (Input.GetKeyDown(KeyCode.Alpha6)) { settings.motionBlurEnabled = !settings.motionBlurEnabled; ShowStatus("Motion Blur", settings.motionBlurEnabled); }
            if (Input.GetKeyDown(KeyCode.Alpha7)) { settings.kuwaharaEnabled = !settings.kuwaharaEnabled; ShowStatus("Painterly", settings.kuwaharaEnabled); }
            if (Input.GetKeyDown(KeyCode.Alpha9)) { settings.lutEnabled = !settings.lutEnabled; ShowStatus("Lookup Table", settings.lutEnabled); }
            if (Input.GetKeyDown(KeyCode.Backslash)) { settings.chromaticAberrationEnabled = !settings.chromaticAberrationEnabled; ShowStatus("Chromatic Aberration", settings.chromaticAberrationEnabled); }
        }

        // ---------------------------------------------------------------
        // Water switches
        // ---------------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.M))
        {
            float enabled = m_WaterMaterial != null ? m_WaterMaterial.GetFloat("_UseSSR") : 0f;
            bool next = enabled < 0.5f;
            SetWater("_UseSSR", next ? 1f : 0f);
            ShowStatus("Screen Space Reflection", next);
        }
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            bool next = m_WaterRefraction > 0.01f;
            SetWater("_RefractionStrength", next ? 0f : m_WaterRefractionDefault);
            ShowStatus("Screen Space Refraction", !next);
        }
        if (Input.GetKeyDown(KeyCode.Period))
        {
            m_FlowMapping = !m_FlowMapping;
            SetWater("_FlowSpeed", m_FlowMapping ? 0.05f : 0f);
            PushSoundState();
            ShowStatus("Flow Maps", m_FlowMapping);
        }

        // Foam and refraction adjustments, like the tutorial's - and = keys.
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            float dir = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1f : 1f;
            m_WaterFoamDepth = Mathf.Clamp(m_WaterFoamDepth + dir * waterFoamDepthStep, 0.001f, 12f);
            SetWater("_FoamDepth", m_WaterFoamDepth);
            ShowStatus("Foam Depth " + m_WaterFoamDepth.ToString("0.0"));
        }
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            float dir = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1f : 1f;
            m_WaterRefraction = Mathf.Clamp(m_WaterRefraction + dir * waterRefractionStep, 0f, 64f);
            SetWater("_RefractionStrength", m_WaterRefraction);
            ShowStatus("Refraction Offset " + m_WaterRefraction.ToString("0"));
        }

        // ---------------------------------------------------------------
        // Particles, sound, fog
        // ---------------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            m_Particles = !m_Particles;
            if (smokeParticles != null)
            {
                smokeParticles.gameObject.SetActive(m_Particles);
            }
            ShowStatus("Particles", m_Particles);
        }
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            m_SoundEnabled = !m_SoundEnabled;
            PushSoundState();
            ShowStatus("Sound", m_SoundEnabled);
        }
        if (settings != null)
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                float dir = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1f : -1f;
                settings.fogNear = Mathf.Max(0f, settings.fogNear + dir * 2f);
                ShowStatus("Fog Near " + settings.fogNear.ToString("0.0"));
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                float dir = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1f : 1f;
                settings.fogFar = Mathf.Max(settings.fogNear + 1f, settings.fogFar + dir * 2f);
                ShowStatus("Fog Far " + settings.fogFar.ToString("0.0"));
            }
        }

        // ---------------------------------------------------------------
        // Middle mouse sets the chromatic aberration focus point.
        // ---------------------------------------------------------------
        if (settings != null && Input.GetMouseButton(2))
        {
            settings.caFocusPoint = new Vector2(
                Mathf.Clamp01(Input.mousePosition.x / Mathf.Max(Screen.width, 1)),
                Mathf.Clamp01(Input.mousePosition.y / Mathf.Max(Screen.height, 1)));
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetScene();
            ShowStatus("Reset");
        }
    }

    // The tutorial's r key restores the camera, the fog, the foam depth, the
    // refraction and the mouse focus point.
    private void ResetScene()
    {
        if (m_Camera != null)
        {
            m_Camera.transform.SetPositionAndRotation(m_InitialCameraPosition, m_InitialCameraRotation);
            OrbitCamera orbit = m_Camera.GetComponent<OrbitCamera>();
            if (orbit != null)
            {
                orbit.SyncFromTransform();
            }
        }

        PostProcessingFeature.Settings settings = CurrentSettings();
        if (settings != null)
        {
            settings.fogNear = m_InitialFogNear;
            settings.fogFar = m_InitialFogFar;
            settings.caFocusPoint = new Vector2(0.5f, 0.5f);
        }

        m_WaterFoamDepth = m_WaterFoamDepthDefault;
        m_WaterRefraction = m_WaterRefractionDefault;
        SetWater("_FoamDepth", m_WaterFoamDepth);
        SetWater("_RefractionStrength", m_WaterRefraction);
    }

    private void SetSunAngle(float degrees)
    {
        m_SunAngle = degrees;

        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(m_SunAngle, -30f, 0f);
            // Below the horizon fades the light out, like the tutorial's night.
            float elevation = Mathf.Sin(m_SunAngle * Mathf.Deg2Rad);
            sunLight.intensity = Mathf.Clamp01(elevation) * 1.2f;
        }

        PostProcessingFeature.Settings settings = CurrentSettings();
        if (settings != null)
        {
            // The lookup table blends between its two tables with this value.
            settings.sunPosition = Mathf.Repeat(m_SunAngle / 360f, 1f);
        }
    }

    private void PushBaseLitState()
    {
        foreach (Material material in m_BaseLitMaterials)
        {
            if (material == null)
            {
                continue;
            }

            material.SetFloat("_NormalMapOn", m_NormalMap ? 1f : 0f);
            material.SetFloat("_FresnelOn", m_Fresnel ? 1f : 0f);
            material.SetFloat("_RimOn", m_Rim ? 1f : 0f);
            material.SetFloat("_BlinnPhongOn", m_BlinnPhong ? 1f : 0f);
            material.SetFloat("_CelShading", m_CelShading ? 1f : 0f);
        }
    }

    private void PushSoundState()
    {
        if (ambientSounds == null)
        {
            return;
        }

        // The tutorial stops the ambience when the flow maps are switched off and
        // when the sound is muted.
        bool playing = m_SoundEnabled && m_FlowMapping;
        foreach (AudioSource source in ambientSounds)
        {
            if (source == null)
            {
                continue;
            }

            source.mute = !playing;
            if (playing && !source.isPlaying)
            {
                source.Play();
            }
        }
    }

    private void SetWater(string property, float value)
    {
        if (m_WaterMaterial != null)
        {
            m_WaterMaterial.SetFloat(property, value);
        }
    }

    private static PostProcessingFeature.Settings CurrentSettings()
    {
        PostProcessingFeature feature = PostProcessingFeature.Instance;
        return feature != null ? feature.settings : null;
    }

    private void ShowStatus(string text, bool enabled = true)
    {
        m_Status = enabled ? text + " On" : text + " Off";
        m_StatusUntil = Time.time + StatusHoldSeconds;
    }

    private void OnGUI()
    {
        if (m_StatusUntil > Time.time)
        {
            GUI.Label(new Rect(16f, Screen.height - 40f, 640f, 28f), m_Status);
        }

        if (!showPanel)
        {
            GUI.Label(new Rect(16f, Screen.height - 64f, 640f, 24f),
                "F1: show controls");
            return;
        }

        PostProcessingFeature.Settings settings = CurrentSettings();

        GUILayout.BeginArea(new Rect(Screen.width - 300f, 16f, 284f, Screen.height - 32f), GUI.skin.box);
        GUILayout.Label("3D Game Shaders - effects");

        GUILayout.Label("-- shading --");
        m_Fresnel    = GUILayout.Toggle(m_Fresnel, " Fresnel (3)");
        m_Rim        = GUILayout.Toggle(m_Rim, " Rim lighting (4)");
        m_NormalMap  = GUILayout.Toggle(m_NormalMap, " Normal mapping (O)");
        m_BlinnPhong = GUILayout.Toggle(m_BlinnPhong, " Blinn-Phong (0)");
        m_CelShading = GUILayout.Toggle(m_CelShading, " Cel shading (8)");

        GUILayout.Label("-- water --");
        if (m_WaterMaterial != null)
        {
            bool ssr = m_WaterMaterial.GetFloat("_UseSSR") > 0.5f;
            bool nextSsr = GUILayout.Toggle(ssr, " Reflection (M)");
            if (nextSsr != ssr)
            {
                SetWater("_UseSSR", nextSsr ? 1f : 0f);
            }

            GUILayout.Label(" Foam depth " + m_WaterFoamDepth.ToString("0.0"));
            m_WaterFoamDepth = GUILayout.HorizontalSlider(m_WaterFoamDepth, 0.001f, 12f);
            SetWater("_FoamDepth", m_WaterFoamDepth);

            GUILayout.Label(" Refraction " + m_WaterRefraction.ToString("0"));
            m_WaterRefraction = GUILayout.HorizontalSlider(m_WaterRefraction, 0f, 64f);
            SetWater("_RefractionStrength", m_WaterRefraction);
        }

        if (settings != null)
        {
            GUILayout.Label("-- post --");
            settings.bloomEnabled              = GUILayout.Toggle(settings.bloomEnabled, " Bloom (I)");
            settings.ssaoEnabled               = GUILayout.Toggle(settings.ssaoEnabled, " SSAO (Y)");
            settings.outlineEnabled            = GUILayout.Toggle(settings.outlineEnabled, " Outlining (U)");
            settings.fogEnabled                = GUILayout.Toggle(settings.fogEnabled, " Fog (P)");
            settings.dofEnabled                = GUILayout.Toggle(settings.dofEnabled, " Depth of field (H)");
            settings.posterizeEnabled          = GUILayout.Toggle(settings.posterizeEnabled, " Posterize (J)");
            settings.pixelizeEnabled           = GUILayout.Toggle(settings.pixelizeEnabled, " Pixelize (K)");
            settings.sharpenEnabled            = GUILayout.Toggle(settings.sharpenEnabled, " Sharpen (L)");
            settings.filmGrainEnabled          = GUILayout.Toggle(settings.filmGrainEnabled, " Film grain (N)");
            settings.motionBlurEnabled         = GUILayout.Toggle(settings.motionBlurEnabled, " Motion blur (6)");
            settings.kuwaharaEnabled           = GUILayout.Toggle(settings.kuwaharaEnabled, " Painterly Kuwahara (7)");
            settings.lutEnabled                = GUILayout.Toggle(settings.lutEnabled, " Lookup table (9)");
            settings.chromaticAberrationEnabled = GUILayout.Toggle(settings.chromaticAberrationEnabled, " Chromatic aberration (\\)");

            GUILayout.Label("-- scene --");
            m_Particles = GUILayout.Toggle(m_Particles, " Smoke particles (5)");
            if (smokeParticles != null && smokeParticles.gameObject.activeSelf != m_Particles)
            {
                smokeParticles.gameObject.SetActive(m_Particles);
            }

            m_SoundEnabled = GUILayout.Toggle(m_SoundEnabled, " Sound (Del)");
            PushSoundState();

            animateSun = GUILayout.Toggle(animateSun, " Sun animation (/)");
            GUILayout.Label(" Sun " + m_SunAngle.ToString("0") + " deg");
            float sun = GUILayout.HorizontalSlider(m_SunAngle, -180f, 180f);
            if (!Mathf.Approximately(sun, m_SunAngle))
            {
                SetSunAngle(sun);
            }
        }

        PushBaseLitState();
        GUILayout.EndArea();
    }
}
