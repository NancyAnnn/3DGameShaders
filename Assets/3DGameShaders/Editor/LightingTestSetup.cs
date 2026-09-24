using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LightingTestSetup
{
    private const string SettingsDir = "Assets/3DGameShaders/Settings";

    [MenuItem("3DGameShaders/Add Lighting Test Lights")]
    public static void AddTestLights()
    {
        EnsureAdditionalLightSettings();

        Light point = EnsureLight("TestPointLight", LightType.Point);
        point.transform.position = new Vector3(-3.5f, 3.5f, 3.5f);
        point.color = new Color(1f, 0.85f, 0.6f);
        point.intensity = 3f;
        point.range = 14f;
        point.shadows = LightShadows.Soft;
        point.shadowStrength = 0.8f;

        Light spot = EnsureLight("TestSpotLight", LightType.Spot);
        spot.transform.position = new Vector3(6f, 9f, 7f);
        spot.transform.LookAt(new Vector3(0f, 3f, 0f));
        spot.color = new Color(0.7f, 0.85f, 1f);
        spot.intensity = 8f;
        spot.range = 30f;
        spot.spotAngle = 45f;
        spot.innerSpotAngle = 20f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.8f;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("3DGameShaders: test lights added (point + spot). Move them around and watch "
            + "distance attenuation, the spot cone, and their shadows.");
    }

    private static void EnsureAdditionalLightSettings()
    {
        string path = SettingsDir + "/3DGameShadersURP.asset";
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipeline == null)
        {
            Debug.LogWarning("3DGameShaders: URP asset not found at " + path);
            return;
        }

        SerializedObject serialized = new SerializedObject(pipeline);

        SerializedProperty mode = serialized.FindProperty("m_AdditionalLightsRenderingMode");
        if (mode != null && mode.intValue != 1)  // 1 = Per Pixel
        {
            mode.intValue = 1;
        }

        SerializedProperty shadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
        if (shadows != null && !shadows.boolValue)
        {
            shadows.boolValue = true;
        }

        SerializedProperty limit = serialized.FindProperty("m_AdditionalLightsPerObjectLimit");
        if (limit != null && limit.intValue < 8)
        {
            limit.intValue = 8;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
    }

    private static Light EnsureLight(string name, LightType type)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
        }

        Light light = go.GetComponent<Light>();
        if (light == null)
        {
            light = go.AddComponent<Light>();
        }
        light.type = type;

        if (go.GetComponent<UniversalAdditionalLightData>() == null)
        {
            go.AddComponent<UniversalAdditionalLightData>();
        }
        return light;
    }
}