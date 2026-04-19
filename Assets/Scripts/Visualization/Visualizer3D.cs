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
    // Orden: Clase 1 = verde, Clase 2 = azul, Clase 3 = rojo, Clase 4 = naranja, …
    // El class_id del backend empieza en 0 (Clase 1 = id 0), por eso el orden
    // de la paleta está desplazado respecto a la numeración visible en la UI.
    // -----------------------------------------------------------------------
    private static readonly Color[] Palette = new Color[]
    {
        new Color(0.180f, 0.800f, 0.443f), // #2ECC71 — verde    (Clase 1, id 0)
        new Color(0.204f, 0.596f, 0.859f), // #3498DB — azul     (Clase 2, id 1)
        new Color(0.906f, 0.298f, 0.235f), // #E74C3C — rojo     (Clase 3, id 2)
        new Color(0.953f, 0.612f, 0.071f), // #F39C12 — naranja  (Clase 4, id 3)
        new Color(0.608f, 0.349f, 0.714f), // #9B59B6 — morado   (Clase 5, id 4)
        new Color(0.102f, 0.737f, 0.612f), // #1ABC9C — turquesa (Clase 6, id 5)
        new Color(0.902f, 0.494f, 0.133f), // #E67E22 — ámbar    (Clase 7, id 6)
        new Color(0.204f, 0.286f, 0.369f), // #34495E — pizarra  (Clase 8, id 7)
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
                    // Tamaño visible en el cubo [-4,4]: 0.18 = ~2.2% del espacio
                    sphere.transform.localScale = Vector3.one * 0.18f;
                    ApplyOpaqueMaterial(sphere.GetComponent<Renderer>(), baseColor);
                }
                else
                {
                    // Clasificado incorrectamente: más grande + desaturado
                    sphere.transform.localScale = Vector3.one * 0.26f;
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

                // Usar el color de la paleta por clase (mismo que los puntos) en lugar
                // del color raw de la API, que podría fallar al parsear.
                Color bmColor = GetColor(bm.class_id, data.colors);
                bmColor.a = 0.12f;   // muy transparente para no tapar los puntos
                ApplyTransparentMaterial(mr, bmColor);

                // Contorno del mesh: aristas con el mismo color de clase, más opaco.
                Color edgeColor = GetColor(bm.class_id, data.colors);
                edgeColor.a = 0.60f;
                DrawMeshEdges(meshGO.transform, normVerts, tris, vizLayerIdx, edgeColor);
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
    // Material helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Colorea un MeshRenderer opaco reutilizando el material que ya tiene
    /// (asignado por CreatePrimitive → siempre válido para el Render Pipeline activo).
    /// Sólo sobreescribimos las propiedades de color sin tocar el shader.
    /// </summary>
    private static void ApplyOpaqueMaterial(Renderer renderer, Color color)
    {
        // renderer.material crea una copia instanciada del sharedMaterial.
        // CreatePrimitive garantiza que ese material sea válido para el RP activo.
        Material mat = renderer.material;

        // URP Lit / URP Unlit → propiedad _BaseColor
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        // Built-in Standard / Sprites/Default → propiedad _Color
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);

        // Asegurarse de que sea opaco (URP: Surface Type 0 = Opaque)
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
    }

    private static void ApplyTransparentMaterial(Renderer renderer, Color color)
    {
        Material mat = CreateTransparentMaterial(color);
        if (mat != null) renderer.material = mat;
    }

    /// <summary>
    /// Devuelve un Material válido para LineRenderer.
    /// Usa Sprites/Default (siempre disponible) con _Color=blanco para que
    /// lr.startColor/endColor (vertex colors) controlen el color final.
    /// </summary>
    private static Material CreateLineRendererMaterial(Color color)
    {
        // Sprites/Default: color_final = _Color × vertex_color
        // Con _Color = blanco, el vertex color controla el resultado → startColor / endColor.
        Shader sprites = Shader.Find("Sprites/Default");
        if (sprites != null)
        {
            var mat = new Material(sprites);
            mat.color = Color.white; // vertex color (startColor/endColor) domina
            return mat;
        }

        // Fallback: URP Unlit con color directo (startColor/endColor no actúan,
        // pero al menos el LineRenderer tiene un material válido y no es magenta).
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit != null)
        {
            var mat = new Material(urpUnlit);
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        // Último recurso
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null)
        {
            var mat = new Material(urpLit);
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        Debug.LogWarning("[Visualizer3D] No se encontró ningún shader para LineRenderer.");
        return null;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        // Sprites/Default tiene alpha blending nativo. Disponible en TODOS
        // los proyectos Unity sin importar el Render Pipeline.
        // Solo asignamos color (incluye alpha) — el shader gestiona el blending.
        Shader spriteDefault = Shader.Find("Sprites/Default");
        if (spriteDefault != null)
        {
            var mat = new Material(spriteDefault);
            mat.color = color;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // Fallback: URP Particles/Unlit — alpha nativo en URP
        Shader particlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particlesUnlit != null)
        {
            var mat = new Material(particlesUnlit);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Cull", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        Debug.LogWarning("[Visualizer3D] No se encontró shader transparente.");
        return null;
    }

    /// <summary>
    /// Dibuja las aristas únicas de un mesh (sin duplicados) usando LineRenderers.
    /// Aporta el "contorno" visible de la frontera de decisión sin ocluir los puntos.
    /// </summary>
    private static void DrawMeshEdges(Transform parent, Vector3[] verts, int[] tris,
                                      int layer, Color color)
    {
        if (verts == null || tris == null) return;

        // Recopila aristas únicas (par ordenado min→max para evitar duplicados)
        var seen = new System.Collections.Generic.HashSet<long>();
        var edges = new System.Collections.Generic.List<(int, int)>();

        for (int i = 0; i < tris.Length - 2; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            TryAddEdge(seen, edges, a, b);
            TryAddEdge(seen, edges, b, c);
            TryAddEdge(seen, edges, a, c);
        }

        // Limitar a un máximo de 600 aristas para no degradar el rendimiento
        int limit = Mathf.Min(edges.Count, 600);
        // Un solo material compartido por todas las aristas (Sprites/Default
        // con vertex colors habilitados → startColor/endColor controlan el tono).
        Material edgeMat = CreateLineRendererMaterial(color);

        for (int e = 0; e < limit; e++)
        {
            var (i0, i1) = edges[e];
            var go = new GameObject("Edge");
            go.layer = layer;
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, verts[i0]);
            lr.SetPosition(1, verts[i1]);
            lr.startWidth = 0.015f;
            lr.endWidth   = 0.015f;
            if (edgeMat != null) lr.material = edgeMat;
            lr.startColor = color;
            lr.endColor   = color;
        }
    }

    private static void TryAddEdge(System.Collections.Generic.HashSet<long> seen,
                                    System.Collections.Generic.List<(int, int)> list,
                                    int a, int b)
    {
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        long key = ((long)lo << 32) | (uint)hi;
        if (seen.Add(key)) list.Add((lo, hi));
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

        Color boxColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // gris semitransparente

        // ── Bounding box (12 aristas grises finas) ───────────────────────────
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-4,-4,-4), new Vector3( 4,-4,-4),
            new Vector3( 4, 4,-4), new Vector3(-4, 4,-4),
            new Vector3(-4,-4, 4), new Vector3( 4,-4, 4),
            new Vector3( 4, 4, 4), new Vector3(-4, 4, 4),
        };
        int[,] edges = new int[,]
        {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };
        Material boxMat = CreateLineRendererMaterial(boxColor);
        for (int e = 0; e < edges.GetLength(0); e++)
        {
            var lineGO = new GameObject($"BoxEdge_{e}");
            lineGO.layer = vizLayerIdx;
            lineGO.transform.SetParent(boxRoot.transform, false);
            var lr = lineGO.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.positionCount = 2;
            lr.SetPosition(0, corners[edges[e, 0]]);
            lr.SetPosition(1, corners[edges[e, 1]]);
            lr.startWidth = 0.02f; lr.endWidth = 0.02f;
            if (boxMat != null) lr.material = boxMat;
            lr.startColor = boxColor; lr.endColor = boxColor;
        }

        // ── Ejes principales coloreados (gruesos, desde el origen hasta ±4) ──
        // X = rojo, Y = verde, Z = azul — colores estándar de Unity
        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3( 4, 0, 0), Color.red,   0.06f, "AxisX+");
        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3(-4, 0, 0),
            new Color(1f, 0.4f, 0.4f), 0.03f, "AxisX-");

        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3(0,  4, 0), Color.green, 0.06f, "AxisY+");
        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3(0, -4, 0),
            new Color(0.4f, 1f, 0.4f), 0.03f, "AxisY-");

        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3(0, 0,  4), Color.blue,  0.06f, "AxisZ+");
        DrawAxis(boxRoot.transform, vizLayerIdx,
            Vector3.zero, new Vector3(0, 0, -4),
            new Color(0.4f, 0.6f, 1f), 0.03f, "AxisZ-");

        // ── Etiquetas de ejes ─────────────────────────────────────────────────
        CreateAxisLabel(boxRoot.transform, "X", new Vector3(4.8f,  0f,   0f),   Color.red);
        CreateAxisLabel(boxRoot.transform, "Y", new Vector3(0f,    4.8f, 0f),   Color.green);
        CreateAxisLabel(boxRoot.transform, "Z", new Vector3(0f,    0f,   4.8f), Color.blue);

        // ── Marcas de graduación ──────────────────────────────────────────────
        CreateAxisTicks(boxRoot.transform, boxColor);
    }

    private static void DrawAxis(Transform parent, int layer,
        Vector3 from, Vector3 to, Color color, float width, string goName)
    {
        var go = new GameObject(goName);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = width;
        lr.endWidth   = width * 0.4f;   // flecha cónica
        var axisMat = CreateLineRendererMaterial(color);
        if (axisMat != null) lr.material = axisMat;
        lr.startColor = color;
        lr.endColor   = color;
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
        var tickMat = CreateLineRendererMaterial(color);
        if (tickMat != null) lr.material = tickMat;
        lr.startColor = color;
        lr.endColor   = color;
    }
}
