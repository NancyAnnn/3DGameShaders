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
    private const string WeatherVaneName = "weather-vane-diffuse";
    private const string WeatherVanePivotName = "WeatherVanePivot";
    private const string SmokeName = "SmokeParticles";
    private const string ControllerName = "DemoController";

    // Panda3D places the smoke node at (0.47, 4.5, 8.9) which in Unity's y-up
    // axes is (0.47, 8.9, -4.5). The roof surface in that column measures
    // y = 8.64, so the tutorial's height already sits in the chimney mouth -
    // that is what makes the plume read as coming out of the chimney.
    private static readonly Vector3 SmokePosition = new Vector3(0.47f, 8.9f, -4.5f);

    [MenuItem("3DGameShaders/Setup Demo Extras (Audio, Particles, Controls)")]
    public static void SetupExtras()
    {
        SetupExtrasInternal(false);
    }

    // Re-applies the particle settings over an existing system.
    [MenuItem("3DGameShaders/Reset Smoke Particles")]
    public static void ResetSmokeParticles()
    {
        SetupExtrasInternal(true);
    }

    private static void SetupExtrasInternal(bool applyParticleDefaults)
    {
        FixLookupTableImports();

        int sounds = SetupAudio();
        bool smoke = SetupSmoke(applyParticleDefaults);
        bool vane = SetupWeatherVane();
        bool controller = SetupController();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);

        bool saved = false;
        if (!Application.isPlaying && !string.IsNullOrEmpty(scene.path))
        {
            saved = EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("3DGameShaders: demo extras set up. sounds=" + sounds
            + " smoke=" + smoke + " weatherVane=" + vane + " controller=" + controller
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
    private static bool SetupSmoke(bool applyDefaults)
    {
        Texture2D smokeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/smoke.png");
        if (smokeTexture == null)
        {
            Debug.LogWarning("3DGameShaders: missing " + TexturesDir + "/smoke.png");
            return false;
        }

        GameObject go = GameObject.Find(SmokeName);
        bool created = go == null;
        if (created)
        {
            go = new GameObject(SmokeName);
            // Unity's cone shape emits along the object's local +Z axis, so a
            // fresh particle object needs the standard -90 tilt about X for the
            // plume to start out going up.
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        go.transform.position = SmokePosition;

        ParticleSystem particles = go.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = go.AddComponent<ParticleSystem>();
        }
        else if (!applyDefaults)
        {
            // Keep whatever was tuned in the inspector.
            return true;
        }

        // Reset path: restore the emission axis too.
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        // Adding the component runs the default Play; stop it so the settings
        // below are what actually gets emitted.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.02f;             // buoyancy, so it keeps rising
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 75;                    // tutorial pool size

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        // A tight column so the smoke leaves the chimney as a plume rather than
        // a wide puff that immediately spreads into the roof.
        shape.angle = 6f;
        shape.radius = 0.08f;

        // Without this the smoke only hovers at the chimney mouth: the original
        // pushes the particles with an offset force, so add a steady updraft plus
        // a sideways wind so the plume drifts away.
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        // The plume drifts the way the water reads as flowing. The flow map is a
        // constant (0, +0.25) in uv space and the shaders sample at
        // "uv + flow * time", so the pattern appears to travel along -v; the
        // river's uv-to-world mapping turns that into a world direction.
        Vector3 downstream;
        if (!TryGetWaterFlowDirection(out downstream))
        {
            downstream = Vector3.forward;   // measured value for the mill river
            Debug.LogWarning("3DGameShaders: could not read the river uv axes, "
                + "defaulting the smoke wind to world +Z.");
        }

        // Unity requires all three velocity curves to share one mode, so every
        // axis is a curve here - mixing a curve with a constant throws
        // "Particle Velocity curves must all be in the same mode".
        velocity.x = new ParticleSystem.MinMaxCurve(1f, WindCurve(downstream.x));
        velocity.z = new ParticleSystem.MinMaxCurve(1f, WindCurve(downstream.z));
        velocity.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0.8f));

        Debug.Log("3DGameShaders: smoke wind direction (down stream) = "
            + downstream.ToString("F3"));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 1f));

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

    // Wind speed along one axis over the particle's life:
    //   birth      -> a slight lean already (0.4 against a 2.2 rise is ~10 deg)
    //   0 .. 0.25  -> barely more while the smoke climbs out of the chimney; the
    //                 ridge beside it reaches y = 10.05 and, at the slowest rise,
    //                 clearing it takes about a quarter of the particle's life
    //   0.25 .. 1  -> the lean builds steadily, ending near 47 degrees
    private static AnimationCurve WindCurve(float alongAxis)
    {
        Keyframe[] keys =
        {
            new Keyframe(0f, 0.4f * alongAxis),
            new Keyframe(0.25f, 0.7f * alongAxis),
            new Keyframe(1f, 2.4f * alongAxis),
        };
        return new AnimationCurve(keys);
    }

    // Recovers the world direction the river's textures appear to travel in. The
    // uv axes are linear across the flat river surface, so two vertices that
    // share u and differ in v give the world direction of the v axis. The
    // shaders sample at "uv + flow * time", which makes the pattern travel along
    // -v, so downstream is the opposite of the v axis.
    private static bool TryGetWaterFlowDirection(out Vector3 downstream)
    {
        downstream = Vector3.forward;

        GameObject water = GameObject.Find(WaterMeshName);
        if (water == null)
        {
            return false;
        }

        MeshFilter filter = water.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return false;
        }

        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length != vertices.Length)
        {
            return false;
        }

        float bestDeltaV = 0f;
        int bestA = -1;
        int bestB = -1;
        for (int a = 0; a < uvs.Length; a++)
        {
            for (int b = a + 1; b < uvs.Length; b++)
            {
                if (Mathf.Abs(uvs[a].x - uvs[b].x) > 0.02f)
                {
                    continue;
                }

                float deltaV = Mathf.Abs(uvs[a].y - uvs[b].y);
                if (deltaV > bestDeltaV)
                {
                    bestDeltaV = deltaV;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA < 0 || bestDeltaV < 0.2f)
        {
            return false;
        }

        Vector3 alongV = (vertices[bestB] - vertices[bestA])
                       / (uvs[bestB].y - uvs[bestA].y);
        Vector3 flat = new Vector3(-alongV.x, 0f, -alongV.z);
        if (flat.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        downstream = water.transform.TransformDirection(flat.normalized);
        return true;
    }

    private static Material LoadOrCreateSmokeMaterial(Texture2D smokeTexture)
    {
        string path = MaterialsDir + "/Smoke.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("3DGameShaders/Smoke"));
            AssetDatabase.CreateAsset(material, path);
        }

        Shader smokeShader = Shader.Find("3DGameShaders/Smoke");
        if (smokeShader != null)
        {
            material.shader = smokeShader;
        }

        material.SetTexture("_MainTex", smokeTexture);
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // The tutorial samples the lookup tables raw and wraps its own sRGB
    // round-trip around the lookup. Unity must therefore hand the shader the
    // stored texel values instead of decoding them first.
    private static void FixLookupTableImports()
    {
        SetTextureLinear("lookup-table-0.png");
        SetTextureLinear("lookup-table-1.png");
    }

    private static void SetTextureLinear(string fileName)
    {
        string path = TexturesDir + "/" + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || !importer.sRGBTexture)
        {
            return;
        }

        importer.sRGBTexture = false;
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------
    // Controller: wires the runtime switches to the objects above.
    // ---------------------------------------------------------------
    // The original loops a baked "weather-vane-shake" animation on the vane
    // node; the OBJ carries no animation, so the vane gets a pivot on its own
    // axle and turns in place.
    private static bool SetupWeatherVane()
    {
        GameObject vane = GameObject.Find(WeatherVaneName);
        if (vane == null)
        {
            return false;
        }

        if (vane.transform.parent != null && vane.transform.parent.name == WeatherVanePivotName)
        {
            return true;
        }

        MeshFilter filter = vane.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return false;
        }

        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 axle = vane.transform.TransformPoint(
            new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));

        GameObject pivot = new GameObject(WeatherVanePivotName);
        pivot.transform.SetParent(vane.transform.parent, false);
        pivot.transform.position = axle;
        pivot.transform.rotation = Quaternion.identity;

        vane.transform.SetParent(pivot.transform, true);

        if (pivot.GetComponent<WeatherVane>() == null)
        {
            pivot.AddComponent<WeatherVane>();
        }

        EditorUtility.SetDirty(pivot);
        return true;
    }

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
