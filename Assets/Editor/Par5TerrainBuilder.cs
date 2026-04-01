using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public class Par5TerrainBuilder : EditorWindow
{
    const string BASE_PATH = "Assets/GolfTerrain";
    const float  TW = 500f;   // terrain width  (X)
    const float  TH = 20f;    // terrain height (Y)
    const float  TL = 500f;   // terrain length (Z)

    // Fairway centreline: tee (bottom-left) curves NE to green (upper-right)
    // nx=0 is left, nz=0 is bottom
    static readonly Vector2[] FAIRWAY_PATH =
    {
        new Vector2(0.21f, 0.08f),  // tee
        new Vector2(0.28f, 0.18f),
        new Vector2(0.35f, 0.28f),
        new Vector2(0.40f, 0.38f),
        new Vector2(0.44f, 0.48f),
        new Vector2(0.52f, 0.62f),
        new Vector2(0.60f, 0.74f),
        new Vector2(0.66f, 0.83f),
        new Vector2(0.70f, 0.88f),  // green
    };

    [MenuItem("Golf/Build Par 5 Terrain")]
    public static void BuildTerrain()
    {
        // ── 0. Clean up old objects & assets ─────────────────────────────
        foreach (string n in new[]{ "Par5Terrain","Water_Hazard","TeeGroup",
                                     "FlagPole","Hole","Sun","TreeGroup" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }

        if (AssetDatabase.IsValidFolder(BASE_PATH))
            AssetDatabase.DeleteAsset(BASE_PATH);
        AssetDatabase.CreateFolder("Assets", "GolfTerrain");
        AssetDatabase.Refresh();

        // ── 1. Textures ───────────────────────────────────────────────────
        // Rough (dark green)
        Texture2D roughTex = SaveSolidTex(new Color(0.10f, 0.24f, 0.05f), "Tex_Rough");
        // Fairway — alternating stripes to simulate mowing pattern
        Texture2D fairwayTex = SaveStripedTex(
            new Color(0.22f, 0.52f, 0.12f),
            new Color(0.28f, 0.60f, 0.16f), "Tex_Fairway");
        // Green surface (Prizm — bright tight cut)
        Texture2D greenTex = SaveSolidTex(new Color(0.40f, 0.76f, 0.22f), "Tex_Green");
        // Sand / bunker
        Texture2D sandTex = SaveSolidTex(new Color(0.88f, 0.80f, 0.50f), "Tex_Sand");
        // Tee (slightly brighter green, short cut like green)
        Texture2D teeTex = SaveSolidTex(new Color(0.34f, 0.68f, 0.18f), "Tex_Tee");
        // Water — procedural ripple pattern
        Texture2D waterTex = SaveWaterRippleTex();

        // ── 2. Terrain layers ─────────────────────────────────────────────
        TerrainLayer roughLayer   = SaveLayer(roughTex,   new Vector2(8,  8),  "Layer_Rough");
        TerrainLayer fairwayLayer = SaveLayer(fairwayTex, new Vector2(14, 14), "Layer_Fairway");
        TerrainLayer greenLayer   = SaveLayer(greenTex,   new Vector2(5,  5),  "Layer_Green");
        TerrainLayer sandLayer    = SaveLayer(sandTex,    new Vector2(4,  4),  "Layer_Sand");
        TerrainLayer teeLayer     = SaveLayer(teeTex,     new Vector2(6,  6),  "Layer_Tee");

        // ── 3. Heightmap ──────────────────────────────────────────────────
        TerrainData td = new TerrainData();
        td.heightmapResolution = 513;
        td.size = new Vector3(TW, TH, TL);

        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx  = x / (float)(res - 1);
                float nz  = z / (float)(res - 1);
                float fd  = DistToFairway(nx, nz);

                // Base with gentle Perlin rolling
                float h = 0.04f + Mathf.PerlinNoise(nx * 3f, nz * 3f) * 0.06f;

                // Fairway: slightly flatter than surroundings
                h -= Mathf.Clamp01(1f - fd / 0.08f) * 0.02f;

                // Green: crowned dome
                float gd = DistGreen(nx, nz);
                if (gd < GreenR())
                    h += 0.05f * (1f - gd / GreenR());

                // Tee: slight raised platform
                if (DistTee(nx, nz) < TeeR())
                    h += 0.018f;

                // Water basin: terrain sinks so water plane sits naturally
                h -= WaterDepth(nx, nz) * 0.30f;

                heights[z, x] = Mathf.Clamp01(h);
            }
        }
        td.SetHeights(0, 0, heights);

        // ── 4. Splatmap  (layer order: rough, fairway, green, sand, tee) ──
        td.terrainLayers = new TerrainLayer[]
            { roughLayer, fairwayLayer, greenLayer, sandLayer, teeLayer };

        int aw = td.alphamapWidth, ah = td.alphamapHeight;
        float[,,] alpha = new float[aw, ah, 5];

        for (int az = 0; az < ah; az++)
        {
            for (int ax = 0; ax < aw; ax++)
            {
                float nx = ax / (float)(aw - 1);
                float nz = az / (float)(ah - 1);
                float fd = DistToFairway(nx, nz);

                float rw = 1f, fw = 0f, gw = 0f, sw = 0f, tw = 0f;

                // Fairway blend (smooth edge)
                fw  = Mathf.Clamp01(1f - fd / 0.09f);
                rw  = 1f - fw;

                // Green surface (Prizm)
                float gd = DistGreen(nx, nz);
                if (gd < GreenR())
                { gw = Mathf.Clamp01(1f - gd / GreenR()); fw = 0f; rw = 0f; }

                // Sand bunkers (yellow)
                if (InBunker1(nx, nz) || InBunker2(nx, nz))
                { sw = 0.95f; fw = 0f; rw = 0f; gw = 0f; }

                // Tee surface
                float td2 = DistTee(nx, nz);
                if (td2 < TeeR())
                { tw = Mathf.Clamp01(1f - td2 / TeeR()); fw = 0f; rw = 0f; }

                // Water zone: rough only (plane sits here)
                if (InWater(nx, nz))
                { rw = 1f; fw = 0f; gw = 0f; sw = 0f; tw = 0f; }

                float total = rw + fw + gw + sw + tw;
                if (total < 0.001f) total = 1f;
                alpha[az, ax, 0] = rw / total;
                alpha[az, ax, 1] = fw / total;
                alpha[az, ax, 2] = gw / total;
                alpha[az, ax, 3] = sw / total;
                alpha[az, ax, 4] = tw / total;
            }
        }
        td.SetAlphamaps(0, 0, alpha);

        // ── 5. Save terrain data & create GameObject ──────────────────────
        AssetDatabase.CreateAsset(td, BASE_PATH + "/Par5TerrainData.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject terrainGO = Terrain.CreateTerrainGameObject(td);
        terrainGO.name = "Par5Terrain";
        terrainGO.transform.position = Vector3.zero;
        terrainGO.GetComponent<Terrain>().drawInstanced = true;

        // ── 6. Materials ──────────────────────────────────────────────────
        Material whiteMat  = SaveMat(Color.white,                    "Mat_White");
        Material redMat    = SaveMat(Color.red,                      "Mat_Red");
        Material brownMat  = SaveMat(new Color(0.38f, 0.22f, 0.08f), "Mat_Brown");
        Material leafMat   = SaveMat(new Color(0.08f, 0.22f, 0.05f), "Mat_Leaf");
        Material teeBlobMat= SaveMat(new Color(0.30f, 0.65f, 0.14f), "Mat_TeeBlob");
        Material waterMat  = SaveWaterMat(waterTex);

        // ── 7. Flowing water hazard ───────────────────────────────────────
        // Main body: world (280, -0.3, 210) — right of fairway mid-hole
        // WaterFlow script drives UV scroll + shimmer every frame
        var water = MakePlane("Water_Hazard", new Vector3(280f,-0.3f,210f),
                              new Vector3(9f,1f,8f), waterMat);
        water.AddComponent<WaterFlow>();

        // Left extension lobe
        var waterLobe = MakePlane("Water_Lobe", new Vector3(230f,-0.3f,225f),
                                  new Vector3(5f,1f,4f), waterMat);
        waterLobe.AddComponent<WaterFlow>();
        waterLobe.transform.SetParent(water.transform, true);

        // ── 8. Tee boxes — 4 oval markers in a row ────────────────────────
        float teeH   = td.GetInterpolatedHeight(0.21f, 0.08f);
        Vector3 teeC = new Vector3(0.21f * TW, teeH + 0.2f, 0.08f * TL);
        GameObject teeGroup = new GameObject("TeeGroup");

        for (int i = 0; i < 4; i++)
        {
            float ox = (i - 1.5f) * 2.8f;
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blob.name = "Tee_" + i;
            blob.transform.position   = teeC + new Vector3(ox, 0f, 0f);
            blob.transform.localScale = new Vector3(1.4f, 0.08f, 2.2f);
            blob.GetComponent<Renderer>().sharedMaterial = teeBlobMat;
            blob.transform.SetParent(teeGroup.transform, true);
        }

        // Empty spawn point for player/ball
        GameObject teePos = new GameObject("TeePosition");
        teePos.transform.position = teeC + new Vector3(0f, 1f, 5f);
        teePos.tag = "Respawn";
        teePos.transform.SetParent(teeGroup.transform, true);

        // ── 9. Flag, hole, green ──────────────────────────────────────────
        float greenH = td.GetInterpolatedHeight(0.70f, 0.88f);
        float gx = 0.70f * TW, gz = 0.88f * TL;

        GameObject flagPole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flagPole.name = "FlagPole";
        flagPole.transform.position   = new Vector3(gx, greenH + 1.5f, gz);
        flagPole.transform.localScale = new Vector3(0.05f, 1.5f, 0.05f);
        flagPole.GetComponent<Renderer>().sharedMaterial = whiteMat;

        GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Flag";
        flag.transform.position   = new Vector3(gx + 0.25f, greenH + 4.4f, gz);
        flag.transform.localScale = new Vector3(0.55f, 0.30f, 0.05f);
        flag.GetComponent<Renderer>().sharedMaterial = redMat;
        flag.transform.SetParent(flagPole.transform, true);

        GameObject hole = new GameObject("Hole");
        hole.transform.position = new Vector3(gx, greenH + 0.1f, gz);
        var hc = hole.AddComponent<CapsuleCollider>();
        hc.radius = 0.6f; hc.height = 0.5f; hc.isTrigger = true;

        // ── 10. Trees (matching image rough distribution) ──────────────────
        // Each Vector2 = (nx, nz) normalised terrain position
        Vector2[] treePts =
        {
            // Left rough cluster mid-hole (prominent in image)
            new Vector2(0.10f,0.35f), new Vector2(0.14f,0.40f),
            new Vector2(0.12f,0.48f), new Vector2(0.08f,0.44f),
            new Vector2(0.16f,0.55f), new Vector2(0.10f,0.60f),
            // Right border
            new Vector2(0.85f,0.20f), new Vector2(0.88f,0.35f),
            new Vector2(0.86f,0.48f), new Vector2(0.84f,0.60f),
            new Vector2(0.82f,0.72f), new Vector2(0.80f,0.82f),
            // Bottom border
            new Vector2(0.35f,0.04f), new Vector2(0.45f,0.03f),
            new Vector2(0.55f,0.04f), new Vector2(0.14f,0.20f),
            // Upper / green surround
            new Vector2(0.55f,0.94f), new Vector2(0.64f,0.96f),
            new Vector2(0.80f,0.95f), new Vector2(0.88f,0.88f),
            // Scattered inner rough
            new Vector2(0.20f,0.25f), new Vector2(0.18f,0.68f),
            new Vector2(0.24f,0.78f), new Vector2(0.30f,0.88f),
            new Vector2(0.75f,0.45f), new Vector2(0.78f,0.58f),
        };

        GameObject treeGroup = new GameObject("TreeGroup");
        foreach (var pt in treePts)
        {
            float th2 = td.GetInterpolatedHeight(pt.x, pt.y);
            PlaceTree(new Vector3(pt.x * TW, th2, pt.y * TL),
                      brownMat, leafMat, treeGroup.transform);
        }

        // ── 11. Directional sunlight ──────────────────────────────────────
        if (Object.FindFirstObjectByType<Light>() == null)
        {
            GameObject sun = new GameObject("Sun");
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.color = new Color(1f, 0.95f, 0.82f);
            sun.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Hole Built!",
            "Par 5 layout created:\n\n" +
            "- Striped fairway (NE dogleg)\n" +
            "- Flowing animated water hazard\n" +
            "- 2 sand bunkers near green\n" +
            "- Crowned green (Prizm texture)\n" +
            "- 4 tee box ovals\n" +
            "- 26 trees in rough\n\n" +
            "Player spawn: TeeGroup > TeePosition\n" +
            "Save the scene (Cmd+S).",
            "OK");
    }

    // ── Zone definitions ──────────────────────────────────────────────────

    static float DistToFairway(float nx, float nz)
    {
        float min = float.MaxValue;
        var p = new Vector2(nx, nz);
        for (int i = 0; i < FAIRWAY_PATH.Length - 1; i++)
        {
            float d = SegDist(p, FAIRWAY_PATH[i], FAIRWAY_PATH[i + 1]);
            if (d < min) min = d;
        }
        return min;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a, ap = p - a;
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
        return (p - (a + t * ab)).magnitude;
    }

    // Ellipse helper: returns 0..inf, <1 = inside
    static float EllD(float nx, float nz, float cx, float cz, float rx, float rz)
    {
        float dx = (nx - cx) / rx, dz = (nz - cz) / rz;
        return dx * dx + dz * dz;
    }

    // Water: main blob + two extension lobes
    static bool InWater(float nx, float nz)
        => EllD(nx,nz,0.56f,0.42f,0.09f,0.07f) < 1f ||
           EllD(nx,nz,0.49f,0.45f,0.06f,0.05f) < 1f ||
           EllD(nx,nz,0.62f,0.40f,0.05f,0.04f) < 1f;

    static float WaterDepth(float nx, float nz)
    {
        float d1 = Mathf.Clamp01(1f - EllD(nx,nz,0.56f,0.42f,0.09f,0.07f) / 0.15f);
        float d2 = Mathf.Clamp01(1f - EllD(nx,nz,0.49f,0.45f,0.06f,0.05f) / 0.10f);
        float d3 = Mathf.Clamp01(1f - EllD(nx,nz,0.62f,0.40f,0.05f,0.04f) / 0.08f);
        return Mathf.Max(d1, Mathf.Max(d2, d3));
    }

    // Bunker 1: left side between water and green (kidney shape)
    static bool InBunker1(float nx, float nz)
        => EllD(nx,nz,0.61f,0.63f,0.055f,0.04f) < 1f ||
           EllD(nx,nz,0.57f,0.67f,0.040f,0.03f) < 1f;

    // Bunker 2: right/upper near green (two-lobe)
    static bool InBunker2(float nx, float nz)
        => EllD(nx,nz,0.75f,0.83f,0.050f,0.04f) < 1f ||
           EllD(nx,nz,0.79f,0.79f,0.030f,0.03f) < 1f;

    static float GreenR() => 0.075f;
    static float DistGreen(float nx, float nz)
        => Vector2.Distance(new Vector2(nx, nz), new Vector2(0.70f, 0.88f));
    static float TeeR() => 0.055f;
    static float DistTee(float nx, float nz)
        => Vector2.Distance(new Vector2(nx, nz), new Vector2(0.21f, 0.09f));

    // ── Scene helpers ──────────────────────────────────────────────────────

    static GameObject MakePlane(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        return go;
    }

    static void PlaceTree(Vector3 pos, Material trunkMat, Material leafMat, Transform parent)
    {
        var tree = new GameObject("Tree");
        tree.transform.position = pos;
        tree.transform.SetParent(parent, true);

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(tree.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        trunk.transform.localScale    = new Vector3(0.35f, 1.5f, 0.35f);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
        Object.DestroyImmediate(trunk.GetComponent<CapsuleCollider>());

        var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy.transform.SetParent(tree.transform, false);
        canopy.transform.localPosition = new Vector3(0f, 4.5f, 0f);
        canopy.transform.localScale    = new Vector3(2.8f, 2.5f, 2.8f);
        canopy.GetComponent<Renderer>().sharedMaterial = leafMat;
        Object.DestroyImmediate(canopy.GetComponent<SphereCollider>());
    }

    // ── Asset helpers ──────────────────────────────────────────────────────

    static Texture2D SaveSolidTex(Color color, string name)
    {
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var px  = new Color[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = color;
        tex.SetPixels(px); tex.Apply();
        return WriteTex(tex, name);
    }

    static Texture2D SaveStripedTex(Color dark, Color light, string name)
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px  = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = ((x / 14) % 2 == 0) ? dark : light;
        tex.SetPixels(px); tex.Apply();
        return WriteTex(tex, name);
    }

    static Texture2D SaveWaterRippleTex()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px  = new Color[size * size];
        Color deep  = new Color(0.02f, 0.14f, 0.52f);
        Color light = new Color(0.18f, 0.42f, 0.82f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size, ny = y / (float)size;
                float w = (Mathf.Sin((nx + ny) * 22f) * 0.4f
                         + Mathf.Sin((nx - ny) * 16f) * 0.3f
                         + Mathf.Sin(nx * 28f)        * 0.3f)
                         * 0.5f + 0.5f;
                px[y * size + x] = Color.Lerp(deep, light, w * 0.65f);
            }
        }
        tex.SetPixels(px); tex.Apply();
        return WriteTex(tex, "Tex_Water");
    }

    static Texture2D WriteTex(Texture2D tex, string name)
    {
        string dir  = Application.dataPath + "/GolfTerrain/";
        string file = dir + name + ".png";
        string asset= BASE_PATH + "/" + name + ".png";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(file, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(asset);
        var imp = AssetImporter.GetAtPath(asset) as TextureImporter;
        if (imp != null) { imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
    }

    static TerrainLayer SaveLayer(Texture2D tex, Vector2 tileSize, string name)
    {
        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = tileSize };
        string path = BASE_PATH + "/" + name + ".terrainlayer";
        AssetDatabase.CreateAsset(layer, path);
        return AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
    }

    static Material SaveMat(Color color, string name)
    {
        var mat = new Material(Shader.Find("Standard")) { color = color };
        string path = BASE_PATH + "/" + name + ".mat";
        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material SaveWaterMat(Texture2D waterTex)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.mainTexture = waterTex;
        mat.color = new Color(0.04f, 0.22f, 0.68f, 0.88f);
        mat.SetColor("_EmissionColor", new Color(0f, 0.04f, 0.20f));
        mat.EnableKeyword("_EMISSION");
        string path = BASE_PATH + "/Mat_Water.mat";
        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
}
