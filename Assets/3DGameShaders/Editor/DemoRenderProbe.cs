using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Renders the scene twice - once with the water mesh and once without - and
// writes a text report with the resulting pixel statistics plus the current
// effect parameters.
//
// Run it from the menu while in play mode. The report lands next to the project
// as Assets/3DGameShaders/render-probe-report.txt so it can be read and diffed
// without anyone needing to look at a screenshot.
public static class DemoRenderProbe
{
    private const int Width = 960;
    private const int Height = 540;
    private const string ReportPath = "Assets/3DGameShaders/render-probe-report.txt";
    private const string WaterObjectName = "water-diffuse";

    [MenuItem("3DGameShaders/Render Probe (writes report)")]
    public static void Capture()
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            Debug.LogError("3DGameShaders: no camera in the scene, cannot probe.");
            return;
        }

        Renderer water = null;
        GameObject waterObject = GameObject.Find(WaterObjectName);
        if (waterObject != null)
        {
            water = waterObject.GetComponent<Renderer>();
        }

        Texture2D withWater = RenderCamera(camera);
        if (withWater == null)
        {
            return;
        }

        Texture2D withoutWater = null;
        if (water != null)
        {
            bool wasEnabled = water.enabled;
            water.enabled = false;
            withoutWater = RenderCamera(camera);
            water.enabled = wasEnabled;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("3D Game Shaders - render probe report");
        report.AppendLine("time        : " + System.DateTime.Now.ToString("u"));
        report.AppendLine("play mode   : " + Application.isPlaying);
        report.AppendLine("resolution  : " + Width + "x" + Height);
        report.AppendLine("camera      : " + camera.name
            + " position=" + camera.transform.position.ToString("F2")
            + " rotation=" + camera.transform.eulerAngles.ToString("F0"));
        report.AppendLine("scene       : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        report.AppendLine();

        AppendFrameStats(report, withWater);

        if (withoutWater != null)
        {
            AppendWaterStats(report, withWater, withoutWater);
            AppendDebugViews(report, camera, water, withWater, withoutWater);
        }
        else
        {
            report.AppendLine("water mesh '" + WaterObjectName + "' not found - no water stats.");
        }

        AppendParameters(report, water);

        string absolute = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
        File.WriteAllText(absolute, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);

        Debug.Log("3DGameShaders: render probe written to " + ReportPath + "\n" + report);

        Object.DestroyImmediate(withWater);
        if (withoutWater != null)
        {
            Object.DestroyImmediate(withoutWater);
        }
    }

    // Renders the water shader's debug views so the report says whether foam,
    // depth, reflection and thickness are actually being computed.
    private static void AppendDebugViews(
        StringBuilder report, Camera camera, Renderer water,
        Texture2D withWater, Texture2D withoutWater)
    {
        // The renderer's own material instance: writing to sharedMaterial while
        // playing would modify the project asset.
        Material material = water.material;
        if (material == null || !material.HasProperty("_DebugView"))
        {
            return;
        }

        Color[] baseWith = withWater.GetPixels();
        Color[] baseWithout = withoutWater.GetPixels();
        int count = Mathf.Min(baseWith.Length, baseWithout.Length);
        bool[] mask = new bool[count];
        for (int i = 0; i < count; i++)
        {
            float diff = Mathf.Abs(baseWith[i].r - baseWithout[i].r)
                       + Mathf.Abs(baseWith[i].g - baseWithout[i].g)
                       + Mathf.Abs(baseWith[i].b - baseWithout[i].b);
            mask[i] = diff > 0.02f;
        }

        report.AppendLine("-- water debug views (water pixels only) --");
        for (int view = 1; view <= 4; view++)
        {
            material.SetFloat("_DebugView", view);
            Texture2D debug = RenderCamera(camera);
            if (debug == null)
            {
                continue;
            }

            Color[] pixels = debug.GetPixels();
            double sum = 0;
            float max = 0f;
            int used = 0;
            for (int i = 0; i < pixels.Length && i < mask.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }
                float value = pixels[i].r;
                if (value > max)
                {
                    max = value;
                }
                sum += value;
                used++;
            }

            report.AppendLine(ViewName(view) + ": mean=" + Fmt(used > 0 ? sum / used : 0.0)
                + " max=" + Fmt(max) + " over " + used + " px");
            Object.DestroyImmediate(debug);
        }
        material.SetFloat("_DebugView", 0f);
        report.AppendLine();
    }

    private static string ViewName(int view)
    {
        switch (view)
        {
            case 1: return "foam amount ";
            case 2: return "depth factor";
            case 3: return "reflection  ";
            case 4: return "thickness/4 ";
            default: return "view " + view + "    ";
        }
    }

    private static Texture2D RenderCamera(Camera camera)
    {
        RenderTexture target = RenderTexture.GetTemporary(
            Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = camera.targetTexture;

        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = previous;

        RenderTexture.active = target;
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        texture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(target);

        return texture;
    }

    private static void AppendFrameStats(StringBuilder report, Texture2D frame)
    {
        Color[] pixels = frame.GetPixels();
        double r = 0, g = 0, b = 0;
        foreach (Color c in pixels)
        {
            r += c.r;
            g += c.g;
            b += c.b;
        }

        double n = pixels.Length;
        report.AppendLine("-- whole frame --");
        report.AppendLine("mean rgb    : " + Fmt(r / n) + ", " + Fmt(g / n) + ", " + Fmt(b / n));
        report.AppendLine("centre pixel: " + Px(frame, Width / 2, Height / 2));
        report.AppendLine("lower centre: " + Px(frame, Width / 2, Height / 4));
        report.AppendLine("upper centre: " + Px(frame, Width / 2, Height * 3 / 4));
        report.AppendLine();
    }

    private static void AppendWaterStats(StringBuilder report, Texture2D withWater, Texture2D withoutWater)
    {
        Color[] a = withWater.GetPixels();
        Color[] b = withoutWater.GetPixels();
        if (a.Length != b.Length)
        {
            return;
        }

        double waterR = 0, waterG = 0, waterB = 0;
        double bedR = 0, bedG = 0, bedB = 0;
        double delta = 0;
        int count = 0;
        int foamPixels = 0;

        for (int i = 0; i < a.Length; i++)
        {
            // A pixel belongs to the water when removing the water mesh changed it.
            float diff = Mathf.Abs(a[i].r - b[i].r)
                       + Mathf.Abs(a[i].g - b[i].g)
                       + Mathf.Abs(a[i].b - b[i].b);
            if (diff <= 0.02f)
            {
                continue;
            }

            count++;
            waterR += a[i].r; waterG += a[i].g; waterB += a[i].b;
            bedR += b[i].r;   bedG += b[i].g;   bedB += b[i].b;
            delta += diff;

            float lumWater = Luma(a[i]);
            float lumBed = Luma(b[i]);
            if (lumWater - lumBed > 0.2f)
            {
                foamPixels++;
            }
        }

        report.AppendLine("-- water --");
        report.AppendLine("coverage    : " + Fmt(count / (double)a.Length * 100.0) + " % of the frame");
        if (count == 0)
        {
            report.AppendLine("the water mesh did not change any pixel - is it assigned the "
                + "WaterSurface material and inside the camera frustum?");
            report.AppendLine();
            return;
        }

        report.AppendLine("water mean  : " + Fmt(waterR / count) + ", " + Fmt(waterG / count)
            + ", " + Fmt(waterB / count) + "   (what the water looks like)");
        report.AppendLine("bed mean    : " + Fmt(bedR / count) + ", " + Fmt(bedG / count)
            + ", " + Fmt(bedB / count) + "   (same pixels without the water)");
        report.AppendLine("water/bed   : R " + Fmt(waterR / Mathf.Max((float)bedR, 1e-4f))
            + ", G " + Fmt(waterG / Mathf.Max((float)bedG, 1e-4f))
            + ", B " + Fmt(waterB / Mathf.Max((float)bedB, 1e-4f)));
        report.AppendLine("mean delta  : " + Fmt(delta / count) + "  (0 = water invisible, "
            + "3 = water fully replaces the bed)");
        report.AppendLine("foam pixels : " + Fmt(foamPixels / (double)count * 100.0)
            + " % brighter than the bed");
        report.AppendLine();
    }

    private static void AppendParameters(StringBuilder report, Renderer water)
    {
        report.AppendLine("-- parameters --");

        if (water != null && water.sharedMaterial != null)
        {
            Material m = water.sharedMaterial;
            report.AppendLine("water material: " + m.name + "  shader=" + (m.shader != null ? m.shader.name : "null"));
            AppendFloat(report, m, "_FlowSpeed");
            AppendFloat(report, m, "_WaterDepth");
            AppendFloat(report, m, "_WaterBodyStrength");
            AppendFloat(report, m, "_TintStrength");
            AppendFloat(report, m, "_RefractionStrength");
            AppendFloat(report, m, "_UseSSR");
            AppendFloat(report, m, "_EnvironmentStrength");
            AppendFloat(report, m, "_FoamDepth");
            AppendFloat(report, m, "_FoamThreshold");
            AppendFloat(report, m, "_FoamIntensity");
        }
        else
        {
            report.AppendLine("water material: not found");
        }

        PostProcessingFeature feature = PostProcessingFeature.Instance;
        if (feature != null)
        {
            PostProcessingFeature.Settings s = feature.settings;
            report.AppendLine("post: bloom=" + s.bloomEnabled + " ssao=" + s.ssaoEnabled
                + " outline=" + s.outlineEnabled + " fog=" + s.fogEnabled
                + " dof=" + s.dofEnabled + " motionBlur=" + s.motionBlurEnabled
                + " kuwahara=" + s.kuwaharaEnabled + " pixelize=" + s.pixelizeEnabled
                + " posterize=" + s.posterizeEnabled + " sharpen=" + s.sharpenEnabled
                + " filmGrain=" + s.filmGrainEnabled + " lut=" + s.lutEnabled
                + " ca=" + s.chromaticAberrationEnabled);
        }
        else
        {
            report.AppendLine("post: PostProcessingFeature.Instance is null "
                + "(enter play mode, or let the camera render once first)");
        }
    }

    private static void AppendFloat(StringBuilder report, Material material, string property)
    {
        if (material.HasProperty(property))
        {
            report.AppendLine(property + " = " + material.GetFloat(property).ToString("0.####"));
        }
    }

    private static string Px(Texture2D texture, int x, int y)
    {
        Color c = texture.GetPixel(Mathf.Clamp(x, 0, Width - 1), Mathf.Clamp(y, 0, Height - 1));
        return Fmt(c.r) + ", " + Fmt(c.g) + ", " + Fmt(c.b);
    }

    private static float Luma(Color c)
    {
        return 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
    }

    private static string Fmt(double value)
    {
        return value.ToString("0.###");
    }
}
