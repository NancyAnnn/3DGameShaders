using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MillSceneSetup
{
    private const string RootDir = "Assets/3DGameShaders";
    private const string TexturesDir = RootDir + "/Textures";
    private const string ModelsDir = RootDir + "/Models/MillScene";

    private static readonly string[] ModelFiles =
    {
        "mill-scene.obj",
        "banner.obj",
        "shutters.obj",
        "weather-vane.obj"
    };

    [MenuItem("3DGameShaders/Add Mill Scene Model")]
    public static void AddMillScene()
    {
        SetNormalMapImports();
        AssetDatabase.Refresh();

        Shader baseShader = Shader.Find("3DGameShaders/BaseLit");
        if (baseShader == null)
        {
            Debug.LogError("3DGameShaders: BaseLit shader not found.");
            return;
        }

        DestroyObjects(new[]
        {
            "Ground", "Earth", "Wheel", "House", "Water",
            "MillScene", "Banner", "Shutters", "WeatherVane"
        });

        // The OBJ files are parsed directly (Unity's built-in OBJ importer drops
        // object/material names, so we never use the imported model assets).
        GameObject millRoot = new GameObject("MillScene");

        int objectCount = 0;
        foreach (string file in ModelFiles)
        {
            string objPath = Path.Combine(Application.dataPath, ModelsDir.Replace("Assets/", ""), file);
            if (!File.Exists(objPath))
            {
                Debug.LogWarning("3DGameShaders: missing " + objPath);
                continue;
            }
            objectCount += BuildObj(millRoot.transform, objPath, baseShader);
        }

        // Verification: print every part's world bounds.
        foreach (Renderer renderer in millRoot.GetComponentsInChildren<Renderer>())
        {
            Debug.Log("[MillScene] " + renderer.name
                + " center=" + renderer.bounds.center.ToString("F2")
                + " size=" + renderer.bounds.size.ToString("F2"));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("3DGameShaders: mill scene added with " + objectCount
            + " objects. See [MillScene] bounds lines above for verification.");
    }

    private static int BuildObj(Transform parent, string objPath, Shader baseShader)
    {
        string[] lines = File.ReadAllLines(objPath);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        string sectionName = "";
        int objectCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line[0] == 'o')
            {
                FlushObject(parent, sectionName, verts, uvs, normals, tris, baseShader, ref objectCount);
                verts.Clear();
                uvs.Clear();
                normals.Clear();
                tris.Clear();
                sectionName = line.Length > 2 ? line.Substring(2).Trim() : "";
                continue;
            }

            if (line[0] == 'v' && (line.Length == 1 || line[1] == ' '))
            {
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4)
                {
                    continue;
                }
                float x = float.Parse(p[1], CultureInfo.InvariantCulture);
                float y = float.Parse(p[2], CultureInfo.InvariantCulture);
                float z = float.Parse(p[3], CultureInfo.InvariantCulture);
                // Panda3D z-up -> Unity y-up (proper rotation, winding preserved).
                verts.Add(new Vector3(x, z, -y));
                continue;
            }

            if (line.StartsWith("vt "))
            {
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3)
                {
                    uvs.Add(new Vector2(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture)));
                }
                continue;
            }

            if (line.StartsWith("vn "))
            {
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4)
                {
                    normals.Add(new Vector3(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[3], CultureInfo.InvariantCulture),
                        -float.Parse(p[2], CultureInfo.InvariantCulture)).normalized);
                }
                continue;
            }

            if (line[0] == 'f')
            {
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4)
                {
                    continue;
                }
                int a = ParseIndex(p[1]);
                for (int k = 1; k + 2 <= p.Length - 1; k++)
                {
                    int b = ParseIndex(p[k + 1]);
                    int c = ParseIndex(p[k + 2]);
                    // Panda3D windings are clockwise; Unity expects counter-clockwise.
                    tris.Add(a);
                    tris.Add(c);
                    tris.Add(b);
                }
            }
        }

        FlushObject(parent, sectionName, verts, uvs, normals, tris, baseShader, ref objectCount);
        return objectCount;
    }

    private static int ParseIndex(string token)
    {
        int slash = token.IndexOf('/');
        string num = slash >= 0 ? token.Substring(0, slash) : token;
        return int.Parse(num, CultureInfo.InvariantCulture) - 1;
    }

    private static void FlushObject(
        Transform parent, string sectionName,
        List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris,
        Shader baseShader, ref int objectCount)
    {
        if (verts.Count == 0 || tris.Count == 0)
        {
            return;
        }

        GameObject go = new GameObject(sectionName.Length > 0 ? sectionName : ("obj" + objectCount));
        go.transform.SetParent(parent, false);

        Mesh mesh = new Mesh();
        mesh.name = sectionName;
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        if (uvs.Count == verts.Count)
        {
            mesh.SetUVs(0, uvs);
        }
        if (normals.Count == verts.Count)
        {
            mesh.SetNormals(normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();

        Material material = null;
        if (sectionName.EndsWith("-diffuse"))
        {
            string baseName = sectionName.Substring(0, sectionName.Length - "-diffuse".Length);
            material = LoadOrCreateMillMaterial(baseShader, baseName);
        }
        if (material == null)
        {
            material = LoadOrCreateMillMaterial(baseShader, sectionName.Length > 0 ? sectionName : "default");
        }
        renderer.sharedMaterial = material;

        objectCount++;
    }

    private static Material LoadOrCreateMillMaterial(Shader baseShader, string baseName)
    {
        string path = RootDir + "/Materials/Mill_" + baseName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(baseShader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = baseShader;
        material.SetTexture("_DiffuseMap", LoadTexture(baseName + "-diffuse.png"));

        Texture2D normalMap = LoadTexture(baseName + "-normal.png");
        if (normalMap == null)
        {
            normalMap = LoadTexture("normal.png");
        }
        material.SetTexture("_NormalMap", normalMap);

        Texture2D specularMap = LoadTexture(baseName + "-specular.png");
        if (specularMap == null)
        {
            specularMap = LoadTexture("no-specular.png");
        }
        material.SetTexture("_SpecularMap", specularMap);

        // Safety: render both sides so winding issues cannot hide geometry.
        material.SetFloat("_Cull", 0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D LoadTexture(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/" + fileName);
    }

    private static void SetNormalMapImports()
    {
        string[] files = Directory.GetFiles(TexturesDir, "*-normal.png");
        foreach (string file in files)
        {
            string assetPath = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }

    private static void DestroyObjects(string[] names)
    {
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}