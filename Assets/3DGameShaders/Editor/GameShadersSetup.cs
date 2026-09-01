using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GameShadersSetup
{
    private const string RootDir = "Assets/3DGameShaders";
    private const string ScenesDir = RootDir + "/Scenes";
    private const string ShadersDir = RootDir + "/Shaders";
    private const string TexturesDir = RootDir + "/Textures";
    private const string SettingsDir = RootDir + "/Settings";
    private const string MaterialsDir = RootDir + "/Materials";

    private static readonly string[] NormalMaps =
    {
        "earth-normal.png",
        "wheel-normal.png",
        "house-normal.png",
        "water-normal.png"
    };

    [MenuItem("3DGameShaders/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureFolder(ScenesDir);
        EnsureFolder(ShadersDir);
        EnsureFolder(TexturesDir);
        EnsureFolder(SettingsDir);
        EnsureFolder(MaterialsDir);
        AssetDatabase.Refresh();

        FixTextureImportSettings();

        SetupUrp(out UniversalRenderPipelineAsset pipeline, out UniversalRendererData rendererData);

        Shader baseShader = LoadShader(ShadersDir + "/BaseLit.shader", "3DGameShaders/BaseLit");
        Shader postShader = LoadShader(ShadersDir + "/PostProcessing.shader", "Hidden/3DGameShaders/PostProcessing");
        if (baseShader == null || postShader == null)
        {
            Debug.LogError("3DGameShaders: shaders not imported yet. Re-run after compilation finishes.");
            return;
        }

        Texture2D gridDiffuse   = LoadTexture("grid-floor-diffuse.png");
        Texture2D earthDiffuse  = LoadTexture("earth-diffuse.png");
        Texture2D earthNormal   = LoadTexture("earth-normal.png");
        Texture2D earthSpecular = LoadTexture("earth-specular.png");
        Texture2D wheelDiffuse  = LoadTexture("wheel-diffuse.png");
        Texture2D wheelNormal   = LoadTexture("wheel-normal.png");
        Texture2D wheelSpecular = LoadTexture("wheel-specular.png");
        Texture2D houseDiffuse  = LoadTexture("house-diffuse.png");
        Texture2D houseNormal   = LoadTexture("house-normal.png");
        Texture2D houseSpecular = LoadTexture("house-specular.png");
        Texture2D waterDiffuse  = LoadTexture("water-diffuse.png");
        Texture2D waterNormal   = LoadTexture("water-normal.png");
        Texture2D waterSpecular = LoadTexture("water-specular.png");
        Texture2D lut0          = LoadTexture("lookup-table-0.png");
        Texture2D lut1          = LoadTexture("lookup-table-1.png");

        Material groundMat = CreateBaseMaterial("Ground", baseShader, gridDiffuse, null, null);
        Material earthMat  = CreateBaseMaterial("Earth",  baseShader, earthDiffuse, earthNormal, earthSpecular);
        Material wheelMat  = CreateBaseMaterial("Wheel",  baseShader, wheelDiffuse, wheelNormal, wheelSpecular);
        Material houseMat  = CreateBaseMaterial("House",  baseShader, houseDiffuse, houseNormal, houseSpecular);
        Material waterMat  = CreateBaseMaterial("Water",  baseShader, waterDiffuse, waterNormal, waterSpecular);

        Material lampWarm = CreateBaseMaterial("LampWarm", baseShader, Texture2D.whiteTexture, null, null);
        lampWarm.SetColor("_EmissionColor", new Color(8f, 6f, 4f, 1f));
        EditorUtility.SetDirty(lampWarm);

        Material lampCool = CreateBaseMaterial("LampCool", baseShader, Texture2D.whiteTexture, null, null);
        lampCool.SetColor("_EmissionColor", new Color(4f, 6f, 8f, 1f));
        EditorUtility.SetDirty(lampCool);

        Material postMaterial = LoadOrCreatePostMaterial(postShader);

        EnsureFeature(rendererData, postMaterial, lut0, lut1);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();

        BuildScene(groundMat, earthMat, wheelMat, houseMat, waterMat, lampWarm, lampCool);

        Debug.Log("3DGameShaders: demo scene built. Press Play to run.");
    }

    [MenuItem("3DGameShaders/Repair Renderer Feature")]
    public static void RepairRendererFeature()
    {
        EnsureFolder(SettingsDir);
        EnsureFolder(MaterialsDir);
        AssetDatabase.Refresh();

        UniversalRendererData rendererData =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/3DGameShadersRenderer.asset");
        if (rendererData == null)
        {
            Debug.LogError("3DGameShaders: renderer asset not found. Run '3DGameShaders > Build Demo Scene' first.");
            return;
        }

        Shader postShader = LoadShader(ShadersDir + "/PostProcessing.shader", "Hidden/3DGameShaders/PostProcessing");
        Material postMaterial = LoadOrCreatePostMaterial(postShader);
        Texture2D lut0 = LoadTexture("lookup-table-0.png");
        Texture2D lut1 = LoadTexture("lookup-table-1.png");

        EnsureFeature(rendererData, postMaterial, lut0, lut1);

        UniversalRenderPipelineAsset pipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsDir + "/3DGameShadersURP.asset");
        if (pipeline != null)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        Debug.Log("3DGameShaders: renderer feature repaired (null entries removed).");
    }

    private static void SetupUrp(out UniversalRenderPipelineAsset pipeline, out UniversalRendererData rendererData)
    {
        string urpPath = SettingsDir + "/3DGameShadersURP.asset";
        string rendererPath = SettingsDir + "/3DGameShadersRenderer.asset";

        pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
        rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);

        if (pipeline != null)
        {
            return;
        }

        if (rendererData == null)
        {
            UniversalRendererData template =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
            rendererData = template != null
                ? Object.Instantiate(template)
                : ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, rendererPath);
        }

        pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipeline, urpPath);
    }

    private static void EnsureFeature(UniversalRendererData rendererData, Material postMaterial, Texture2D lut0, Texture2D lut1)
    {
        // Remove broken/null entries left behind by earlier versions of the setup.
        rendererData.rendererFeatures.RemoveAll(feature => feature == null);

        PostProcessingFeature feature = null;
        for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
        {
            if (rendererData.rendererFeatures[i] is PostProcessingFeature existing)
            {
                feature = existing;
                break;
            }
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<PostProcessingFeature>();
            feature.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
        }

        feature.settings.material = postMaterial;
        feature.settings.bloomEnabled = true;
        feature.settings.lutEnabled = true;
        feature.settings.lut0 = lut0;
        feature.settings.lut1 = lut1;

        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(feature);
        AssetDatabase.SaveAssets();
    }

    private static void BuildScene(
        Material groundMat, Material earthMat, Material wheelMat, Material houseMat,
        Material waterMat, Material lampWarm, Material lampCool)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera camera = Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            camera = cameraGo.AddComponent<Camera>();
        }
        camera.transform.position = new Vector3(0f, 2.2f, -7f);
        camera.transform.LookAt(Vector3.zero);
        if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
        {
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        GameObject pivot = new GameObject("Pivot");
        pivot.transform.position = Vector3.zero;

        OrbitCamera orbit = camera.GetComponent<OrbitCamera>();
        if (orbit == null)
        {
            orbit = camera.gameObject.AddComponent<OrbitCamera>();
        }
        orbit.target = pivot.transform;
        orbit.distance = 8f;

        Light sun = Object.FindObjectOfType<Light>();
        if (sun == null)
        {
            GameObject sunGo = new GameObject("Directional Light");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.intensity = 1.2f;
        sun.color = new Color(1f, 0.95f, 0.85f);
        if (sun.GetComponent<UniversalAdditionalLightData>() == null)
        {
            sun.gameObject.AddComponent<UniversalAdditionalLightData>();
        }

        CreatePrimitive(PrimitiveType.Plane,  "Ground",   new Vector3(0f, 0f, 0f),   Quaternion.identity,          new Vector3(4f, 1f, 4f), groundMat);
        CreatePrimitive(PrimitiveType.Sphere, "Earth",    new Vector3(0f, 2.2f, 0f), Quaternion.identity,          Vector3.one, earthMat);
        CreatePrimitive(PrimitiveType.Cube,   "Wheel",    new Vector3(-2.6f, 1.2f, -0.8f), Quaternion.Euler(0f, -20f, 0f), new Vector3(2.4f, 2.4f, 2.4f), wheelMat);
        CreatePrimitive(PrimitiveType.Cube,   "House",    new Vector3(2.6f, 1.5f, 0.6f), Quaternion.Euler(0f, 25f, 0f), new Vector3(2f, 3f, 2f), houseMat);
        CreatePrimitive(PrimitiveType.Plane,  "Water",    new Vector3(0f, 0.05f, 0f), Quaternion.identity,          new Vector3(2.2f, 1f, 2.2f), waterMat);
        CreatePrimitive(PrimitiveType.Sphere, "LampWarm", new Vector3(0f, 3.6f, 1.8f),  Quaternion.identity,          new Vector3(0.7f, 0.7f, 0.7f), lampWarm);
        CreatePrimitive(PrimitiveType.Sphere, "LampCool", new Vector3(-2.2f, 2.8f, 2.2f), Quaternion.identity,        new Vector3(0.5f, 0.5f, 0.5f), lampCool);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenesDir + "/Demo.unity");
    }

    private static Material LoadOrCreatePostMaterial(Shader postShader)
    {
        string path = MaterialsDir + "/PostProcessing.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(postShader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = postShader;
        material.SetTexture("_LUT0", LoadTexture("lookup-table-0.png"));
        material.SetTexture("_LUT1", LoadTexture("lookup-table-1.png"));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreatePrimitive(
        PrimitiveType type, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;
        if (material != null)
        {
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string leaf = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void FixTextureImportSettings()
    {
        foreach (string file in NormalMaps)
        {
            string path = TexturesDir + "/" + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }

    private static Shader LoadShader(string path, string shaderName)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null)
        {
            Debug.LogError("3DGameShaders: shader not found at " + path + " (expected " + shaderName + ")");
        }
        return shader;
    }

    private static Texture2D LoadTexture(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + fileName);
    }

    private static Material CreateBaseMaterial(string name, Shader shader, Texture2D diffuse, Texture2D normal, Texture2D specular)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        if (diffuse != null) material.SetTexture("_DiffuseMap", diffuse);
        if (normal != null) material.SetTexture("_NormalMap", normal);
        if (specular != null) material.SetTexture("_SpecularMap", specular);
        EditorUtility.SetDirty(material);
        return material;
    }
}