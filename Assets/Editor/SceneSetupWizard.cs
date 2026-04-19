#if UNITY_EDITOR
// ============================================================================
// SceneSetupWizard.cs
// Construye TODA la escena automáticamente desde el menú de Unity.
//
// USO:
//   1. Abre el proyecto en Unity
//   2. En el menú superior: Tools → ⚙️ Configurar Escena Completa
//   3. Acepta el diálogo → espera ~10 segundos
//   4. File → Save (Ctrl+S)
//
// Requiere: TMP instalado, URP, Input System Package
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.Events;
using TMPro;
using Object = UnityEngine.Object;

public static class SceneSetupWizard
{
    // ── Tema CLARO ────────────────────────────────────────────────────────────
    // Fondo general gris muy suave, paneles blancos, texto casi negro.
    static readonly Color BgDark       = C("#F0F2F5");  // fondo de la página
    static readonly Color BgPanel      = C("#FFFFFF");  // paneles / tarjetas
    static readonly Color BgSection    = C("#FFFFFF");  // tarjeta de sección
    static readonly Color BgSectionHdr = C("#E8F4FD");  // cabecera de sección (azul pálido)
    static readonly Color BgStatusBar  = C("#1A237E");  // barra de estado (oscura, alto contraste)
    static readonly Color BgInput      = C("#F5F7FA");  // fondo de inputs
    static readonly Color AccentBlue   = C("#1565C0");
    static readonly Color AccentGreen  = C("#2E7D32");
    static readonly Color AccentPurp   = C("#6A1B9A");
    static readonly Color AccentRed    = C("#C62828");
    static readonly Color TxtMain      = C("#1A1A2E");  // casi negro
    static readonly Color TxtSub       = C("#546E7A");  // gris azulado (etiquetas)
    static readonly Color TxtAccent    = C("#1565C0");  // azul oscuro (títulos)

    // ── Referencias compartidas entre métodos ─────────────────────────────────
    static GameObject      s_appControllerGO;
    static GameObject      s_statusBarGO;
    static GameObject      s_contentGO;       // tiene ConfigPanel
    static GameObject      s_graphsPanelGO;
    static GameObject      s_view3DPanelGO;
    static GameObject      s_classifyPanelGO;
    static GameObject      s_resultsPanelGO;
    static GameObject      s_dataPanelGO;
    static GameObject      s_root3DGO;
    static RenderTexture   s_rt;

    // Prefab de InputField TMP (cargado una vez)
    static GameObject s_inputFieldPrefab;
    static GameObject s_dropdownPrefab;

    // =========================================================================
    // REPARACION RAPIDA — solo arregla el EventSystem (sin reconstruir nada)
    // =========================================================================
    [MenuItem("Tools/Reparar EventSystem (sin interaccion)")]
    public static void FixEventSystem()
    {
        var es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            EditorUtility.DisplayDialog("No encontrado",
                "No hay EventSystem en la escena.\n" +
                "Ejecuta primero el wizard completo.", "OK");
            return;
        }

        // Quitar StandaloneInputModule (incompatible con New Input System)
        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[SceneSetup] StandaloneInputModule eliminado.");
        }

        // Agregar InputSystemUIInputModule (correcto para New Input System)
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[SceneSetup] InputSystemUIInputModule agregado.");
        }
        else
        {
            Debug.Log("[SceneSetup] InputSystemUIInputModule ya estaba presente.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("EventSystem reparado",
            "Listo. Ahora:\n" +
            "1. File → Save (Ctrl+S)\n" +
            "2. Presiona Play\n\n" +
            "Los clicks y toggles deben funcionar.", "OK");
    }

    // =========================================================================
    [MenuItem("Tools/Configurar Escena Completa")]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog(
            "Configurar Escena",
            "Este wizard creará automáticamente:\n" +
            "• Todos los GameObjects de la escena\n" +
            "• Todos los componentes y scripts\n" +
            "• Los 3 prefabs necesarios\n" +
            "• El RenderTexture para la vista 3D\n" +
            "• La capa 'Visualization'\n\n" +
            "¿Continuar?",
            "Sí, construir escena", "Cancelar"))
            return;

        try
        {
            Step("Cargando recursos TMP...", 0.02f);
            LoadTMPPrefabs();

            Step("Agregando capa Visualization...", 0.06f);
            AddVisualizationLayer();

            Step("Creando RenderTexture...", 0.10f);
            s_rt = EnsureRenderTexture();

            Step("Creando objetos base (cámaras, controladores)...", 0.15f);
            BuildBaseObjects();

            Step("Creando Canvas...", 0.20f);
            var canvas = BuildCanvas();

            Step("Creando StatusBar...", 0.25f);
            BuildStatusBar(canvas.transform);

            Step("Creando barra de exportación...", 0.30f);
            BuildExportBar(canvas.transform);

            Step("Creando panel principal...", 0.35f);
            var mainPanel  = BuildMainPanel(canvas.transform);
            var leftScroll = BuildLeftPanel(mainPanel.transform);
            var rightPanel = BuildRightPanel(mainPanel.transform);

            Step("Creando sección de DATOS...", 0.42f);
            s_contentGO = leftScroll.transform.Find("Viewport/Content").gameObject;
            BuildSeccionDatos(s_contentGO.transform);

            Step("Creando sección RED NEURONAL...", 0.50f);
            BuildSeccionRed(s_contentGO.transform);

            Step("Creando paneles del área derecha...", 0.58f);
            BuildRightContent(rightPanel.transform);

            Step("Creando prefab InstRow...", 0.65f);
            var prefabInst   = BuildInstRowPrefab();

            Step("Creando prefab SepRow...", 0.70f);
            var prefabSep    = BuildSepRowPrefab();

            Step("Creando prefab ResultRow...", 0.74f);
            var prefabResult = BuildResultRowPrefab();

            Step("Creando prefab DataRow...", 0.78f);
            var prefabData = BuildDataRowPrefab();

            Step("Asignando scripts y referencias...", 0.84f);
            WireScripts(prefabInst, prefabSep, prefabResult, prefabData);

            Step("Conectando botones...", 0.90f);
            WireButtons();

            Step("Marcando escena como modificada...", 0.97f);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("¡Escena lista!",
                "Todo configurado correctamente.\n\n" +
                "PASOS FINALES (manual):\n" +
                "① File → Save (Ctrl+S)\n" +
                "② Edit → Project Settings → Player →\n" +
                "   Active Input Handling = 'Input System Package (New)'\n" +
                "   → Unity se reinicia, guarda de nuevo\n" +
                "③ Inicia el backend Python\n" +
                "④ Presiona ▶ Play", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[SceneSetup] " + ex);
            EditorUtility.DisplayDialog("Error",
                "Ocurrió un error durante la configuración:\n\n" +
                ex.Message + "\n\nRevisa la consola (Console) para el detalle completo.", "OK");
        }
    }

    static void Step(string msg, float pct) =>
        EditorUtility.DisplayProgressBar("Construyendo escena...", msg, pct);

    // =========================================================================
    // Cargar prefabs TMP del paquete
    // =========================================================================
    static void LoadTMPPrefabs()
    {
        // Busca el prefab TMP Input Field en el paquete
        foreach (var guid in AssetDatabase.FindAssets("TMP Input Field t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Package Resources") || path.Contains("TextMesh Pro"))
            {
                s_inputFieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (s_inputFieldPrefab != null) break;
            }
        }

        // Busca el prefab TMP Dropdown
        foreach (var guid in AssetDatabase.FindAssets("TMP Dropdown t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Package Resources") || path.Contains("TextMesh Pro"))
            {
                s_dropdownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (s_dropdownPrefab != null) break;
            }
        }

        if (s_inputFieldPrefab == null)
            Debug.LogWarning("[SceneSetup] No se encontró el prefab TMP Input Field. " +
                "Importa TMP Essentials: Window → TextMeshPro → Import TMP Essential Resources");
    }

    // =========================================================================
    // Capa Visualization
    // =========================================================================
    static void AddVisualizationLayer()
    {
        var tm  = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var lay = tm.FindProperty("layers");

        for (int i = 0; i < lay.arraySize; i++)
            if (lay.GetArrayElementAtIndex(i).stringValue == "Visualization") return;

        for (int i = 8; i < lay.arraySize; i++)
        {
            var slot = lay.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = "Visualization";
                tm.ApplyModifiedProperties();
                return;
            }
        }
        Debug.LogWarning("[SceneSetup] No hay slot libre para la capa. Agrégala manualmente.");
    }

    // =========================================================================
    // RenderTexture
    // =========================================================================
    static RenderTexture EnsureRenderTexture()
    {
        const string folder = "Assets/RenderTextures";
        const string path   = folder + "/RT_3D.renderTexture";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "RenderTextures");

        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (existing != null) return existing;

        var rt = new RenderTexture(1024, 768, 24);
        rt.name = "RT_3D";
        AssetDatabase.CreateAsset(rt, path);
        AssetDatabase.SaveAssets();
        return rt;
    }

    // =========================================================================
    // Objetos base (no UI)
    // =========================================================================
    static void BuildBaseObjects()
    {
        // APIManager
        new GameObject("APIManager").AddComponent<APIManager>();

        // AppController
        s_appControllerGO = new GameObject("AppController");
        s_appControllerGO.AddComponent<AppController>();

        // Root3D (contenedor de puntos y mallas)
        s_root3DGO = new GameObject("Root3D");
        s_root3DGO.AddComponent<Visualizer3D>();

        // Camera3D
        var cam3DGO = new GameObject("Camera3D");
        var cam3D   = cam3DGO.AddComponent<Camera>();
        cam3D.clearFlags      = CameraClearFlags.SolidColor;
        cam3D.backgroundColor = new Color(0.07f, 0.07f, 0.07f);
        cam3D.depth           = 1;
        cam3D.targetTexture   = s_rt;

        int vizIdx = LayerMask.NameToLayer("Visualization");
        if (vizIdx >= 0) cam3D.cullingMask = 1 << vizIdx;
        else             cam3D.cullingMask = 0;

        cam3DGO.AddComponent<CameraOrbit>();

        // Main Camera: excluir capa Visualization
        if (Camera.main != null && vizIdx >= 0)
            Camera.main.cullingMask &= ~(1 << vizIdx);
    }

    // =========================================================================
    // Canvas
    // =========================================================================
    static GameObject BuildCanvas()
    {
        var go     = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 1280×720 como referencia: en una pantalla 1920×1080 todo se ve
        // 1.5× más grande que con referencia 1920×1080. Textos legibles.
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // InputSystemUIInputModule es el correcto para "New Input System"
            // StandaloneInputModule causa InvalidOperationException en cada frame
            es.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            // Si ya existe un EventSystem, asegurarse de que usa el módulo correcto
            var es = Object.FindObjectOfType<EventSystem>();
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Object.DestroyImmediate(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        return go;
    }

    // =========================================================================
    // StatusBar
    // =========================================================================
    static void BuildStatusBar(Transform parent)
    {
        var sb = MakeImage(parent, "StatusBar", BgStatusBar);
        s_statusBarGO = sb;
        Stretch(sb); AnchorTop(sb, 72);

        // Indicador (círculo de color)
        var ind = MakeImage(sb.transform, "ImgIndicator", C("#2ECC71"));
        PinLeft(ind, 12, 16, 16);

        // Texto principal — blanco sobre fondo azul marino oscuro
        var tStatus = MakeTMP(sb.transform, "TxtStatus", "CONECTANDO...", 15, FontStyles.Bold, Color.white);
        var tsRT = tStatus.GetComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0, 0.45f); tsRT.anchorMax = new Vector2(1, 1);
        tsRT.offsetMin = new Vector2(36, 0);    tsRT.offsetMax = new Vector2(-8, 0);
        tStatus.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Texto de detalles — blanco semitransparente
        var tDetail = MakeTMP(sb.transform, "TxtDetails", "", 11, FontStyles.Normal, C("#B0BEC5"));
        var tdRT = tDetail.GetComponent<RectTransform>();
        tdRT.anchorMin = new Vector2(0, 0); tdRT.anchorMax = new Vector2(1, 0.45f);
        tdRT.offsetMin = new Vector2(36, 2); tdRT.offsetMax = new Vector2(-8, 0);
        tDetail.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

        // Barra de progreso (Slider sin interacción)
        var pbGO = DefaultControls.CreateSlider(default);
        pbGO.name = "ProgressBar";
        pbGO.transform.SetParent(sb.transform, false);
        var pbRT = pbGO.GetComponent<RectTransform>();
        pbRT.anchorMin       = new Vector2(0, 0);
        pbRT.anchorMax       = new Vector2(1, 0);
        pbRT.pivot           = new Vector2(0.5f, 0);
        pbRT.anchoredPosition = Vector2.zero;
        pbRT.sizeDelta       = new Vector2(0, 4);
        var sl = pbGO.GetComponent<Slider>();
        sl.interactable = false; sl.minValue = 0; sl.maxValue = 1; sl.value = 0;
        var handle = pbGO.transform.Find("Handle Slide Area");
        if (handle) handle.gameObject.SetActive(false);

        sb.AddComponent<StatusBar>();
    }

    // =========================================================================
    // Barra de exportación (abajo)
    // =========================================================================
    static void BuildExportBar(Transform parent)
    {
        var bar = MakeImage(parent, "ExportBar", BgPanel);
        var rt  = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 52);

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10; hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.childForceExpandWidth = true; hlg.childControlHeight = true;

        MakeButton(bar.transform, "BtnCSV",    "Exportar CSV", AccentGreen);
        MakeButton(bar.transform, "BtnPDF",    "Exportar PDF", AccentRed);
        MakeButton(bar.transform, "BtnReset",  "Reiniciar",    C("#E65100"));
    }

    // =========================================================================
    // Panel principal (split izquierda / derecha)
    // =========================================================================
    static GameObject BuildMainPanel(Transform parent)
    {
        var mp = MakeImage(parent, "MainPanel", BgDark);
        var rt = mp.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, 52); rt.offsetMax = new Vector2(0, -72);

        var hlg = mp.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 0;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        return mp;
    }

    // =========================================================================
    // Panel izquierdo (ScrollView con layout vertical)
    // =========================================================================
    static GameObject BuildLeftPanel(Transform parent)
    {
        var sv = DefaultControls.CreateScrollView(default);
        sv.name = "LeftScroll";
        sv.transform.SetParent(parent, false);
        sv.GetComponent<Image>().color = C("#E8EDF2");  // borde exterior claro

        // Viewport también claro
        var vp = sv.transform.Find("Viewport");
        if (vp != null)
        {
            var vpImg = vp.GetComponent<Image>();
            if (vpImg != null) vpImg.color = BgPanel;
        }

        var sr = sv.GetComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var le = sv.AddComponent<LayoutElement>();
        // flexibleWidth = 1 sin preferredWidth fijo → ocupa el mismo espacio que
        // RightPanel (que también tiene flexibleWidth=1) → split 50 / 50.
        le.flexibleWidth = 1;

        // Configurar Content
        var content = sv.transform.Find("Viewport/Content");
        if (content != null)
        {
            // El Content no necesita imagen propia, pero si tiene Image la limpiamos
            var cImg = content.GetComponent<Image>();
            if (cImg != null) cImg.color = BgDark;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10; vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
            // childControlHeight = true: el VLG ASIGNA la altura de cada sección
            // basándose en su preferredHeight. Sin esto, las secciones tienen
            // altura 0 → contenido total < viewport → el scroll rebota al inicio.
            vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return sv;
    }

    // =========================================================================
    // Panel derecho
    // =========================================================================
    static GameObject BuildRightPanel(Transform parent)
    {
        var rp  = MakeImage(parent, "RightPanel", BgPanel);
        var leR = rp.AddComponent<LayoutElement>();
        leR.flexibleWidth = 1;
        // NO usamos VerticalLayoutGroup aquí.
        // TabBar y ContentArea usan anclas explícitas dentro de RightPanel,
        // lo que garantiza que TabBar siempre tenga EXACTAMENTE 38 px sin
        // depender del comportamiento de childForceExpandHeight en el VLG.
        return rp;
    }

    // =========================================================================
    // Contenido del panel derecho: TabBar (38px fijos arriba) + ContentArea (resto)
    // =========================================================================
    static void BuildRightContent(Transform parent)
    {
        const float TAB_H = 38f;  // altura del TabBar en píxeles — valor exacto y fijo

        // ── TabBar ────────────────────────────────────────────────────────────
        // Anclado al borde SUPERIOR de RightPanel, altura fija por sizeDelta.
        // Este enfoque no depende de ningún LayoutGroup padre/hijo.
        var tabBar = MakeImage(parent, "TabBar", BgSectionHdr);
        {
            var rt      = tabBar.GetComponent<RectTransform>();
            rt.anchorMin          = new Vector2(0f, 1f);   // esquina sup-izq
            rt.anchorMax          = new Vector2(1f, 1f);   // esquina sup-der
            rt.pivot              = new Vector2(0.5f, 1f); // pivote arriba
            rt.anchoredPosition   = Vector2.zero;
            rt.sizeDelta          = new Vector2(0f, TAB_H);// ancho=0 (ancla lo maneja), alto=38
        }
        var tabHLG = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHLG.spacing = 2; tabHLG.padding = new RectOffset(4, 4, 4, 4);
        tabHLG.childControlWidth  = true;  tabHLG.childForceExpandWidth  = true;
        tabHLG.childControlHeight = true;  tabHLG.childForceExpandHeight = true;

        var btnGraf  = MakeButton(tabBar.transform, "BtnTabGraficas",   "GRAFICAS",   AccentBlue,   10);
        var btnTab3D = MakeButton(tabBar.transform, "BtnTab3D",         "VISTA 3D",   C("#37474F"),  10);
        var btnClasif= MakeButton(tabBar.transform, "BtnTabClasificar", "CLASIFICAR", AccentPurp,   10);
        var btnRes   = MakeButton(tabBar.transform, "BtnTabResultados", "RESULTADOS", C("#37474F"),  10);
        var btnDatos = MakeButton(tabBar.transform, "BtnTabDatos",      "DATOS",      C("#00838F"),  10);

        // ── ContentArea ───────────────────────────────────────────────────────
        // Rellena TODO RightPanel excepto los 38 px superiores del TabBar.
        // offsetMax.y = -TAB_H deja espacio para el TabBar arriba.
        var ca = MakeImage(parent, "ContentArea", BgPanel);
        {
            var rt      = ca.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;                    // esquina inf-izq
            rt.anchorMax = Vector2.one;                     // esquina sup-der
            rt.offsetMin = Vector2.zero;                    // sin margen abajo/izq
            rt.offsetMax = new Vector2(0f, -TAB_H);        // 38 px arriba para el TabBar
        }

        // ── 4 Paneles ─────────────────────────────────────────────────────────
        s_graphsPanelGO   = BuildGraphsPanel(ca.transform);
        s_view3DPanelGO   = BuildView3DPanel(ca.transform);
        s_classifyPanelGO = BuildClassifyPanel(ca.transform);
        s_resultsPanelGO  = BuildResultsPanel(ca.transform);
        s_dataPanelGO     = BuildDataPanel(ca.transform);

        // Solo Gráficas visible al inicio
        s_view3DPanelGO.SetActive(false);
        s_classifyPanelGO.SetActive(false);
        s_resultsPanelGO.SetActive(false);
        s_dataPanelGO.SetActive(false);

        // ── Tab buttons → SetActive ───────────────────────────────────────────
        WireTab(btnGraf.GetComponent<Button>(),
            show: new[]{s_graphsPanelGO},
            hide: new[]{s_view3DPanelGO, s_classifyPanelGO, s_resultsPanelGO, s_dataPanelGO});
        WireTab(btnTab3D.GetComponent<Button>(),
            show: new[]{s_view3DPanelGO},
            hide: new[]{s_graphsPanelGO, s_classifyPanelGO, s_resultsPanelGO, s_dataPanelGO});
        WireTab(btnClasif.GetComponent<Button>(),
            show: new[]{s_classifyPanelGO},
            hide: new[]{s_graphsPanelGO, s_view3DPanelGO, s_resultsPanelGO, s_dataPanelGO});
        WireTab(btnRes.GetComponent<Button>(),
            show: new[]{s_resultsPanelGO},
            hide: new[]{s_graphsPanelGO, s_view3DPanelGO, s_classifyPanelGO, s_dataPanelGO});
        WireTab(btnDatos.GetComponent<Button>(),
            show: new[]{s_dataPanelGO},
            hide: new[]{s_graphsPanelGO, s_view3DPanelGO, s_classifyPanelGO, s_resultsPanelGO});
    }

    // ── Panel Gráficas ────────────────────────────────────────────────────────
    static GameObject BuildGraphsPanel(Transform parent)
    {
        var gp = MakeImage(parent, "GraphsPanel", BgPanel);
        Stretch(gp);
        var vlg = gp.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4; vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;

        // ── Fila 1: título + botones de tipo de gráfica ───────────────────────
        const float BTN_H = 26f;
        const float ROW_H = 32f;

        var topRow = new GameObject("TopRowGraficas");
        topRow.transform.SetParent(gp.transform, false);
        topRow.AddComponent<RectTransform>();
        var topHLG = topRow.AddComponent<HorizontalLayoutGroup>();
        topHLG.spacing = 4; topHLG.padding = new RectOffset(0, 0, 3, 3);
        topHLG.childControlWidth  = true;  topHLG.childForceExpandWidth  = false;
        topHLG.childControlHeight = true;  topHLG.childForceExpandHeight = false;
        LE(topRow, prefH: ROW_H);

        var t = MakeTMP(topRow.transform, "TxtGraphTitle", "Selecciona una grafica", 11, FontStyles.Bold, TxtAccent);
        LE(t, flexW: 1, prefH: BTN_H);

        var bErr = MakeButton(topRow.transform, "BtnErrorPlot",     "Perdida",   AccentBlue, 9);
        LE(bErr, prefW: 65, prefH: BTN_H);
        var bWgt = MakeButton(topRow.transform, "BtnWeightsPlot",   "Pesos",     AccentBlue, 9);
        LE(bWgt, prefW: 58, prefH: BTN_H);
        var bCnf = MakeButton(topRow.transform, "BtnConfusionPlot", "Confusion", AccentBlue, 9);
        LE(bCnf, prefW: 75, prefH: BTN_H);

        // ── Fila 2: controles de zoom ─────────────────────────────────────────
        // Botones − / % actual / + / Ajustar — permiten hacer zoom y scroll sobre
        // cualquier gráfica, especialmente útil para la gráfica de pesos (muy alta).
        const float ZOOM_H = 26f;
        const float ZOOM_ROW_H = 30f;

        var zoomRow = new GameObject("ZoomRow");
        zoomRow.transform.SetParent(gp.transform, false);
        zoomRow.AddComponent<RectTransform>();
        var zoomHLG = zoomRow.AddComponent<HorizontalLayoutGroup>();
        zoomHLG.spacing = 4; zoomHLG.padding = new RectOffset(0, 0, 2, 2);
        zoomHLG.childControlWidth  = true;  zoomHLG.childForceExpandWidth  = false;
        zoomHLG.childControlHeight = true;  zoomHLG.childForceExpandHeight = false;
        LE(zoomRow, prefH: ZOOM_ROW_H);

        // Separador visual izquierdo
        var zoomLbl = MakeTMP(zoomRow.transform, "LblZoom", "Zoom:", 10, FontStyles.Normal, TxtSub);
        LE(zoomLbl, prefW: 36, prefH: ZOOM_H);

        var bZoomOut = MakeButton(zoomRow.transform, "BtnZoomOut",   "−",       C("#546E7A"), 13);
        LE(bZoomOut, prefW: 28, prefH: ZOOM_H);

        var txtZoom = MakeTMP(zoomRow.transform, "TxtZoomLabel", "100 %", 10, FontStyles.Bold, TxtMain);
        txtZoom.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        LE(txtZoom, prefW: 52, prefH: ZOOM_H);

        var bZoomIn  = MakeButton(zoomRow.transform, "BtnZoomIn",    "+",       C("#546E7A"), 13);
        LE(bZoomIn,  prefW: 28, prefH: ZOOM_H);

        var bZoomRst = MakeButton(zoomRow.transform, "BtnZoomReset", "Ajustar", C("#37474F"), 9);
        LE(bZoomRst, prefW: 60, prefH: ZOOM_H);

        // Relleno flexible para empujar los botones a la izquierda
        var spacer = new GameObject("ZoomSpacer");
        spacer.transform.SetParent(zoomRow.transform, false);
        spacer.AddComponent<RectTransform>();
        LE(spacer, flexW: 1);

        // ── ScrollView con la imagen ──────────────────────────────────────────
        // scroll bidireccional: funciona cuando la imagen (escalada por zoom)
        // supera el tamaño del viewport. GraphDisplay.cs controla el tamaño
        // explícito del Content y del RawImage en runtime.
        var sv = DefaultControls.CreateScrollView(default);
        sv.name = "GraphScrollView";
        sv.transform.SetParent(gp.transform, false);
        sv.GetComponent<Image>().color = C("#F0F2F5");
        var sr = sv.GetComponent<ScrollRect>();
        sr.horizontal = true; sr.vertical = true;
        sr.scrollSensitivity = 40;
        sr.movementType = ScrollRect.MovementType.Clamped;
        LE(sv, flexH: 1, flexW: 1);

        // Content: anclaje de stretch por defecto (CreateScrollView ya lo pone así).
        // GraphDisplay.EnterFitMode() lo restaura a stretch cuando llega una imagen,
        // y GraphDisplay.EnterZoomMode() lo cambia a top-left cuando el usuario
        // hace zoom para que el scroll bidireccional funcione.
        var content = sv.transform.Find("Viewport/Content");
        if (content != null)
        {
            var cImg = content.GetComponent<Image>();
            if (cImg != null) cImg.color = Color.clear;

            // ImgGraph: stretch sobre Content con AspectRatioFitter(FitInParent).
            // Así la imagen siempre se muestra proporcional al cargar,
            // sin depender del tamaño del viewport en ese instante.
            var imgGO = new GameObject("ImgGraph");
            imgGO.transform.SetParent(content, false);
            var imgRT = imgGO.AddComponent<RectTransform>();
            imgRT.anchorMin = Vector2.zero;
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
            imgGO.AddComponent<RawImage>();
            // AspectRatioFitter: GraphDisplay.EnterFitMode() asigna aspectRatio
            // en runtime con la relación real de la textura descargada.
            imgGO.AddComponent<AspectRatioFitter>().aspectMode =
                AspectRatioFitter.AspectMode.FitInParent;
        }

        gp.AddComponent<GraphDisplay>();
        return gp;
    }

    // ── Panel Vista 3D ────────────────────────────────────────────────────────
    static GameObject BuildView3DPanel(Transform parent)
    {
        var vp  = new GameObject("View3DPanel");
        vp.transform.SetParent(parent, false);
        var rt  = vp.AddComponent<RectTransform>();
        Stretch(vp);
        vp.AddComponent<RawImage>().texture = s_rt;
        return vp;
    }

    // ── Panel Clasificar ──────────────────────────────────────────────────────
    static GameObject BuildClassifyPanel(Transform parent)
    {
        var cp = MakeImage(parent, "ClassifyPanel", BgPanel);
        Stretch(cp);
        var vlg = cp.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10; vlg.padding = new RectOffset(30, 30, 20, 20);
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;

        var title = MakeTMP(cp.transform, "TxtTituloClasif", "CLASIFICAR PUNTO 3D", 16, FontStyles.Bold, TxtAccent);
        LE(title, prefH: 24);
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // X1/X2/X3 en ASCII simple — los caracteres Unicode X₁ X₂ X₃
        // no existen en LiberationSans SDF y causan advertencias TMP.
        BuildCoordRow(cp.transform, "X1", "InputX1");
        BuildCoordRow(cp.transform, "X2", "InputX2");
        BuildCoordRow(cp.transform, "X3", "InputX3");

        var btn = MakeButton(cp.transform, "BtnClassify", "CLASIFICAR", AccentPurp);
        LE(btn, prefH: 42);

        var res = MakeTMP(cp.transform, "TxtResult", "", 18, FontStyles.Bold, TxtMain);
        res.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        LE(res, prefH: 40);

        var prob = MakeTMP(cp.transform, "TxtProbabilities", "", 12, FontStyles.Normal, TxtSub);
        prob.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        LE(prob, prefH: 60);

        var colorBox = MakeImage(cp.transform, "ImgClassColor", Color.gray);
        LE(colorBox, prefH: 22, prefW: 22);

        cp.AddComponent<ClassifyPanel>();
        return cp;
    }

    static void BuildCoordRow(Transform parent, string label, string inputName)
    {
        var row = new GameObject("Row_" + inputName);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childControlWidth  = true;  hlg.childForceExpandWidth  = false;
        hlg.childControlHeight = true;  hlg.childForceExpandHeight = true;
        LE(row, prefH: 34);   // era 48

        var lbl = MakeTMP(row.transform, "Lbl", label, 13, FontStyles.Bold, TxtAccent);
        LE(lbl, prefW: 44);

        var inp = MakeTMPInput(row.transform, inputName, "0.0");
        LE(inp, flexW: 1);
    }

    // ── Panel Resultados ──────────────────────────────────────────────────────
    static GameObject BuildResultsPanel(Transform parent)
    {
        // Contenedor raíz — ocupa todo ContentArea
        var rp = MakeImage(parent, "ResultsPanel", BgDark);
        Stretch(rp);

        // PanelResults = tarjeta blanca interior (lo que ResultsPanel.cs muestra/oculta)
        var pr = MakeImage(rp.transform, "PanelResults", BgPanel);
        Stretch(pr);
        var prVLG = pr.AddComponent<VerticalLayoutGroup>();
        prVLG.spacing = 0;
        prVLG.padding = new RectOffset(12, 12, 12, 12);
        prVLG.childControlWidth = true;  prVLG.childForceExpandWidth  = true;
        prVLG.childControlHeight = true; prVLG.childForceExpandHeight = false;

        // ── 1. Tarjeta de precisión global ─────────────────────────────────────
        var summaryCard = MakeImage(pr.transform, "SummaryCard", BgSectionHdr);
        LE(summaryCard, prefH: 72);
        var scVLG = summaryCard.AddComponent<VerticalLayoutGroup>();
        scVLG.spacing = 2;
        scVLG.padding = new RectOffset(16, 16, 10, 8);
        scVLG.childControlWidth = true;  scVLG.childForceExpandWidth  = true;
        scVLG.childControlHeight = true; scVLG.childForceExpandHeight = false;

        var lblPrec = MakeTMP(summaryCard.transform, "LblPrecisionGlobal",
            "PRECISION GLOBAL", 9, FontStyles.Bold, TxtSub);
        lblPrec.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        LE(lblPrec, prefH: 14);

        var txtAcc = MakeTMP(summaryCard.transform, "TxtAccuracy",
            "—", 26, FontStyles.Bold, TxtAccent);
        txtAcc.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        LE(txtAcc, prefH: 34);

        // ── 2. Separador fino ───────────────────────────────────────────────────
        var sep1 = MakeImage(pr.transform, "Sep1", C("#DDE3EA"));
        LE(sep1, prefH: 1);

        // ── 3. Cuadrícula de métricas secundarias (2 columnas) ─────────────────
        var metricsRow = new GameObject("MetricsGrid");
        metricsRow.transform.SetParent(pr.transform, false);
        metricsRow.AddComponent<RectTransform>();
        var mrHLG = metricsRow.AddComponent<HorizontalLayoutGroup>();
        mrHLG.spacing = 0;
        mrHLG.childControlWidth = true;  mrHLG.childForceExpandWidth  = true;
        mrHLG.childControlHeight = true; mrHLG.childForceExpandHeight = false;
        LE(metricsRow, prefH: 62);

        // Columna izquierda: Arquitectura + Activación
        var col1 = new GameObject("MetricCol1");
        col1.transform.SetParent(metricsRow.transform, false);
        col1.AddComponent<RectTransform>();
        var c1VLG = col1.AddComponent<VerticalLayoutGroup>();
        c1VLG.spacing = 4;
        c1VLG.padding = new RectOffset(8, 4, 10, 6);
        c1VLG.childControlWidth = true;  c1VLG.childForceExpandWidth  = true;
        c1VLG.childControlHeight = true; c1VLG.childForceExpandHeight = false;
        LE(col1, flexW: 1);

        var txtArch = MakeTMP(col1.transform, "TxtArchitecture",
            "Arquitectura: —", 10, FontStyles.Normal, TxtMain);
        LE(txtArch, prefH: 16);
        var txtAct = MakeTMP(col1.transform, "TxtActivation",
            "Activación: —", 10, FontStyles.Normal, TxtMain);
        LE(txtAct, prefH: 16);

        // Divisor vertical entre columnas
        var divV = MakeImage(metricsRow.transform, "DividerV", C("#DDE3EA"));
        LE(divV, prefW: 1);

        // Columna derecha: Épocas + Parámetros
        var col2 = new GameObject("MetricCol2");
        col2.transform.SetParent(metricsRow.transform, false);
        col2.AddComponent<RectTransform>();
        var c2VLG = col2.AddComponent<VerticalLayoutGroup>();
        c2VLG.spacing = 4;
        c2VLG.padding = new RectOffset(8, 4, 10, 6);
        c2VLG.childControlWidth = true;  c2VLG.childForceExpandWidth  = true;
        c2VLG.childControlHeight = true; c2VLG.childForceExpandHeight = false;
        LE(col2, flexW: 1);

        var txtEpochs = MakeTMP(col2.transform, "TxtEpochs",
            "Épocas: —", 10, FontStyles.Normal, TxtMain);
        LE(txtEpochs, prefH: 16);
        var txtParams = MakeTMP(col2.transform, "TxtParams",
            "Parámetros: —", 10, FontStyles.Normal, TxtMain);
        LE(txtParams, prefH: 16);

        // ── 4. Separador fino ───────────────────────────────────────────────────
        var sep2 = MakeImage(pr.transform, "Sep2", C("#DDE3EA"));
        LE(sep2, prefH: 1);

        // ── 5. Cabecera de la tabla (franja azul, letras blancas) ──────────────
        var tableHdr = MakeImage(pr.transform, "TableHeader", AccentBlue);
        LE(tableHdr, prefH: 13);
        tableHdr.GetComponent<LayoutElement>().minHeight = 0;
        var hhlg = tableHdr.AddComponent<HorizontalLayoutGroup>();
        hhlg.spacing = 1; hhlg.padding = new RectOffset(2, 2, 1, 1);
        hhlg.childControlWidth     = true;  hhlg.childForceExpandWidth  = true;
        // childControlHeight = false: el HLG NO calcula su altura desde los hijos
        // (evita que TMP infle el contenedor por encima de prefH:13).
        // childForceExpandHeight = true: los hijos se estiran a llenar los 13 px.
        hhlg.childControlHeight    = false; hhlg.childForceExpandHeight = true;
        foreach (var col in new[]{ "Clase", "Total", "Correct.", "Precision", "Recall", "F1" })
        {
            var h = MakeTMP(tableHdr.transform, "H_" + col, col, 8, FontStyles.Bold, Color.white);
            h.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            LE(h, flexW: 1);
        }

        // ── 6. Scroll de filas de la tabla ─────────────────────────────────────
        // flexH:1 hace que el scroll ocupe TODO el espacio restante del PanelResults.
        var sv = DefaultControls.CreateScrollView(default);
        sv.name = "TableScrollView";
        sv.transform.SetParent(pr.transform, false);
        sv.GetComponent<Image>().color = BgPanel;
        var sr = sv.GetComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.scrollSensitivity = 30;
        LE(sv, flexH: 1, flexW: 1);

        // El Content del ScrollRect se convierte directamente en el TableContainer:
        // — Una sola capa, sin anidamiento extra → layout limpio y predecible.
        // — WireScripts lo encuentra con Deep(prGO, "TableContainer").
        // — ResultsPanel.cs instancia las filas como hijos directos.
        var tableScrollContent = sv.transform.Find("Viewport/Content");
        if (tableScrollContent != null)
        {
            tableScrollContent.gameObject.name = "TableContainer";

            var cImg = tableScrollContent.GetComponent<Image>();
            if (cImg != null) cImg.color = Color.clear;

            // VerticalLayoutGroup: apila las filas y reporta su altura total al CSF.
            var cVLG = tableScrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
            cVLG.spacing = 1; cVLG.padding = new RectOffset(0, 0, 0, 0);
            cVLG.childControlWidth  = true;  cVLG.childForceExpandWidth  = true;
            cVLG.childControlHeight = true;  cVLG.childForceExpandHeight = false;

            // ContentSizeFitter: ajusta la altura del Content a sus hijos,
            // activando el scroll vertical cuando hay más filas que viewport.
            var cCSF = tableScrollContent.gameObject.AddComponent<ContentSizeFitter>();
            cCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        rp.AddComponent<ResultsPanel>();
        return rp;
    }

    // ── Panel DATOS ───────────────────────────────────────────────────────────
    static GameObject BuildDataPanel(Transform parent)
    {
        // Contenedor raíz
        var dp = MakeImage(parent, "DataPanel", BgDark);
        Stretch(dp);

        var dpVLG = dp.AddComponent<VerticalLayoutGroup>();
        dpVLG.spacing = 0;
        dpVLG.padding = new RectOffset(12, 12, 12, 12);
        dpVLG.childControlWidth = true;  dpVLG.childForceExpandWidth  = true;
        dpVLG.childControlHeight = true; dpVLG.childForceExpandHeight = false;

        // ── Tarjeta interior ──────────────────────────────────────────────────
        var card = MakeImage(dp.transform, "PanelData", BgPanel);
        var cardVLG = card.AddComponent<VerticalLayoutGroup>();
        cardVLG.spacing = 0;
        cardVLG.padding = new RectOffset(0, 0, 0, 0);
        cardVLG.childControlWidth = true;  cardVLG.childForceExpandWidth  = true;
        cardVLG.childControlHeight = true; cardVLG.childForceExpandHeight = false;
        LE(card, flexH: 1);

        // Título
        var titleRow = MakeImage(card.transform, "TitleRow", BgSectionHdr);
        LE(titleRow, prefH: 28);
        var titleHLG = titleRow.AddComponent<HorizontalLayoutGroup>();
        titleHLG.spacing = 8; titleHLG.padding = new RectOffset(12, 12, 8, 8);
        titleHLG.childControlWidth = true;  titleHLG.childForceExpandWidth  = false;
        titleHLG.childControlHeight = true; titleHLG.childForceExpandHeight = true;

        var bar = MakeImage(titleRow.transform, "AccentBar", C("#00838F"));
        LE(bar, prefW: 4, flexW: 0);
        var titleTxt = MakeTMP(titleRow.transform, "TxtTitle", "TABLA DE DATOS GENERADOS", 13, FontStyles.Bold, TxtAccent);
        LE(titleTxt, flexW: 1);

        // Estado
        var statusRow = MakeImage(card.transform, "StatusRow", C("#EEF2F7"));
        LE(statusRow, prefH: 20);
        var sHLG = statusRow.AddComponent<HorizontalLayoutGroup>();
        sHLG.spacing = 4; sHLG.padding = new RectOffset(8, 8, 4, 4);
        sHLG.childControlWidth = true;  sHLG.childForceExpandWidth  = true;
        sHLG.childControlHeight = true; sHLG.childForceExpandHeight = true;
        MakeTMP(statusRow.transform, "TxtDataStatus",
            "Genera datos y haz clic en DATOS para ver la tabla.", 10, FontStyles.Normal, TxtSub);

        // ── Cabecera de tabla ─────────────────────────────────────────────────
        var hdr = MakeImage(card.transform, "DataTableHeader", C("#00838F"));
        LE(hdr, prefH: 13);
        hdr.GetComponent<LayoutElement>().minHeight = 0;
        var dhhLG = hdr.AddComponent<HorizontalLayoutGroup>();
        dhhLG.spacing = 1; dhhLG.padding = new RectOffset(2, 2, 1, 1);
        dhhLG.childControlWidth     = true;  dhhLG.childForceExpandWidth  = true;
        // Mismo patrón que Results: desactivar childControlHeight para que TMP
        // no infle el contenedor, y activar childForceExpandHeight para estirar.
        dhhLG.childControlHeight    = false; dhhLG.childForceExpandHeight = true;
        foreach (var col in new[]{ "X1", "X2", "X3", "CLASE" })
        {
            var h = MakeTMP(hdr.transform, "DH_" + col, col, 8, FontStyles.Bold, Color.white);
            h.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            LE(h, flexW: 1);
        }

        // ── Scroll de filas ───────────────────────────────────────────────────
        var sv = DefaultControls.CreateScrollView(default);
        sv.name = "DataScrollView";
        sv.transform.SetParent(card.transform, false);
        sv.GetComponent<Image>().color = BgPanel;
        sv.GetComponent<ScrollRect>().horizontal = false;
        LE(sv, flexH: 1);

        var dataContent = sv.transform.Find("Viewport/Content");
        if (dataContent != null)
        {
            dataContent.name = "DataTableContainer";
            var cVLG = dataContent.gameObject.AddComponent<VerticalLayoutGroup>();
            cVLG.spacing = 1; cVLG.padding = new RectOffset(0, 0, 0, 0);
            cVLG.childControlWidth  = true;  cVLG.childForceExpandWidth  = true;
            cVLG.childControlHeight = true;  cVLG.childForceExpandHeight = false;
            var cCSF = dataContent.gameObject.AddComponent<ContentSizeFitter>();
            cCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        dp.AddComponent<DataPanel>();
        return dp;
    }

    static void MetricLabel(Transform p, string name, string text, float sz, FontStyles style, Color col)
    {
        var g = MakeTMP(p, name, text, sz, style, col);
        LE(g, prefH: (sz > 15 ? 32 : sz > 12 ? 26 : 22));
    }

    // =========================================================================
    // Sección DATOS
    // =========================================================================
    static void BuildSeccionDatos(Transform parent)
    {
        var sec = BuildSection(parent, "SeccionDatos", "── DATOS ──");

        // Número de clases
        SectionLbl(sec.transform, "Número de clases  (2 – 10):");
        var rowC = HRow(sec.transform, "RowClases", 34);
        var lblClases = MakeTMP(rowC.transform, "LblClases", "Clases:", 12, FontStyles.Bold, TxtMain);
        LE(lblClases, prefW: 72);
        var slClases = MakeSlider(rowC.transform, "SliderClases", 2, 10, 3, true);
        LE(slClases, flexW: 1);
        var inClases = MakeTMPInput(rowC.transform, "InputClases", "3");
        LE(inClases, prefW: 60);

        // Instancias
        SectionLbl(sec.transform, "Instancias por clase  (total = instancias x clases):");
        var grpInst = HRow(sec.transform, "GrupoInstancias", 28);
        var tgInst = grpInst.AddComponent<ToggleGroup>();
        var togIg  = MakeToggle(grpInst.transform, "ToggleInstIguales",   "Igual por clase",    tgInst, true);
        LE(togIg, prefW: 120);
        var togDif = MakeToggle(grpInst.transform, "ToggleInstDiferentes","Diferente por clase", tgInst, false);
        LE(togDif, prefW: 150);

        // Panel "igual"
        var pIg = HRow(sec.transform, "PanelInstIguales", 30);
        var lblIg = MakeTMP(pIg.transform, "Lbl", "Inst. por clase:", 12, FontStyles.Bold, TxtMain);
        LE(lblIg, prefW: 120);
        var inIg = MakeTMPInput(pIg.transform, "InputInstIguales", "100");
        LE(inIg, flexW: 1);

        // Panel "diferente" (scroll con filas dinámicas)
        var pDif = BuildScrollSection(sec.transform, "PanelInstDiferentes", 100);
        pDif.SetActive(false);

        // Separabilidad
        SectionLbl(sec.transform, "Separabilidad  (que tan separadas quedan las clases):");
        var grpSepM = HRow(sec.transform, "GrupoSepModo", 28);
        var tgSepM = grpSepM.AddComponent<ToggleGroup>();
        var togSAuto = MakeToggle(grpSepM.transform, "ToggleSepAuto",   "Automática", tgSepM, false);
        LE(togSAuto, prefW: 100);
        var togSMan  = MakeToggle(grpSepM.transform, "ToggleSepManual", "Manual",     tgSepM, true);
        LE(togSMan, prefW: 80);

        // Panel manual de separabilidad
        var pSepM = MakeImage(sec.transform, "PanelSepManual", C("#EEF2F7"));
        var pSepMVLG = pSepM.AddComponent<VerticalLayoutGroup>();
        pSepMVLG.spacing = 5; pSepMVLG.padding = new RectOffset(8, 8, 6, 6);
        pSepMVLG.childControlWidth  = true;  pSepMVLG.childForceExpandWidth  = true;
        pSepMVLG.childControlHeight = true;  pSepMVLG.childForceExpandHeight = false;
        // NO ponemos LE.prefH fijo: con childControlHeight=true el VLG calcula
        // la altura real desde sus hijos. La sección lo propagará hacia arriba.

        // Global vs Por clase
        var grpSepT = HRow(pSepM.transform, "GrupoSepTipo", 25);
        var tgSepT = grpSepT.AddComponent<ToggleGroup>();
        var togGlob = MakeToggle(grpSepT.transform, "ToggleSepGlobal",   "Global",   tgSepT, true);
        LE(togGlob, prefW: 80);
        var togPC   = MakeToggle(grpSepT.transform, "ToggleSepPorClase", "Por clase", tgSepT, false);
        LE(togPC, prefW: 100);

        // Panel global (sliders de separación)
        var pGlob = MakeImage(pSepM.transform, "PanelSepGlobal", Color.clear);
        pGlob.GetComponent<Image>().raycastTarget = false;
        var pGlobVLG = pGlob.AddComponent<VerticalLayoutGroup>();
        pGlobVLG.spacing = 4;
        pGlobVLG.childControlWidth  = true;  pGlobVLG.childForceExpandWidth  = true;
        pGlobVLG.childControlHeight = true;  pGlobVLG.childForceExpandHeight = false;

        BuildSliderRow(pGlob.transform, "MeanSep", "Separación (0.5–6):", "SliderMeanSep", "InputMeanSep", 0.5f, 6f, 2.5f, "2.50");
        BuildSliderRow(pGlob.transform, "StdDev",  "Dispersión (0.1–2.5):", "SliderStdDev",  "InputStdDev",  0.1f, 2.5f, 0.6f, "0.60");

        // Panel por clase (scroll)
        var pPC = BuildScrollSection(pSepM.transform, "PanelSepPorClase", 110);
        pPC.SetActive(false);

        // Semilla
        SectionLbl(sec.transform, "Semilla aleatoria  (mismo numero = mismos datos):");
        var rowSeed = HRow(sec.transform, "RowSeed", 30);
        var lblSeed = MakeTMP(rowSeed.transform, "LblSeed", "Semilla:", 12, FontStyles.Bold, TxtMain);
        LE(lblSeed, prefW: 80);
        var inSeed = MakeTMPInput(rowSeed.transform, "InputSeed", "42");
        LE(inSeed, flexW: 1);

        // Botón
        var btnG = MakeButton(sec.transform, "BtnGenerate", "GENERAR DATOS", AccentGreen);
        LE(btnG, prefH: 52);
    }

    static void BuildSliderRow(Transform parent, string id, string label,
        string sliderName, string inputName, float min, float max, float val, string def)
    {
        var row = HRow(parent, "Row" + id, 26);
        var lbl = MakeTMP(row.transform, "Lbl", label, 11, FontStyles.Normal, TxtSub);
        LE(lbl, prefW: 148);
        var sl = MakeSlider(row.transform, sliderName, min, max, val, false);
        LE(sl, flexW: 1);
        var inp = MakeTMPInput(row.transform, inputName, def);
        LE(inp, prefW: 62);
    }

    // =========================================================================
    // Sección RED NEURONAL
    // =========================================================================
    static void BuildSeccionRed(Transform parent)
    {
        var sec = BuildSection(parent, "SeccionRed", "── RED NEURONAL ──");

        // Capas
        SectionLbl(sec.transform, "Capas ocultas  (neuronas intermedias de la red):");
        var grpCap = HRow(sec.transform, "GrupoCapas", 28);
        var tgCap = grpCap.AddComponent<ToggleGroup>();
        var togCAuto = MakeToggle(grpCap.transform, "ToggleCapasAuto",   "Automático",  tgCap, true);
        LE(togCAuto, prefW: 105);
        var togCMan  = MakeToggle(grpCap.transform, "ToggleCapasManual", "Manual",      tgCap, false);
        LE(togCMan, prefW: 80);

        var pCapM = HRow(sec.transform, "PanelCapasManual", 30);
        var lblCap = MakeTMP(pCapM.transform, "Lbl", "Neuronas (ej: 16,8):", 12, FontStyles.Normal, TxtSub);
        LE(lblCap, prefW: 175);
        var inCap = MakeTMPInput(pCapM.transform, "InputCapasManuales", "16,8");
        LE(inCap, flexW: 1);
        pCapM.SetActive(false);

        // Activación
        SectionLbl(sec.transform, "Funcion de activacion  (como procesa cada neurona):");
        var grpAct = HRow(sec.transform, "GrupoActiv", 28);
        var tgAct = grpAct.AddComponent<ToggleGroup>();
        var togAAuto = MakeToggle(grpAct.transform, "ToggleActAuto",   "Auto (ReLU)", tgAct, true);
        LE(togAAuto, prefW: 115);
        var togAMan  = MakeToggle(grpAct.transform, "ToggleActManual", "Manual",      tgAct, false);
        LE(togAMan, prefW: 80);

        var pActM = HRow(sec.transform, "PanelActManual", 32);
        var lblAct = MakeTMP(pActM.transform, "Lbl", "Funcion:", 12, FontStyles.Normal, TxtSub);
        LE(lblAct, prefW: 80);
        var dd = MakeTMPDropdown(pActM.transform, "DropdownActivacion", new[]{"ReLU","Sigmoid","Tanh"});
        LE(dd, flexW: 1);
        pActM.SetActive(false);

        // Hiperparámetros
        SectionLbl(sec.transform, "Hiperparametros de entrenamiento:");
        BuildParamRow(sec.transform, "RowLR",      "Learning Rate (velocidad):",  "InputLearningRate", "0.0100");
        BuildParamRow(sec.transform, "RowEpochs",  "Epocas (veces que aprende):", "InputEpochs",       "1000");
        BuildParamRow(sec.transform, "RowMomentum","Momentum (inercia):",         "InputMomentum",     "0.90");
        BuildParamRow(sec.transform, "RowRecord",  "Registrar cada N epocas:",   "InputRecordEvery",  "10");

        // Botón
        var btnT = MakeButton(sec.transform, "BtnTrain", "ENTRENAR", AccentBlue);
        LE(btnT, prefH: 52);
    }

    static void BuildParamRow(Transform parent, string rowName, string label, string inputName, string def)
    {
        var row = HRow(parent, rowName, 28);
        var lbl = MakeTMP(row.transform, "Lbl", label, 12, FontStyles.Normal, TxtSub);
        LE(lbl, prefW: 200);
        var inp = MakeTMPInput(row.transform, inputName, def);
        LE(inp, flexW: 1);
    }

    // =========================================================================
    // Prefabs
    // =========================================================================
    static void EnsurePrefabsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    static GameObject BuildInstRowPrefab()
    {
        EnsurePrefabsFolder();
        var root = new GameObject("InstRow");
        root.AddComponent<RectTransform>();
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8; hlg.padding = new RectOffset(4,4,2,2);
        hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
        LE(root, prefH: 30, flexW: 1);

        var lbl = MakeTMP(root.transform, "LabelClase", "Clase:", 12, FontStyles.Bold, C("#1A1A2E"));
        LE(lbl, prefW: 68);
        var inp = MakeTMPInput(root.transform, "InputInst", "100");
        LE(inp, flexW: 1);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/InstRow.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildSepRowPrefab()
    {
        EnsurePrefabsFolder();
        var root = new GameObject("SepRow");
        root.AddComponent<RectTransform>();
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3; vlg.padding = new RectOffset(4,4,3,3);
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;
        LE(root, prefH: 70, flexW: 1);

        var lbl = MakeTMP(root.transform, "LabelClaseSep", "Clase:", 12, FontStyles.Bold, C("#1A1A2E"));
        LE(lbl, prefH: 18);
        var slM = MakeSlider(root.transform, "SliderMeanSep", 0.5f, 6f,  2.5f, false);
        LE(slM, prefH: 20);
        var slS = MakeSlider(root.transform, "SliderStdDev",  0.1f, 2.5f, 0.6f, false);
        LE(slS, prefH: 20);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/SepRow.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildResultRowPrefab()
    {
        EnsurePrefabsFolder();
        var root = new GameObject("ResultRow");
        root.AddComponent<RectTransform>();
        // Fondo blanco alternado con franja sutil (el color real lo aplica ResultsPanel en runtime)
        root.AddComponent<Image>().color = C("#F8FAFB");
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2;
        hlg.padding = new RectOffset(2, 2, 2, 2);   // 2px arriba+abajo → cabe fuente 9pt
        hlg.childControlWidth  = true;  hlg.childForceExpandWidth  = true;
        hlg.childControlHeight = true;  hlg.childForceExpandHeight = true;
        LE(root, prefH: 17, flexW: 1);  // 9pt ≈ 11px + 4px padding = 15px; 17 da margen
        root.GetComponent<LayoutElement>().minHeight = 0;

        // Columnas en el mismo orden que la cabecera de la tabla
        var cols = new (string name, float pW, float fW)[]
        {
            ("ColClase",     0, 1),   // Clase (ej. "Clase 1")
            ("ColTotal",     0, 1),   // Total
            ("ColCorrectas", 0, 1),   // Correctas
            ("ColPrecision", 0, 1),   // Precisión
            ("ColRecall",    0, 1),   // Recall
            ("ColF1",        0, 1),   // F1
        };
        foreach (var (colName, pW, fW) in cols)
        {
            var col = MakeTMP(root.transform, colName, "—", 9, FontStyles.Normal, C("#212121"));
            col.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var cLE = col.GetComponent<LayoutElement>() ?? col.AddComponent<LayoutElement>();
            if (pW > 0) cLE.preferredWidth = pW;
            cLE.flexibleWidth = fW;
            cLE.minHeight = 0;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/ResultRow.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildDataRowPrefab()
    {
        EnsurePrefabsFolder();
        var root = new GameObject("DataRow");
        root.AddComponent<RectTransform>();
        root.AddComponent<Image>().color = C("#F8FAFB");
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2;
        hlg.padding = new RectOffset(2, 2, 2, 2);   // 2px arriba+abajo
        hlg.childControlWidth  = true;  hlg.childForceExpandWidth  = true;
        hlg.childControlHeight = true;  hlg.childForceExpandHeight = true;
        LE(root, prefH: 17, flexW: 1);  // 9pt + 4px padding; 17 da margen
        root.GetComponent<LayoutElement>().minHeight = 0;

        // Columnas: X1, X2, X3, CLASE
        foreach (var colName in new[]{ "ColX1", "ColX2", "ColX3", "ColClase" })
        {
            var col = MakeTMP(root.transform, colName, "—", 9, FontStyles.Normal, C("#212121"));
            col.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var cLE = col.GetComponent<LayoutElement>() ?? col.AddComponent<LayoutElement>();
            cLE.flexibleWidth = 1;
            cLE.minHeight = 0;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/DataRow.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // =========================================================================
    // Asignar referencias a todos los scripts
    // =========================================================================
    static void WireScripts(GameObject prefabInst, GameObject prefabSep, GameObject prefabResult, GameObject prefabData)
    {
        var secDatos = s_contentGO.transform.Find("SeccionDatos");
        var secRed   = s_contentGO.transform.Find("SeccionRed");

        // ── AppController ─────────────────────────────────────────────────────
        Assign(s_appControllerGO.GetComponent<AppController>(), ac => {
            ac.FindProperty("statusBar").objectReferenceValue     = s_statusBarGO.GetComponent<StatusBar>();
            ac.FindProperty("graphDisplay").objectReferenceValue  = s_graphsPanelGO.GetComponent<GraphDisplay>();
            ac.FindProperty("visualizer3D").objectReferenceValue  = s_root3DGO.GetComponent<Visualizer3D>();
            ac.FindProperty("classifyPanel").objectReferenceValue = s_classifyPanelGO.GetComponent<ClassifyPanel>();
            ac.FindProperty("resultsPanel").objectReferenceValue  = s_resultsPanelGO.GetComponent<ResultsPanel>();
            ac.FindProperty("dataPanel").objectReferenceValue     = s_dataPanelGO.GetComponent<DataPanel>();
            // configPanel se asigna después de añadir ConfigPanel
        });

        // ── StatusBar ─────────────────────────────────────────────────────────
        Assign(s_statusBarGO.GetComponent<StatusBar>(), sb => {
            sb.FindProperty("txtStatus").objectReferenceValue   = Deep(s_statusBarGO,"TxtStatus")?.GetComponent<TextMeshProUGUI>();
            sb.FindProperty("txtDetails").objectReferenceValue  = Deep(s_statusBarGO,"TxtDetails")?.GetComponent<TextMeshProUGUI>();
            sb.FindProperty("imgIndicator").objectReferenceValue= Deep(s_statusBarGO,"ImgIndicator")?.GetComponent<Image>();
            sb.FindProperty("progressBar").objectReferenceValue = Deep(s_statusBarGO,"ProgressBar")?.GetComponent<Slider>();
        });

        // ── GraphDisplay ──────────────────────────────────────────────────────
        Assign(s_graphsPanelGO.GetComponent<GraphDisplay>(), gd => {
            gd.FindProperty("imgGraph").objectReferenceValue      = Deep(s_graphsPanelGO,"ImgGraph")?.GetComponent<RawImage>();
            gd.FindProperty("panelGraphs").objectReferenceValue   = s_graphsPanelGO;
            gd.FindProperty("txtGraphTitle").objectReferenceValue = Deep(s_graphsPanelGO,"TxtGraphTitle")?.GetComponent<TextMeshProUGUI>();
            // Zoom / scroll
            gd.FindProperty("scrollContent").objectReferenceValue =
                s_graphsPanelGO.transform.Find("GraphScrollView/Viewport/Content")
                                         ?.GetComponent<RectTransform>();
            gd.FindProperty("txtZoomLabel").objectReferenceValue  =
                Deep(s_graphsPanelGO,"TxtZoomLabel")?.GetComponent<TextMeshProUGUI>();
        });

        // ── ClassifyPanel ─────────────────────────────────────────────────────
        Assign(s_classifyPanelGO.GetComponent<ClassifyPanel>(), cp => {
            cp.FindProperty("inputX1").objectReferenceValue          = Deep(s_classifyPanelGO,"InputX1")?.GetComponent<TMP_InputField>();
            cp.FindProperty("inputX2").objectReferenceValue          = Deep(s_classifyPanelGO,"InputX2")?.GetComponent<TMP_InputField>();
            cp.FindProperty("inputX3").objectReferenceValue          = Deep(s_classifyPanelGO,"InputX3")?.GetComponent<TMP_InputField>();
            cp.FindProperty("txtResult").objectReferenceValue        = Deep(s_classifyPanelGO,"TxtResult")?.GetComponent<TextMeshProUGUI>();
            cp.FindProperty("txtProbabilities").objectReferenceValue = Deep(s_classifyPanelGO,"TxtProbabilities")?.GetComponent<TextMeshProUGUI>();
            cp.FindProperty("imgClassColor").objectReferenceValue    = Deep(s_classifyPanelGO,"ImgClassColor")?.GetComponent<Image>();
        });

        // ── ResultsPanel ──────────────────────────────────────────────────────
        Assign(s_resultsPanelGO.GetComponent<ResultsPanel>(), rp => {
            var prGO = s_resultsPanelGO.transform.Find("PanelResults")?.gameObject;
            rp.FindProperty("panelResults").objectReferenceValue    = prGO;
            // Usar Deep() porque los labels pueden estar en sub-tarjetas anidadas
            rp.FindProperty("txtAccuracy").objectReferenceValue     = Deep(prGO, "TxtAccuracy")?.GetComponent<TextMeshProUGUI>();
            rp.FindProperty("txtArchitecture").objectReferenceValue = Deep(prGO, "TxtArchitecture")?.GetComponent<TextMeshProUGUI>();
            rp.FindProperty("txtActivation").objectReferenceValue   = Deep(prGO, "TxtActivation")?.GetComponent<TextMeshProUGUI>();
            rp.FindProperty("txtEpochs").objectReferenceValue       = Deep(prGO, "TxtEpochs")?.GetComponent<TextMeshProUGUI>();
            rp.FindProperty("txtParams").objectReferenceValue       = Deep(prGO, "TxtParams")?.GetComponent<TextMeshProUGUI>();
            // TableContainer es el propio Content del TableScrollView (renombrado).
            rp.FindProperty("tableContainer").objectReferenceValue  =
                prGO?.transform.Find("TableScrollView/Viewport/TableContainer")
                ?? Deep(prGO, "TableContainer")?.transform;
            rp.FindProperty("rowPrefab").objectReferenceValue       = prefabResult;
        });

        // ── DataPanel ─────────────────────────────────────────────────────────
        Assign(s_dataPanelGO.GetComponent<DataPanel>(), dp => {
            var pdGO = s_dataPanelGO.transform.Find("PanelData")?.gameObject;
            dp.FindProperty("panelData").objectReferenceValue    = pdGO;
            dp.FindProperty("txtStatus").objectReferenceValue    = Deep(pdGO, "TxtDataStatus")?.GetComponent<TextMeshProUGUI>();
            dp.FindProperty("tableContainer").objectReferenceValue =
                pdGO?.transform.Find("DataScrollView/Viewport/DataTableContainer")
                ?? Deep(pdGO, "DataTableContainer")?.transform;
            dp.FindProperty("rowPrefab").objectReferenceValue    = prefabData;
        });

        // ── Visualizer3D ──────────────────────────────────────────────────────
        Assign(s_root3DGO.GetComponent<Visualizer3D>(), v => {
            v.FindProperty("root3D").objectReferenceValue       = s_root3DGO.transform;
            v.FindProperty("renderTexture").objectReferenceValue = s_rt;
            v.FindProperty("cam3D").objectReferenceValue        = GameObject.Find("Camera3D")?.GetComponent<Camera>();
        });

        // ── CameraOrbit ───────────────────────────────────────────────────────
        var cam3DGO = GameObject.Find("Camera3D");
        Assign(cam3DGO?.GetComponent<CameraOrbit>(), orb => {
            orb.FindProperty("target").objectReferenceValue      = s_root3DGO.transform;
            orb.FindProperty("viewportRect").objectReferenceValue = s_view3DPanelGO?.GetComponent<RectTransform>();
            orb.FindProperty("orbitSpeed").floatValue  = 200f;
            orb.FindProperty("zoomSpeed").floatValue   = 5f;
            orb.FindProperty("panSpeed").floatValue    = 0.02f;
            orb.FindProperty("minDistance").floatValue = 3f;
            orb.FindProperty("maxDistance").floatValue = 25f;
        });

        // ── ConfigPanel (añadir al Content y asignar todo) ────────────────────
        var cfg = s_contentGO.AddComponent<ConfigPanel>();
        Assign(cfg, cp => {
            // Clases
            cp.FindProperty("sliderClases").objectReferenceValue = Deep(secDatos?.gameObject,"SliderClases")?.GetComponent<Slider>();
            cp.FindProperty("inputClases").objectReferenceValue  = Deep(secDatos?.gameObject,"InputClases")?.GetComponent<TMP_InputField>();
            // Instancias
            cp.FindProperty("toggleInstIguales").objectReferenceValue    = Deep(secDatos?.gameObject,"ToggleInstIguales")?.GetComponent<Toggle>();
            cp.FindProperty("toggleInstDiferentes").objectReferenceValue = Deep(secDatos?.gameObject,"ToggleInstDiferentes")?.GetComponent<Toggle>();
            cp.FindProperty("panelInstIguales").objectReferenceValue     = Deep(secDatos?.gameObject,"PanelInstIguales");
            cp.FindProperty("inputInstIguales").objectReferenceValue     = Deep(secDatos?.gameObject,"InputInstIguales")?.GetComponent<TMP_InputField>();
            cp.FindProperty("panelInstDiferentes").objectReferenceValue  = Deep(secDatos?.gameObject,"PanelInstDiferentes");
            cp.FindProperty("containerInstDifer").objectReferenceValue   = Deep(secDatos?.gameObject,"PanelInstDiferentes")?.transform.Find("Viewport/Content");
            cp.FindProperty("prefabInstRow").objectReferenceValue        = prefabInst;
            // Separabilidad
            cp.FindProperty("toggleSepAuto").objectReferenceValue     = Deep(secDatos?.gameObject,"ToggleSepAuto")?.GetComponent<Toggle>();
            cp.FindProperty("toggleSepManual").objectReferenceValue   = Deep(secDatos?.gameObject,"ToggleSepManual")?.GetComponent<Toggle>();
            cp.FindProperty("panelSepManual").objectReferenceValue    = Deep(secDatos?.gameObject,"PanelSepManual");
            cp.FindProperty("toggleSepGlobal").objectReferenceValue   = Deep(secDatos?.gameObject,"ToggleSepGlobal")?.GetComponent<Toggle>();
            cp.FindProperty("toggleSepPorClase").objectReferenceValue = Deep(secDatos?.gameObject,"ToggleSepPorClase")?.GetComponent<Toggle>();
            cp.FindProperty("panelSepGlobal").objectReferenceValue    = Deep(secDatos?.gameObject,"PanelSepGlobal");
            cp.FindProperty("sliderMeanSep").objectReferenceValue     = Deep(secDatos?.gameObject,"SliderMeanSep")?.GetComponent<Slider>();
            cp.FindProperty("inputMeanSep").objectReferenceValue      = Deep(secDatos?.gameObject,"InputMeanSep")?.GetComponent<TMP_InputField>();
            cp.FindProperty("sliderStdDev").objectReferenceValue      = Deep(secDatos?.gameObject,"SliderStdDev")?.GetComponent<Slider>();
            cp.FindProperty("inputStdDev").objectReferenceValue       = Deep(secDatos?.gameObject,"InputStdDev")?.GetComponent<TMP_InputField>();
            cp.FindProperty("panelSepPorClase").objectReferenceValue  = Deep(secDatos?.gameObject,"PanelSepPorClase");
            cp.FindProperty("containerSepClases").objectReferenceValue= Deep(secDatos?.gameObject,"PanelSepPorClase")?.transform.Find("Viewport/Content");
            cp.FindProperty("prefabSepRow").objectReferenceValue      = prefabSep;
            // Semilla
            cp.FindProperty("inputSeed").objectReferenceValue = Deep(secDatos?.gameObject,"InputSeed")?.GetComponent<TMP_InputField>();
            // Capas
            cp.FindProperty("toggleCapasAuto").objectReferenceValue   = Deep(secRed?.gameObject,"ToggleCapasAuto")?.GetComponent<Toggle>();
            cp.FindProperty("toggleCapasManual").objectReferenceValue = Deep(secRed?.gameObject,"ToggleCapasManual")?.GetComponent<Toggle>();
            cp.FindProperty("panelCapasManual").objectReferenceValue  = Deep(secRed?.gameObject,"PanelCapasManual");
            cp.FindProperty("inputCapasManuales").objectReferenceValue= Deep(secRed?.gameObject,"InputCapasManuales")?.GetComponent<TMP_InputField>();
            // Activación
            cp.FindProperty("toggleActAuto").objectReferenceValue    = Deep(secRed?.gameObject,"ToggleActAuto")?.GetComponent<Toggle>();
            cp.FindProperty("toggleActManual").objectReferenceValue  = Deep(secRed?.gameObject,"ToggleActManual")?.GetComponent<Toggle>();
            cp.FindProperty("panelActManual").objectReferenceValue   = Deep(secRed?.gameObject,"PanelActManual");
            cp.FindProperty("dropdownActivacion").objectReferenceValue = Deep(secRed?.gameObject,"DropdownActivacion")?.GetComponent<TMP_Dropdown>();
            // Hiperparámetros
            cp.FindProperty("inputLearningRate").objectReferenceValue = Deep(secRed?.gameObject,"InputLearningRate")?.GetComponent<TMP_InputField>();
            cp.FindProperty("inputEpochs").objectReferenceValue       = Deep(secRed?.gameObject,"InputEpochs")?.GetComponent<TMP_InputField>();
            cp.FindProperty("inputMomentum").objectReferenceValue     = Deep(secRed?.gameObject,"InputMomentum")?.GetComponent<TMP_InputField>();
            cp.FindProperty("inputRecordEvery").objectReferenceValue  = Deep(secRed?.gameObject,"InputRecordEvery")?.GetComponent<TMP_InputField>();
        });

        // Ahora asignar configPanel en AppController (cfg ya existe)
        Assign(s_appControllerGO.GetComponent<AppController>(), ac => {
            ac.FindProperty("configPanel").objectReferenceValue = s_contentGO.GetComponent<ConfigPanel>();
        });
    }

    // =========================================================================
    // Conectar botones → AppController
    // =========================================================================
    static void WireButtons()
    {
        var ac       = s_appControllerGO.GetComponent<AppController>();
        var secDatos = s_contentGO.transform.Find("SeccionDatos");
        var secRed   = s_contentGO.transform.Find("SeccionRed");
        var exportBar= GameObject.Find("ExportBar")?.transform;

        ConnectBtn(Deep(secDatos?.gameObject,"BtnGenerate"),       ac, "OnGenerateClicked");
        ConnectBtn(Deep(secRed?.gameObject,  "BtnTrain"),          ac, "OnTrainClicked");
        ConnectBtn(Deep(s_graphsPanelGO,     "BtnErrorPlot"),      ac, "OnShowErrorPlot");
        ConnectBtn(Deep(s_graphsPanelGO,     "BtnWeightsPlot"),    ac, "OnShowWeightsPlot");
        ConnectBtn(Deep(s_graphsPanelGO,     "BtnConfusionPlot"),  ac, "OnShowConfusionPlot");
        ConnectBtn(Deep(s_classifyPanelGO,   "BtnClassify"),       ac, "OnClassifyClicked");
        ConnectBtn(exportBar?.Find("BtnCSV")?.gameObject,   ac, "OnExportCSV");
        ConnectBtn(exportBar?.Find("BtnPDF")?.gameObject,   ac, "OnExportPDF");
        ConnectBtn(exportBar?.Find("BtnReset")?.gameObject, ac, "OnResetClicked");

        // Zoom de gráficas → GraphDisplay directamente
        var gd = s_graphsPanelGO.GetComponent<GraphDisplay>();
        ConnectBtnDirect(Deep(s_graphsPanelGO, "BtnZoomIn"),    gd, "ZoomIn");
        ConnectBtnDirect(Deep(s_graphsPanelGO, "BtnZoomOut"),   gd, "ZoomOut");
        ConnectBtnDirect(Deep(s_graphsPanelGO, "BtnZoomReset"), gd, "ResetZoom");

        // BtnTab3D también carga los datos 3D
        var btn3D = GameObject.Find("BtnTab3D");
        if (btn3D != null)
            UnityEventTools.AddPersistentListener(
                btn3D.GetComponent<Button>().onClick,
                (UnityAction)ac.OnShow3D);

        // BtnTabDatos también carga la tabla de datos
        var btnDatos = GameObject.Find("BtnTabDatos");
        if (btnDatos != null)
            UnityEventTools.AddPersistentListener(
                btnDatos.GetComponent<Button>().onClick,
                (UnityAction)ac.OnShowData);
    }

    static void ConnectBtn(GameObject go, AppController ac, string method)
    {
        if (go == null) { Debug.LogWarning("[SceneSetup] Botón no encontrado: " + method); return; }
        var btn = go.GetComponent<Button>();
        if (btn == null) return;
        var mi = typeof(AppController).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (mi == null) return;
        var del = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), ac, mi);
        UnityEventTools.AddPersistentListener(btn.onClick, del);
    }

    /// <summary>
    /// Conecta un botón a un método de cualquier componente (no solo AppController).
    /// </summary>
    static void ConnectBtnDirect(GameObject go, Component target, string method)
    {
        if (go == null || target == null) return;
        var btn = go.GetComponent<Button>();
        if (btn == null) return;
        var mi = target.GetType().GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (mi == null) { Debug.LogWarning($"[SceneSetup] Método '{method}' no encontrado en {target.GetType().Name}"); return; }
        var del = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, mi);
        UnityEventTools.AddPersistentListener(btn.onClick, del);
    }

    static void WireTab(Button btn, GameObject[] show, GameObject[] hide)
    {
        if (btn == null) return;
        foreach (var go in show)
        {
            if (go == null) continue;
            UnityEventTools.AddBoolPersistentListener(btn.onClick, (UnityAction<bool>)go.SetActive, true);
        }
        foreach (var go in hide)
        {
            if (go == null) continue;
            UnityEventTools.AddBoolPersistentListener(btn.onClick, (UnityAction<bool>)go.SetActive, false);
        }
    }

    // =========================================================================
    // Helpers de construcción de UI
    // =========================================================================

    static GameObject BuildSection(Transform parent, string name, string titleText)
    {
        // ── Tarjeta blanca (estructura PLANA — una sola VLG, todo en el mismo nivel)
        // IMPORTANTE: no usar VLG anidados con childControlHeight=false porque
        // la propagación de altura falla → contenido colapsa → scroll no funciona.
        var sec = MakeImage(parent, name, BgSection);
        var vlg = sec.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.padding = new RectOffset(14, 14, 10, 14);
        vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
        // childControlHeight = true: el VLG de la sección asigna altura a cada
        // fila/label desde su LayoutElement.preferredHeight. La sección reporta
        // su preferredHeight total al Content → la cadena de alturas funciona.
        vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;
        sec.AddComponent<LayoutElement>();

        // ── Fila de título (fondo azul pálido como "header" visual) ──────────
        // Se incluye como primer hijo de la VLG (mismo nivel que el resto de
        // contenido), así la altura se propaga correctamente al padre.
        var hdrGO = MakeImage(sec.transform, name + "_Hdr", BgSectionHdr);
        LE(hdrGO, prefH: 38);
        var hdrHLG = hdrGO.AddComponent<HorizontalLayoutGroup>();
        hdrHLG.spacing = 8; hdrHLG.padding = new RectOffset(8, 8, 8, 8);
        hdrHLG.childControlWidth  = true;  hdrHLG.childForceExpandWidth  = false;
        hdrHLG.childControlHeight = true;  hdrHLG.childForceExpandHeight = true;

        // Barra de acento (4 px, ancho fijo — childControlWidth=true la respeta)
        var bar = MakeImage(hdrGO.transform, "AccentBar", AccentBlue);
        LE(bar, prefW: 4, flexW: 0);

        // Título
        var t = MakeTMP(hdrGO.transform, "TxtTitulo", titleText, 14, FontStyles.Bold, TxtAccent);
        LE(t, flexW: 1, prefH: 22);

        // Devolvemos la sección directamente: los callers añaden hijos a sec.transform
        return sec;
    }

    static void SectionLbl(Transform parent, string text)
    {
        var g = MakeTMP(parent, "Lbl_" + text.GetHashCode(), text, 11, FontStyles.Bold, TxtSub);
        // prefH 34: espacio para 2 líneas de 11pt (para texto largo que haga wrap)
        LE(g, prefH: 34);
    }

    static GameObject HRow(Transform parent, string name, float height)
    {
        var row = new GameObject(name);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        // childControlWidth = true: el HLG asigna anchos desde LayoutElement.
        // Sin esto, prefW y flexW se ignoran → inputs toman tamaño del prefab.
        hlg.childControlWidth     = true;  hlg.childForceExpandWidth  = false;
        hlg.childControlHeight    = true;  hlg.childForceExpandHeight = true;
        LE(row, prefH: height, flexW: 1);
        return row;
    }

    static GameObject BuildScrollSection(Transform parent, string name, float prefH)
    {
        var sv = DefaultControls.CreateScrollView(default);
        sv.name = name;
        sv.transform.SetParent(parent, false);
        sv.GetComponent<Image>().color = C("#E8EDF2");
        sv.GetComponent<ScrollRect>().horizontal = false;
        LE(sv, prefH: prefH, flexW: 1);

        var content = sv.transform.Find("Viewport/Content");
        if (content != null)
        {
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3; vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth  = true;  vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true;  vlg.childForceExpandHeight = false;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return sv;
    }

    // ── Primitivas de UI ──────────────────────────────────────────────────────

    static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeTMP(Transform parent, string name, string text,
        float size, FontStyles style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        // Wrapping ON + Masking: el texto se ajusta al ancho del contenedor y
        // queda RECORTADO dentro de su RectTransform — nunca "se monta" sobre
        // otros elementos (que es lo que pasa con Overflow o sin wrapping).
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Masking;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string text, Color bg, float fontSize = 13)
    {
        var go  = DefaultControls.CreateButton(default);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;

        var oldText = go.GetComponentInChildren<Text>();
        if (oldText != null)
        {
            var lGO = oldText.gameObject;
            Object.DestroyImmediate(oldText);
            var tmp = lGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
        }
        return go;
    }

    static GameObject MakeTMPInput(Transform parent, string name, string defaultText)
    {
        GameObject go;
        if (s_inputFieldPrefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(s_inputFieldPrefab, parent);
            PrefabUtility.UnpackPrefabInstance(go,
                PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            // Fallback: construir TMP_InputField desde cero sin depender del prefab.
            // DefaultControls.CreateInputField crea un InputField LEGACY (sin TMP),
            // lo que hace que GetComponent<TMP_InputField>() devuelva null y las
            // referencias de ConfigPanel queden sin asignar.
            go = BuildTMPInputFromScratch(parent, defaultText);
        }
        go.name = name;

        var img = go.GetComponent<Image>();
        if (img != null) img.color = BgInput;

        var f = go.GetComponent<TMP_InputField>();
        if (f != null)
        {
            f.text      = defaultText;
            f.pointSize = 11;
        }

        // Colorear todos los hijos TextMeshProUGUI independientemente de cómo
        // esté estructurado el prefab (evita depender de f.textComponent que
        // puede ser null si TMP Essentials no está importado correctamente).
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            bool isPlaceholder = t.gameObject.name.ToLower().Contains("placeholder");
            t.color = isPlaceholder ? TxtSub : TxtMain;
        }
        return go;
    }

    /// <summary>
    /// Construye un TMP_InputField funcional manualmente, sin necesitar el prefab
    /// de TMP Essentials. Útil si el usuario no lo importó aún.
    /// </summary>
    static GameObject BuildTMPInputFromScratch(Transform parent, string defaultText)
    {
        var root = new GameObject("__tmpInput");
        root.transform.SetParent(parent, false);
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(160, 30);
        root.AddComponent<Image>().color = BgInput;

        // Text Area con RectMask2D para clip del texto
        var areaGO = new GameObject("Text Area");
        areaGO.transform.SetParent(root.transform, false);
        var areaRT = areaGO.AddComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(6, 2); areaRT.offsetMax = new Vector2(-6, -2);
        areaGO.AddComponent<RectMask2D>();

        // Placeholder
        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(areaGO.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = defaultText; phTMP.fontSize = 11;
        phTMP.color = TxtSub; phTMP.fontStyle = FontStyles.Italic;
        phTMP.enableWordWrapping = false;
        phTMP.overflowMode = TextOverflowModes.Masking;

        // Text (el que muestra lo que escribe el usuario)
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(areaGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.text = string.Empty; textTMP.fontSize = 11;
        textTMP.color = TxtMain;
        textTMP.enableWordWrapping = false;
        textTMP.overflowMode = TextOverflowModes.Masking;

        // TMP_InputField con referencias conectadas.
        // CRÍTICO: textViewport debe apuntar al RectTransform del Text Area.
        // Sin él, TMP_InputField lanza m_TextViewport NullReference al hacer drag.
        var field = root.AddComponent<TMP_InputField>();
        field.textViewport  = areaRT;   // ← la referencia que faltaba
        field.textComponent = textTMP;
        field.placeholder   = phTMP;
        field.text          = defaultText;

        return root;
    }

    static GameObject MakeSlider(Transform parent, string name,
        float min, float max, float val, bool wholeNums)
    {
        var go = DefaultControls.CreateSlider(default);
        go.name = name;
        go.transform.SetParent(parent, false);
        var sl = go.GetComponent<Slider>();
        sl.minValue = min; sl.maxValue = max; sl.value = val; sl.wholeNumbers = wholeNums;
        return go;
    }

    static GameObject MakeToggle(Transform parent, string name,
        string label, ToggleGroup group, bool isOn)
    {
        var go = DefaultControls.CreateToggle(default);
        go.name = name;
        go.transform.SetParent(parent, false);
        var tog = go.GetComponent<Toggle>();
        tog.group = group; tog.isOn = isOn;

        // ── Colores del toggle para tema claro ────────────────────────────────
        // Background = caja blanca con borde gris
        var bgImg = go.transform.Find("Background")?.GetComponent<Image>();
        if (bgImg != null) bgImg.color = Color.white;

        // Checkmark = azul visible sobre fondo blanco
        var chkImg = go.transform.Find("Background/Checkmark")?.GetComponent<Image>();
        if (chkImg != null) chkImg.color = AccentBlue;

        // Colores de transición del Toggle (Normal/Highlighted/Pressed)
        var cb = tog.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = C("#DDEEFF");
        cb.pressedColor     = C("#AACCFF");
        cb.selectedColor    = C("#DDEEFF");
        cb.disabledColor    = C("#CCCCCC");
        tog.colors = cb;

        // ── Label en texto oscuro ─────────────────────────────────────────────
        var old = go.GetComponentInChildren<Text>();
        if (old != null)
        {
            var g = old.gameObject; Object.DestroyImmediate(old);
            var t = g.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 12; t.color = TxtMain;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Masking;
        }
        return go;
    }

    static GameObject MakeTMPDropdown(Transform parent, string name, string[] options)
    {
        GameObject go;
        if (s_dropdownPrefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(s_dropdownPrefab, parent);
            PrefabUtility.UnpackPrefabInstance(go,
                PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            go = DefaultControls.CreateDropdown(default);
            go.transform.SetParent(parent, false);
        }
        go.name = name;

        var dd = go.GetComponent<TMP_Dropdown>();
        if (dd != null)
        {
            // 1) Asignar con API directa (siempre funciona en runtime y edit-mode)
            dd.ClearOptions();
            var optList = new List<TMP_Dropdown.OptionData>();
            foreach (var o in options)
                optList.Add(new TMP_Dropdown.OptionData(o));
            dd.AddOptions(optList);
            dd.value = 0;
            dd.RefreshShownValue();

            // 2) Intentar también via SerializedObject para garantizar
            //    que los datos queden serializados en la escena (edit-mode).
            //    En Unity 6 el path puede variar; si falla, la asignación
            //    directa de arriba ya es suficiente.
            try
            {
                var so      = new SerializedObject(dd);
                var optProp = so.FindProperty("m_Options")
                                ?.FindPropertyRelative("m_Options");
                if (optProp != null)
                {
                    optProp.ClearArray();
                    for (int i = 0; i < options.Length; i++)
                    {
                        optProp.InsertArrayElementAtIndex(i);
                        var elem = optProp.GetArrayElementAtIndex(i);
                        elem.FindPropertyRelative("m_Text").stringValue = options[i];
                    }
                    so.FindProperty("m_Value").intValue = 0;
                    so.ApplyModifiedProperties();
                }
            }
            catch { /* silenciar: la asignación directa ya aplicó los valores */ }

            // 3) Forzar el texto del Label directamente (soluciona el caso donde
            //    Unity muestra "Option A/B/C" del prefab en lugar del valor real).
            var labelTMP = go.GetComponentsInChildren<TextMeshProUGUI>(true)
                             .Length > 0
                           ? System.Array.Find(
                                go.GetComponentsInChildren<TextMeshProUGUI>(true),
                                t => t.gameObject.name == "Label")
                           : null;
            if (labelTMP == null)
                labelTMP = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (labelTMP != null && options.Length > 0)
            {
                labelTMP.text = options[0];
                EditorUtility.SetDirty(labelTMP.gameObject);
            }

            // 4) Marcar el objeto como modificado para que Unity lo guarde en escena
            EditorUtility.SetDirty(dd);
            EditorUtility.SetDirty(go);
        }
        return go;
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    static void LE(GameObject go, float prefH = -1, float prefW = -1,
                                   float flexW = -1, float flexH = -1)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (prefH >= 0) le.preferredHeight = prefH;
        if (prefW >= 0) le.preferredWidth  = prefW;
        if (flexW >= 0) le.flexibleWidth   = flexW;
        if (flexH >= 0) le.flexibleHeight  = flexH;
    }

    static void LE(Transform t, float prefH = -1, float prefW = -1,
                                 float flexW = -1, float flexH = -1)
        => LE(t.gameObject, prefH, prefW, flexW, flexH);

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorTop(GameObject go, float height)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, height);
    }

    static void PinLeft(GameObject go, float x, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0); rt.sizeDelta = new Vector2(w, h);
    }

    // ── Búsqueda recursiva ────────────────────────────────────────────────────

    static GameObject Deep(GameObject root, string name)
    {
        if (root == null || root.name == name) return root;
        foreach (Transform c in root.transform)
        {
            var f = Deep(c.gameObject, name);
            if (f != null) return f;
        }
        return null;
    }

    // ── SerializedObject helper ───────────────────────────────────────────────

    static void Assign<T>(T component, Action<SerializedObject> configure) where T : Component
    {
        if (component == null) { Debug.LogWarning("[SceneSetup] Componente nulo: " + typeof(T).Name); return; }
        var so = new SerializedObject(component);
        configure(so);
        so.ApplyModifiedProperties();
    }

    // ── Color desde hex ───────────────────────────────────────────────────────

    static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
#endif
