using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the parts of the original demo that are not shader work: the positional
// ambience, the chimney smoke and the runtime control layer.
public static class DemoSceneSetup
{
    private const string RootDir     = "Assets/3DGameShaders";
    private const string SoundsDir   = RootDir + "/Sounds";
    private const string TexturesDir = RootDir + "/Textures";
    private const string MaterialsDir = RootDir + "/Materials";
    private const string ScenesDir   = RootDir + "/Scenes";

    private const string WaterMeshName = "water-diffuse";
    private const string WheelMeshName = "wheel-diffuse";
    private const string WheelPivotName = "WheelPivot";
    private const string SmokeName = "SmokeParticles";
    private const string ControllerName = "DemoController";

    // Panda3D places the smoke node at (0.47, 4.5, 8.9); converted to Unity's
    // y-up axes that is y = 8.9, above the mill house chimney.
    private static readonly Vector3 SmokePosition = new Vector3(0.47f, 8.9f, -4.5f);

    [MenuItem("3DGameShaders/Setup Demo Extras (Audio, Particles, Controls)")]
    public static void SetupExtras()
    {
        int sounds = SetupAudio();
        bool smoke = SetupSmoke();
        bool controller = SetupController();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);

        bool saved = false;
        if (!Application.isPlaying && !string.IsNullOrEmpty(scene.path))
        {
            saved = EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("3DGameShaders: demo extras set up. sounds=" + sounds
            + " smoke=" + smoke + " controller=" + controller
            + " scene=" + (saved ? "saved" : "NOT saved (leave play mode and press Ctrl+S)"));
    }

    // ---------------------------------------------------------------
    // Audio: the tutorial plays wheel.ogg and water.ogg as positional sounds at
    // the wheel and the water, with min distances of 60 and 50.
    // ---------------------------------------------------------------
    private static int SetupAudio()
    {
        int count = 0;
        count += SetupOneSound("Sound_Wheel", "wheel.ogg", WheelMeshName, WheelPivotName, 60f);
        count += SetupOneSound("Sound_Water", "water.ogg", WaterMeshName, null, 50f);
        return count;
    }

    private static int SetupOneSound(
        string objectName, string clipName, string anchorName, string preferredAnchor, float minDistance)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundsDir + "/" + clipName);
        if (clip == null)
        {
            Debug.LogWarning("3DGameShaders: missing sound " + SoundsDir + "/" + clipName);
            return 0;
        }

        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            go = new GameObject(objectName);
        }

        GameObject anchor = preferredAnchor != null ? GameObject.Find(preferredAnchor) : null;
        if (anchor == null)
        {
            anchor = GameObject.Find(anchorName);
        }
        go.transform.position = anchor != null ? anchor.transform.position : Vector3.zero;

        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null)
        {
            source = go.AddComponent<AudioSource>();
        }

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 1f;                  // fully positional
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = minDistance * 4f;
        source.dopplerLevel = 0f;

        EditorUtility.SetDirty(source);
        return 1;
    }

    // ---------------------------------------------------------------
    // Smoke: setUpParticles in main.cxx - 75 particle pool, ~3 second life,
    // rising slowly, fading from white to dark blue.
    // ---------------------------------------------------------------
    private static bool SetupSmoke()
    {
        Texture2D smokeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/smoke.png");
        if (smokeTexture == null)
        {
            Debug.LogWarning("3DGameShaders: missing " + TexturesDir + "/smoke.png");
            return false;
        }

        GameObject go = GameObject.Find(SmokeName);
        if (go == null)
        {
            go = new GameObject(SmokeName);
        }
        go.transform.position = SmokePosition;
        go.transform.rotation = Quaternion.identity;

        ParticleSystem particles = go.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = go.AddComponent<ParticleSystem>();
        }

        // Adding the component runs the default Play; stop it so the settings
        // below are what actually gets emitted.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.015f;            // smoke rises
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 75;                    // tutorial pool size

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.12f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.15f, 1f, 1f));

        // Colour ramp from the tutorial: white smoke settling into dark blue,
        // fading out at the end.
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.039f, 0.078f, 0.156f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.35f, 0.35f),
                new GradientAlphaKey(0f, 1f),
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = LoadOrCreateSmokeMaterial(smokeTexture);

        EditorUtility.SetDirty(particles);
        return true;
    }

    private static Material LoadOrCreateSmokeMaterial(Texture2D smokeTexture)
    {
        string path = MaterialsDir + "/Smoke.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            // Sprites/Default is an unlit, alpha blended shader that works under
            // URP and takes particle vertex colours, which is what the original's
            // sprite particle renderer does.
            Shader shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_MainTex", smokeTexture);
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------------------------------------------------------------
    // Controller: wires the runtime switches to the objects above.
    // ---------------------------------------------------------------
    private static bool SetupController()
    {
        GameObject go = GameObject.Find(ControllerName);
        if (go == null)
        {
            go = new GameObject(ControllerName);
        }

        DemoEffectsController controller = go.GetComponent<DemoEffectsController>();
        if (controller == null)
        {
            controller = go.AddComponent<DemoEffectsController>();
        }

        Light sun = null;
        foreach (Light light in Object.FindObjectsOfType<Light>(true))
        {
            if (light.type == LightType.Directional)
            {
                sun = light;
                break;
            }
        }
        controller.sunLight = sun;

        GameObject smoke = GameObject.Find(SmokeName);
        controller.smokeParticles = smoke != null ? smoke.GetComponent<ParticleSystem>() : null;

        List<AudioSource> sources = new List<AudioSource>();
        foreach (AudioSource source in Object.FindObjectsOfType<AudioSource>(true))
        {
            sources.Add(source);
        }
        controller.ambientSounds = sources.ToArray();

        GameObject water = GameObject.Find(WaterMeshName);
        controller.waterRenderer = water != null ? water.GetComponent<Renderer>() : null;

        EditorUtility.SetDirty(controller);
        return true;
    }
}
