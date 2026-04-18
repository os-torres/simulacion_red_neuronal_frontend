using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Renders the 3D interactive visualization of the neural network classifier.
/// Maps data-space coordinates to an 8x8x8 Unity cube centered at the origin.
/// </summary>
public class Visualizer3D : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------
    [SerializeField] private Transform root3D;          // Parent for all 3D objects
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Camera cam3D;

    // -----------------------------------------------------------------------
    // Hardcoded colour palette (8 colours)
    // -----------------------------------------------------------------------
    private static readonly Color[] Palette = new Color[]
    {
        new Color(0.906f, 0.298f, 0.235f), // #E74C3C
        new Color(0.204f, 0.596f, 0.859f), // #3498DB
        new Color(0.180f, 0.800f, 0.443f), // #2ECC71
        new Color(0.953f, 0.612f, 0.071f), // #F39C12
        new Color(0.608f, 0.349f, 0.714f), // #9B59B6
        new Color(0.102f, 0.737f, 0.612f), // #1ABC9C
        new Color(0.902f, 0.494f, 0.133f), // #E67E22
        new Color(0.204f, 0.286f, 0.369f), // #34495E
    };

    // -----------------------------------------------------------------------
    // Half-size of the Unity cube ([-4, 4] on every axis)
    // -----------------------------------------------------------------------
    private const float HalfSize = 4f;

    // Layer name for the 3D visualization objects.
    // Camera3D must have its Culling Mask set to include this layer.
    // The main camera should exclude it.
    private const string VizLayer = "Visualization";

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Loads and renders a full 3D visualization dataset.</summary>
    public void LoadData(VisualizationData3D data)
    {
        Clear();

        if (data == null) return;

        Bounds3D b = data.bounds;

        // Resolve the Visualization layer index once (graceful fallback to 0)
        int vizLayerIdx = LayerMask.NameToLayer(VizLayer);
        if (vizLayerIdx < 0)
        {
            Debug.LogWarning($"[Visualizer3D] Capa '{VizLayer}' no existe. " +
                             "Créala en Edit → Project Settings → Tags & Layers.");
            vizLayerIdx = 0;
        }

        // --- Group containers per class --------------------------------
        var classRoots = new Dictionary<int, Transform>();
        for (int c = 0; c < data.n_classes; c++)
        {
            var go = new GameObject($"Class_{c}");
            go.layer = vizLayerIdx;
            go.transform.SetParent(root3D, false);
            classRoots[c] = go.transform;
        }

        // --- Data points -----------------------------------------------
        if (data.points != null)
        {
            foreach (var pt in data.points)
            {
                Vector3 pos = NormalizePoint(pt.x, pt.y, pt.z, b);
                Color baseColor = GetColor(pt.class_id, data.colors);

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Point_{pt.class_id}";

                Transform parent = classRoots.ContainsKey(pt.class_id)
                    ? classRoots[pt.class_id]
                    : root3D;
                sphere.layer = vizLayerIdx;
                sphere.transform.SetParent(parent, false);
                sphere.transform.localPosition = pos;

                // Destroy the default collider (not needed for visualization)
                Destroy(sphere.GetComponent<Collider>());

                if (pt.correct)
                {
                    sphere.transform.localScale = Vector3.one * 0.12f;
                    ApplyOpaqueMaterial(sphere.GetComponent<Renderer>(), baseColor);
                }
                else
                {
                    // Misclassified: slightly larger, desaturated sphere + transparent halo
                    sphere.transform.localScale = Vector3.one * 0.18f;
                    Color desaturated = Desaturate(baseColor, 0.4f);
                    ApplyOpaqueMaterial(sphere.GetComponent<Renderer>(), desaturated);

                    // Semi-transparent halo
                    GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    halo.name = "Halo";
                    halo.layer = vizLayerIdx;
                    halo.transform.SetParent(sphere.transform, false);
                    halo.transform.localScale = Vector3.one * 1.6f;
                    halo.transform.localPosition = Vector3.zero;
                    Destroy(halo.GetComponent<Collider>());

                    Color haloColor = GetColor(pt.predicted_id, data.colors);
                    haloColor.a = 0.28f;
                    ApplyTransparentMaterial(halo.GetComponent<Renderer>(), haloColor);
                }
            }
        }

        // --- Decision boundary meshes ----------------------------------
        if (data.boundaries != null)
        {
            var boundaryRoot = new GameObject("Boundaries");
            boundaryRoot.layer = vizLayerIdx;
            boundaryRoot.transform.SetParent(root3D, false);

            foreach (var bm in data.boundaries)
            {
                Vector3[] rawVerts = bm.GetVertices();
                int[]     tris     = bm.GetTriangles();

                if (rawVerts == null || rawVerts.Length < 3 || tris == null || tris.Length < 3)
                    continue;

                // Normalise vertices from data-space to Unity-space
                Vector3[] normVerts = new Vector3[rawVerts.Length];
                for (int i = 0; i < rawVerts.Length; i++)
                    normVerts[i] = NormalizePoint(rawVerts[i].x, rawVerts[i].y, rawVerts[i].z, b);

                Mesh mesh = new Mesh();
                mesh.name = $"Boundary_{bm.class_id}";
                mesh.vertices  = normVerts;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                GameObject meshGO = new GameObject($"Boundary_{bm.class_id}");
                meshGO.layer = vizLayerIdx;
                meshGO.transform.SetParent(boundaryRoot.transform, false);

                MeshFilter   mf = meshGO.AddComponent<MeshFilter>();
                MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();
                mf.mesh = mesh;

                Color bmColor = ParseHexColor(bm.color);
                bmColor.a = 0.25f;
                ApplyTransparentMaterial(mr, bmColor);
            }
        }

        // --- Class centres (star / diamond markers) --------------------
        if (data.centers != null)
        {
            var centerRoot = new GameObject("Centers");
            centerRoot.layer = vizLayerIdx;
            centerRoot.transform.SetParent(root3D, false);

            foreach (var cc in data.centers)
            {
                Vector3 pos = NormalizePoint(cc.x, cc.y, cc.z, b);

                // Use a cube rotated 45° as a cheap diamond proxy
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Cube);
                star.name = $"Center_{cc.class_id}";
                star.layer = vizLayerIdx;
                star.transform.SetParent(centerRoot.transform, false);
                star.transform.localPosition = pos;
                star.transform.localScale    = Vector3.one * 0.3f;
                star.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                Destroy(star.GetComponent<Collider>());

                ApplyOpaqueMaterial(star.GetComponent<Renderer>(), new Color(1f, 0.84f, 0f)); // Gold
            }
        }

        // --- Reference axis box ----------------------------------------
        DrawAxisBox();
    }

    /// <summary>Destroys all children of root3D.</summary>
    public void Clear()
    {
        if (root3D == null) return;
        for (int i = root3D.childCount - 1; i >= 0; i--)
            Destroy(root3D.GetChild(i).gameObject);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Maps a data-space coordinate to the [-4, 4] Unity cube.
    /// </summary>
    private Vector3 NormalizePoint(float x, float y, float z, Bounds3D b)
    {
        float nx = Remap(x, b.x_min, b.x_max, -HalfSize, HalfSize);
        float ny = Remap(y, b.y_min, b.y_max, -HalfSize, HalfSize);
        float nz = Remap(z, b.z_min, b.z_max, -HalfSize, HalfSize);
        return new Vector3(nx, ny, nz);
    }

    private static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        if (Mathf.Approximately(fromMax, fromMin)) return (toMin + toMax) * 0.5f;
        return toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);
    }

    private Color GetColor(int classId, string[] apiColors)
    {
        // Prefer colours sent by the API
        if (apiColors != null && classId >= 0 && classId < apiColors.Length)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(apiColors[classId], out c))
                return c;
        }
        // Fall back to built-in palette
        return Palette[classId % Palette.Length];
    }

    private Color ParseHexColor(string hex)
    {
        Color c;
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c))
            return c;
        return Color.white;
    }

    private static Color Desaturate(Color color, float amount)
    {
        float grey = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        return new Color(
            Mathf.Lerp(color.r, grey, amount),
            Mathf.Lerp(color.g, grey, amount),
            Mathf.Lerp(color.b, grey, amount),
            color.a);
    }

    // -----------------------------------------------------------------------
    // Material helpers — tries URP first, then Standard
    // -----------------------------------------------------------------------

    private static void ApplyOpaqueMaterial(Renderer renderer, Color color)
    {
        Material mat = CreateOpaqueMaterial(color);
        renderer.material = mat;
    }

    private static Material CreateOpaqueMaterial(Color color)
    {
        // Try URP Lit
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp != null)
        {
            var mat = new Material(urp);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            return mat;
        }

        // Fallback: Built-in Standard
        var fallback = new Material(Shader.Find("Standard"));
        fallback.color = color;
        return fallback;
    }

    private static void ApplyTransparentMaterial(Renderer renderer, Color color)
    {
        Material mat = CreateTransparentMaterial(color);
        renderer.material = mat;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        // Try URP Lit with transparent surface
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp != null)
        {
            var mat = new Material(urp);
            // Surface Type = 1 (Transparent)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f); // Alpha blend
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite",   0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            return mat;
        }

        // Fallback: Built-in Standard transparent
        var fallbackStd = new Material(Shader.Find("Standard"));
        fallbackStd.SetFloat("_Mode", 3f); // Transparent
        fallbackStd.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fallbackStd.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fallbackStd.SetInt("_ZWrite", 0);
        fallbackStd.DisableKeyword("_ALPHATEST_ON");
        fallbackStd.EnableKeyword("_ALPHABLEND_ON");
        fallbackStd.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        fallbackStd.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        fallbackStd.color = color;
        return fallbackStd;
    }

    // -----------------------------------------------------------------------
    // Axis box
    // -----------------------------------------------------------------------

    private void DrawAxisBox()
    {
        int vizLayerIdx = LayerMask.NameToLayer(VizLayer);
        if (vizLayerIdx < 0) vizLayerIdx = 0;

        GameObject boxRoot = new GameObject("AxisBox");
        boxRoot.layer = vizLayerIdx;
        boxRoot.transform.SetParent(root3D, false);

        Color boxColor = new Color(0.667f, 0.667f, 0.667f); // #AAAAAA

        // 8 corners of the [-4,4]^3 cube
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-4,-4,-4), new Vector3( 4,-4,-4),
            new Vector3( 4, 4,-4), new Vector3(-4, 4,-4),
            new Vector3(-4,-4, 4), new Vector3( 4,-4, 4),
            new Vector3( 4, 4, 4), new Vector3(-4, 4, 4),
        };

        // 12 edges: pairs of corner indices
        int[,] edges = new int[,]
        {
            {0,1},{1,2},{2,3},{3,0}, // bottom face
            {4,5},{5,6},{6,7},{7,4}, // top face
            {0,4},{1,5},{2,6},{3,7}  // verticals
        };

        for (int e = 0; e < edges.GetLength(0); e++)
        {
            GameObject lineGO = new GameObject($"Edge_{e}");
            lineGO.layer = vizLayerIdx;
            lineGO.transform.SetParent(boxRoot.transform, false);

            LineRenderer lr = lineGO.AddComponent<LineRenderer>();
            lr.useWorldSpace   = false;
            lr.positionCount   = 2;
            lr.SetPosition(0, corners[edges[e, 0]]);
            lr.SetPosition(1, corners[edges[e, 1]]);
            lr.startWidth      = 0.03f;
            lr.endWidth        = 0.03f;
            lr.material        = CreateOpaqueMaterial(boxColor);
            lr.startColor      = boxColor;
            lr.endColor        = boxColor;
        }

        // --- Axis labels (TMP world-space) ---
        CreateAxisLabel(boxRoot.transform, "X\u2081", new Vector3(4.6f,  -4f,  -4f), Color.red);
        CreateAxisLabel(boxRoot.transform, "X\u2082", new Vector3(-4f,   4.6f, -4f), Color.green);
        CreateAxisLabel(boxRoot.transform, "X\u2083", new Vector3(-4f,   -4f,  4.6f), Color.cyan);

        // --- Tick marks on each axis (5 ticks) ---
        CreateAxisTicks(boxRoot.transform, boxColor);
    }

    private static void CreateAxisLabel(Transform parent, string text, Vector3 localPos, Color color)
    {
        GameObject go = new GameObject($"Label_{text}");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * 0.5f;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text      = text;
        tmp.color     = color;
        tmp.fontSize  = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 1f);
    }

    private static void CreateAxisTicks(Transform parent, Color color)
    {
        int   tickCount  = 5;
        float tickLength = 0.15f;

        for (int i = 0; i <= tickCount; i++)
        {
            float t   = (float)i / tickCount;               // 0..1
            float val = Mathf.Lerp(-HalfSize, HalfSize, t); // -4..4

            // X axis ticks (bottom-front edge, Y=-4, Z=-4)
            CreateTick(parent, color,
                new Vector3(val, -4f - tickLength, -4f),
                new Vector3(val, -4f + tickLength, -4f));

            // Y axis ticks (left-front edge, X=-4, Z=-4)
            CreateTick(parent, color,
                new Vector3(-4f - tickLength, val, -4f),
                new Vector3(-4f + tickLength, val, -4f));

            // Z axis ticks (left-bottom edge, X=-4, Y=-4)
            CreateTick(parent, color,
                new Vector3(-4f, -4f - tickLength, val),
                new Vector3(-4f, -4f + tickLength, val));
        }
    }

    private static void CreateTick(Transform parent, Color color, Vector3 from, Vector3 to)
    {
        GameObject go = new GameObject("Tick");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = 0.02f;
        lr.endWidth   = 0.02f;
        lr.material   = CreateOpaqueMaterial(color);
        lr.startColor = color;
        lr.endColor   = color;
    }
}
