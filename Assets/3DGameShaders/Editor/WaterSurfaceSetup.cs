using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Wires the screen space water (Water.shader) onto the water mesh that already
// exists in the scene. It deliberately creates no geometry: the mill scene's
// "water-diffuse" mesh is the water.
//
// It also fixes the prerequisites the water samples depend on:
//   * camera depth texture  -> thickness, foam and the SSR march
//   * camera opaque texture -> refraction and reflection taps
//   * the post chain's full screen SSR is switched off, because it reflects the
//     whole frame and fights with the water's own screen space reflection
public static class WaterSurfaceSetup
{
    private const string RootDir       = "Assets/3DGameShaders";
    private const string TexturesDir   = RootDir + "/Textures";
    private const string MaterialsDir  = RootDir + "/Materials";
    private const string ShadersDir    = RootDir + "/Shaders";
    private const string SettingsDir   = RootDir + "/Settings";
    private const string ScenesDir     = RootDir + "/Scenes";

    private const string WaterShaderPath   = ShadersDir + "/Water.shader";
    private const string WaterMaterialName = "WaterSurface";
    private const string RendererDataPath  = SettingsDir + "/3DGameShadersRenderer.asset";
    private const string DemoScenePath     = ScenesDir + "/Demo.unity";

    // The OBJ section (and therefore the GameObject) that holds the river mesh.
    public const string WaterMeshName = "water-diffuse";
    public const string WheelMeshName = "wheel-diffuse";
    public const string WheelPivotName = "WheelPivot";

    [MenuItem("3DGameShaders/Setup Screen Space Water")]
    public static void SetupWater()
    {
        FixTextureImports();

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath);
        if (shader == null)
        {
            Debug.LogError("3DGameShaders: " + WaterShaderPath
                + " is missing or failed to compile.");
            return;
        }

        Material material = LoadOrCreateWaterMaterial();
        if (material == null)
        {
            return;
        }

        int assigned = AssignToWaterMeshes(material);
        if (assigned == 0)
        {
            Debug.LogError("3DGameShaders: no '" + WaterMeshName + "' mesh in the open scene. "
                + "Run '3DGameShaders > Add Mill Scene Model' first, or open the demo scene.");
            return;
        }

        bool cameraOk = EnableCameraTextures();
        bool ssrOff   = DisableFullScreenSSR();
        bool wheelOk  = SetupWaterWheel();

        AssetDatabase.SaveAssets();

        // The camera overrides and the material assignment only live in the
        // scene, so persist them. Skipped in play mode, where saving a scene is
        // not meaningful.
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = false;
        if (!Application.isPlaying && !string.IsNullOrEmpty(scene.path))
        {
            saved = EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("3DGameShaders: screen space water set up on " + assigned + " mesh(es)."
            + " camera depth+opaque " + (cameraOk ? "enabled" : "NOT FOUND")
            + ", full screen SSR " + (ssrOff ? "disabled" : "not present")
            + ", water wheel " + (wheelOk ? "spinning" : "not found")
            + ", scene " + (saved ? "saved"
                                  : "NOT saved - leave play mode and press Ctrl+S"));
    }

    // Batchmode entry point so the whole thing can be applied and verified from
    // the command line.
    public static void BatchSetupDemoScene()
    {
        if (!System.IO.File.Exists(DemoScenePath))
        {
            Debug.LogError("3DGameShaders: " + DemoScenePath + " not found.");
            return;
        }

        AssetDatabase.ImportAsset(WaterShaderPath, ImportAssetOptions.ForceUpdate);

        Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        SetupWater();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("3DGameShaders: batch water setup finished.");
    }

    // applyDefaults is only true when the material is created, or when the user
    // asks for it through "Reset Water Material To Defaults". That way tuning the
    // material by hand is not wiped out every time the scene is set up.
    [MenuItem("3DGameShaders/Reset Water Material To Defaults")]
    public static void ResetWaterMaterialToDefaults()
    {
        Material material = LoadOrCreateWaterMaterial(WaterMaterialName, applyDefaults: true);
        if (material == null)
        {
            return;
        }

        Debug.Log("3DGameShaders: " + WaterMaterialName + ".mat reset to the tutorial defaults "
            + "(flow speed 1.0, foam depth 1.5, refraction 24 px, SSR on).");
    }

    public static Material LoadOrCreateWaterMaterial(
        string materialName = WaterMaterialName, bool applyDefaults = false)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath);
        if (shader == null)
        {
            return null;
        }

        string path = MaterialsDir + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = false;
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            created = true;
        }

        material.shader = shader;
        material.SetTexture("_DiffuseMap", LoadTexture("water-diffuse.png"));
        material.SetTexture("_NormalMap", LoadTexture("water-normal.png"));
        material.SetTexture("_FlowMap", LoadTexture("water-flow.png"));
        material.SetTexture("_FoamPattern", LoadTexture("foam-pattern.png"));
        material.SetTexture("_SpecularMap", LoadTexture("water-specular.png"));
        AssignWaterMasks(material);

        if (!created && !applyDefaults)
        {
            EditorUtility.SetDirty(material);
            return material;
        }

        // The tutorial's constants. The river bed is ~3 units under the surface,
        // so _FoamDepth stays small: foam belongs on the water line of the wheel
        // and the dock, not over the whole river.
        material.SetFloat("_FlowSpeed", 1f);
        material.SetColor("_TintColor", new Color(0.392f, 0.537f, 0.561f, 1f));
        material.SetFloat("_TintStrength", 0.15f);
        material.SetFloat("_WaterDepth", 2f);
        // Clearer water: the body colour replaces less of the river bed.
        material.SetFloat("_WaterBodyStrength", 0.55f);
        material.SetFloat("_AmbientStrength", 0.8f);
        material.SetFloat("_RefractionStrength", 24f);
        material.SetFloat("_UseSSR", 1f);
        material.SetFloat("_SSRMaxDistance", 8f);
        material.SetFloat("_SSRResolution", 0.3f);
        material.SetFloat("_SSRSteps", 5f);
        material.SetFloat("_SSRThickness", 0.5f);
        // The masks carry the tutorial's 0.8 amount and 0.5 roughness, so these
        // sliders act as multipliers on top of them.
        material.SetFloat("_ReflectionAmount", 1f);
        material.SetFloat("_ReflectionRoughness", 1f);
        material.SetFloat("_EnvironmentStrength", 0.6f);
        material.SetColor("_FoamColor", new Color(0.8f, 0.85f, 0.92f, 1f));
        // The bank crosses the water plane, so a wider band than the tutorial's
        // 1.5 lets the shoreline foam actually reach the screen.
        material.SetFloat("_FoamDepth", 3.5f);
        material.SetFloat("_FoamIntensity", 1f);
        material.SetFloat("_FoamThreshold", 0.22f);
        material.SetFloat("_FoamSoftness", 0.3f);
        material.SetFloat("_FoamFalloff", 1.2f);
        material.SetFloat("_DebugView", 0f);
        material.SetFloat("_SpecularIntensity", 1f);

        EditorUtility.SetDirty(material);
        return material;
    }

    // The tutorial spins the wheel at -90 deg/s around its axle. The parsed OBJ
    // mesh has its transform origin at the world origin, so a pivot is inserted
    // on the wheel's own bounds centre and the wheel is parented under it.
    // geometry-buffer-1.frag writes the water's masks into the G-buffer next to the
    // position and normal: water-lp uses reflection-refraction.png for both the
    // reflection and the refraction stage (a flat texture whose red channel is the
    // reflection amount 0.8 and whose green channel is the water roughness 0.5),
    // while the channel caps and the river bed use blank.png - black - which is what
    // keeps those meshes out of the water effect entirely.
    public static void AssignWaterMasks(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetMaskImportSettings();

        Texture2D mask = LoadTexture("reflection-refraction.png");
        material.SetTexture("_ReflectionMaskMap", mask);
        material.SetTexture("_RefractionMaskMap", mask);
        material.SetFloat("_UseMasks", 1f);
        // The mask values are the tutorial's amount and roughness; these sliders
        // stay at one so the mask is not attenuated twice.
        material.SetFloat("_ReflectionAmount", 1f);
        material.SetFloat("_ReflectionRoughness", 1f);

        EditorUtility.SetDirty(material);
    }

    [MenuItem("3DGameShaders/Assign Water Masks")]
    public static void AssignWaterMasksToExistingMaterial()
    {
        Material material = LoadOrCreateWaterMaterial();
        AssignWaterMasks(material);
        AssetDatabase.SaveAssets();
        Debug.Log("3DGameShaders: reflection/refraction masks assigned to "
            + WaterMaterialName + ".mat");
    }

    // The masks are data (channel values), not colour, so they must not go through
    // an sRGB decode: 204/255 has to read as 0.8, not 0.6.
    private static void SetMaskImportSettings()
    {
        string path = TexturesDir + "/reflection-refraction.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }

    private static bool SetupWaterWheel()
    {
        GameObject wheel = GameObject.Find(WheelMeshName);
        if (wheel == null)
        {
            return false;
        }

        if (wheel.transform.parent != null && wheel.transform.parent.name == WheelPivotName)
        {
            return true;
        }

        MeshFilter filter = wheel.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return false;
        }

        Vector3 axle = wheel.transform.TransformPoint(filter.sharedMesh.bounds.center);

        GameObject pivot = new GameObject(WheelPivotName);
        pivot.transform.SetParent(wheel.transform.parent, false);
        pivot.transform.position = axle;
        pivot.transform.rotation = Quaternion.identity;

        wheel.transform.SetParent(pivot.transform, true);

        if (pivot.GetComponent<WaterWheel>() == null)
        {
            pivot.AddComponent<WaterWheel>();
        }

        EditorUtility.SetDirty(pivot);
        return true;
    }

    // Assigns the water material to the river mesh(es) already in the scene.
    // Nothing is instantiated: no extra plane, so the water can never spill
    // outside the modelled river.
    private static int AssignToWaterMeshes(Material material)
    {
        int count = 0;
        foreach (MeshRenderer meshRenderer in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (meshRenderer == null)
            {
                continue;
            }

            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            string objectName = meshRenderer.gameObject.name;
            string meshName   = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.name
                : "";
            bool isWater = objectName == WaterMeshName
                        || meshName == WaterMeshName
                        || objectName.StartsWith("water-");
            if (!isWater)
            {
                continue;
            }

            meshRenderer.sharedMaterial = material;
            EditorUtility.SetDirty(meshRenderer);
            count++;
        }
        return count;
    }

    private static bool EnableCameraTextures()
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            return false;
        }

        UniversalAdditionalCameraData cameraData =
            camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        // requiresColorOption is the camera override for the opaque texture.
        cameraData.requiresDepthOption = CameraOverrideOption.On;
        cameraData.requiresColorOption = CameraOverrideOption.On;
        EditorUtility.SetDirty(cameraData);
        return true;
    }

    private static bool DisableFullScreenSSR()
    {
        UniversalRendererData rendererData =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
        if (rendererData == null)
        {
            return false;
        }

        bool changed = false;
        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is PostProcessingFeature postProcessing && postProcessing.settings.ssrEnabled)
            {
                postProcessing.settings.ssrEnabled = false;
                EditorUtility.SetDirty(postProcessing);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(rendererData);
        }
        return changed;
    }

    private static void FixTextureImports()
    {
        // Flow vectors are data, not colour: sampling them through sRGB would
        // bend every direction in the flow map.
        SetTextureSettings("water-flow.png", linear: true, wrap: TextureWrapMode.Repeat);
        SetTextureSettings("foam-pattern.png", linear: false, wrap: TextureWrapMode.Repeat);
    }

    private static void SetTextureSettings(string fileName, bool linear, TextureWrapMode wrap)
    {
        string path = TexturesDir + "/" + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("3DGameShaders: texture not imported yet: " + path);
            return;
        }

        bool changed = false;
        if (importer.sRGBTexture == linear)
        {
            importer.sRGBTexture = !linear;
            changed = true;
        }
        if (importer.wrapMode != wrap)
        {
            importer.wrapMode = wrap;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Texture2D LoadTexture(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + fileName);
    }
}
