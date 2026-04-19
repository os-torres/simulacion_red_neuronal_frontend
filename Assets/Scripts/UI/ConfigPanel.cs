using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// ConfigPanel.cs
// Panel izquierdo de configuración. Cubre TODAS las opciones del simulador
// Python: instancias iguales/diferentes, separabilidad global/por clase,
// semilla, capas, activación e hiperparámetros.
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class ConfigPanel : MonoBehaviour
{
    // =========================================================================
    // Referencias Inspector
    // =========================================================================

    // ── Clases ───────────────────────────────────────────────────────────────
    [Header("Clases")]
    [SerializeField] private Slider         sliderClases;
    [SerializeField] private TMP_InputField inputClases;

    // ── Instancias ────────────────────────────────────────────────────────────
    [Header("Instancias")]
    [SerializeField] private Toggle         toggleInstIguales;   // ON = misma cantidad
    [SerializeField] private Toggle         toggleInstDiferentes;// ON = distinta por clase
    [SerializeField] private GameObject     panelInstIguales;    // contiene inputInstIguales
    [SerializeField] private TMP_InputField inputInstIguales;
    [SerializeField] private GameObject     panelInstDiferentes; // contiene el scroll
    [SerializeField] private Transform      containerInstDifer;  // padre dinámico (ScrollRect content)
    [SerializeField] private GameObject     prefabInstRow;       // prefab: Label + InputField

    // ── Separabilidad ─────────────────────────────────────────────────────────
    [Header("Separabilidad")]
    [SerializeField] private Toggle         toggleSepAuto;
    [SerializeField] private Toggle         toggleSepManual;
    [SerializeField] private GameObject     panelSepManual;      // visible cuando manual

    [SerializeField] private Toggle         toggleSepGlobal;     // dentro de panelSepManual
    [SerializeField] private Toggle         toggleSepPorClase;
    [SerializeField] private GameObject     panelSepGlobal;
    [SerializeField] private Slider         sliderMeanSep;
    [SerializeField] private TMP_InputField inputMeanSep;
    [SerializeField] private Slider         sliderStdDev;
    [SerializeField] private TMP_InputField inputStdDev;

    [SerializeField] private GameObject     panelSepPorClase;    // visible cuando por clase
    [SerializeField] private Transform      containerSepClases;  // padre dinámico
    [SerializeField] private GameObject     prefabSepRow;        // prefab: Label + SliderMean + SliderStd

    // ── Semilla ───────────────────────────────────────────────────────────────
    [Header("Semilla")]
    [SerializeField] private TMP_InputField inputSeed;

    // ── Red Neuronal: capas ───────────────────────────────────────────────────
    [Header("Capas")]
    [SerializeField] private Toggle         toggleCapasAuto;
    [SerializeField] private Toggle         toggleCapasManual;
    [SerializeField] private GameObject     panelCapasManual;
    [SerializeField] private TMP_InputField inputCapasManuales;  // ej. "16,8"

    // ── Red Neuronal: activación ──────────────────────────────────────────────
    [Header("Activación")]
    [SerializeField] private Toggle         toggleActAuto;
    [SerializeField] private Toggle         toggleActManual;
    [SerializeField] private GameObject     panelActManual;
    [SerializeField] private TMP_Dropdown   dropdownActivacion;  // 0=ReLU,1=Sigmoid,2=Tanh

    // ── Hiperparámetros ───────────────────────────────────────────────────────
    [Header("Hiperparámetros")]
    [SerializeField] private TMP_InputField inputLearningRate;
    [SerializeField] private TMP_InputField inputEpochs;
    [SerializeField] private TMP_InputField inputMomentum;
    [SerializeField] private TMP_InputField inputRecordEvery;

    // =========================================================================
    // Defaults
    // =========================================================================
    private const int    DefaultClases        = 3;
    private const int    DefaultInst          = 100;
    private const float  DefaultMeanSep       = 2.5f;
    private const float  DefaultStdDev        = 0.6f;
    private const int    DefaultSeed          = 42;
    private const string DefaultCapasManuales = "16,8";
    private const float  DefaultLR            = 0.01f;
    private const int    DefaultEpochs        = 1000;
    private const float  DefaultMomentum      = 0.9f;
    private const int    DefaultRecordEvery   = 10;

    private static readonly string[] ActivationNames = { "relu", "sigmoid", "tanh" };

    // ── Filas dinámicas generadas en runtime ──────────────────────────────────
    private readonly List<TMP_InputField> _instDiferFields = new();
    private readonly List<Slider>         _meanSepSliders  = new();
    private readonly List<Slider>         _stdDevSliders   = new();

    // ── Scroll anti-reset ─────────────────────────────────────────────────────
    // Cuando SetActive() cambia el tamaño del Content, el ScrollRect salta al
    // inicio. Guardamos la posición y la restauramos al final del frame.
    // IMPORTANTE: usamos StopCoroutine porque los ToggleGroup disparan DOS eventos
    // en el mismo frame (off + on), creando dos corrutinas que compiten entre sí.
    private ScrollRect _parentScroll;
    private Coroutine  _scrollRestore;

    // =========================================================================
    // Unity lifecycle
    // =========================================================================
    private void Start()
    {
        // Buscar el ScrollRect padre (LeftScroll) para preservar posición
        _parentScroll = GetComponentInParent<ScrollRect>();

        ApplyDefaults();
        RegisterCallbacks();
        RefreshDynamicPanels();
    }

    // =========================================================================
    // API pública — construir requests
    // =========================================================================

    /// <summary>Construye el GenerateRequest con todas las opciones.</summary>
    public GenerateRequest BuildGenerateRequest()
    {
        try
        {
            int n = ReadClases();

            bool instIguales = toggleInstIguales == null || toggleInstIguales.isOn;
            bool sepAuto     = toggleSepAuto     != null && toggleSepAuto.isOn;
            bool sepGlobal   = toggleSepGlobal   == null || toggleSepGlobal.isOn;

            var req = new GenerateRequest
            {
                n_classes       = n,
                equal_instances = instIguales,
                n_instances     = ReadInt(inputInstIguales, DefaultInst),
                sep_auto        = sepAuto,
                sep_mode        = sepGlobal ? "global" : "perclass",
                mean_sep        = ReadFloat(inputMeanSep, DefaultMeanSep),
                std_dev         = ReadFloat(inputStdDev,  DefaultStdDev),
                seed            = ReadInt(inputSeed, DefaultSeed),
            };

            // Instancias diferentes por clase
            if (!instIguales && _instDiferFields.Count >= n)
            {
                req.instances_per_class = new int[n];
                for (int i = 0; i < n; i++)
                    req.instances_per_class[i] = ReadInt(_instDiferFields[i], DefaultInst);
            }

            // Separabilidad por clase
            if (!sepAuto && !sepGlobal
                && _meanSepSliders.Count >= n
                && _stdDevSliders.Count  >= n)
            {
                req.mean_sep_per_class = new float[n];
                req.std_dev_per_class  = new float[n];
                for (int i = 0; i < n; i++)
                {
                    req.mean_sep_per_class[i] = _meanSepSliders[i] != null
                        ? _meanSepSliders[i].value : DefaultMeanSep;
                    req.std_dev_per_class[i]  = _stdDevSliders[i] != null
                        ? _stdDevSliders[i].value : DefaultStdDev;
                }
            }

            // Clamp de seguridad
            req.n_classes   = Mathf.Clamp(req.n_classes,   2, 10);
            req.n_instances = Mathf.Max(req.n_instances, 1);
            req.mean_sep    = Mathf.Clamp(req.mean_sep,  0.5f, 6f);
            req.std_dev     = Mathf.Clamp(req.std_dev,   0.1f, 2.5f);

            return req;
        }
        catch (Exception ex)
        {
            Debug.LogError("[ConfigPanel] BuildGenerateRequest: " + ex.Message);
            return null;
        }
    }

    /// <summary>Construye el TrainRequest con todas las opciones.</summary>
    public TrainRequest BuildTrainRequest()
    {
        try
        {
            bool   capasAuto  = toggleCapasAuto  == null || toggleCapasAuto.isOn;
            bool   actAuto    = toggleActAuto    == null || toggleActAuto.isOn;
            int[]  layers     = capasAuto ? Array.Empty<int>() : ParseLayers(inputCapasManuales);
            string activation = actAuto   ? "relu"           : ReadActivation();

            var req = new TrainRequest
            {
                layers_auto   = capasAuto,
                hidden_layers = layers,
                act_auto      = actAuto,
                activation    = activation,
                learning_rate = ReadFloat(inputLearningRate, DefaultLR),
                epochs        = ReadInt(inputEpochs,         DefaultEpochs),
                momentum      = ReadFloat(inputMomentum,     DefaultMomentum),
                record_every  = ReadInt(inputRecordEvery,    DefaultRecordEvery),
            };

            req.learning_rate = Mathf.Clamp(req.learning_rate, 1e-6f, 1f);
            req.epochs        = Mathf.Max(req.epochs, 1);
            req.momentum      = Mathf.Clamp(req.momentum, 0f, 1f);
            req.record_every  = Mathf.Max(req.record_every, 1);

            return req;
        }
        catch (Exception ex)
        {
            Debug.LogError("[ConfigPanel] BuildTrainRequest: " + ex.Message);
            return null;
        }
    }

    // =========================================================================
    // Inicialización
    // =========================================================================
    private void ApplyDefaults()
    {
        // Clases
        if (sliderClases != null)
        {
            sliderClases.minValue     = 2;
            sliderClases.maxValue     = 10;
            sliderClases.wholeNumbers = true;
            sliderClases.value        = DefaultClases;
        }
        SetText(inputClases, DefaultClases.ToString());

        // Instancias
        if (toggleInstIguales   != null) toggleInstIguales.isOn    = true;
        if (toggleInstDiferentes!= null) toggleInstDiferentes.isOn = false;
        SetText(inputInstIguales, DefaultInst.ToString());

        // Separabilidad
        if (toggleSepAuto   != null) toggleSepAuto.isOn   = false;
        if (toggleSepManual != null) toggleSepManual.isOn = true;
        if (toggleSepGlobal != null) toggleSepGlobal.isOn = true;
        if (toggleSepPorClase != null) toggleSepPorClase.isOn = false;

        if (sliderMeanSep != null)
        {
            sliderMeanSep.minValue = 0.5f;
            sliderMeanSep.maxValue = 6f;
            sliderMeanSep.value    = DefaultMeanSep;
        }
        SetText(inputMeanSep, DefaultMeanSep.ToString("F2"));

        if (sliderStdDev != null)
        {
            sliderStdDev.minValue = 0.1f;
            sliderStdDev.maxValue = 2.5f;
            sliderStdDev.value    = DefaultStdDev;
        }
        SetText(inputStdDev, DefaultStdDev.ToString("F2"));

        // Semilla
        SetText(inputSeed, DefaultSeed.ToString());

        // Red neuronal
        if (toggleCapasAuto   != null) toggleCapasAuto.isOn   = true;
        if (toggleCapasManual != null) toggleCapasManual.isOn = false;
        SetText(inputCapasManuales, DefaultCapasManuales);

        if (toggleActAuto   != null) toggleActAuto.isOn   = true;
        if (toggleActManual != null) toggleActManual.isOn = false;
        if (dropdownActivacion != null) dropdownActivacion.value = 0;

        // Hiperparámetros
        SetText(inputLearningRate, DefaultLR.ToString("F4"));
        SetText(inputEpochs,       DefaultEpochs.ToString());
        SetText(inputMomentum,     DefaultMomentum.ToString("F2"));
        SetText(inputRecordEvery,  DefaultRecordEvery.ToString());
    }

    private void RegisterCallbacks()
    {
        // Slider clases ↔ input
        sliderClases?.onValueChanged.AddListener(v => {
            SetText(inputClases, ((int)v).ToString());
            RefreshDynamicPanels();
        });
        inputClases?.onEndEdit.AddListener(v => {
            if (int.TryParse(v, out int n) && sliderClases != null)
                sliderClases.value = Mathf.Clamp(n, 2, 10);
            RefreshDynamicPanels();
        });

        // Slider MeanSep ↔ input
        sliderMeanSep?.onValueChanged.AddListener(v => SetText(inputMeanSep, v.ToString("F2")));
        inputMeanSep?.onEndEdit.AddListener(v => {
            if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) && sliderMeanSep != null)
                sliderMeanSep.value = Mathf.Clamp(f, 0.5f, 6f);
        });

        // Slider StdDev ↔ input
        sliderStdDev?.onValueChanged.AddListener(v => SetText(inputStdDev, v.ToString("F2")));
        inputStdDev?.onEndEdit.AddListener(v => {
            if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) && sliderStdDev != null)
                sliderStdDev.value = Mathf.Clamp(f, 0.1f, 2.5f);
        });

        // Toggles instancias
        toggleInstIguales?.onValueChanged.AddListener(on => {
            if (on) RefreshDynamicPanels();
        });
        toggleInstDiferentes?.onValueChanged.AddListener(on => {
            if (on) RefreshDynamicPanels();
        });

        // Toggles separabilidad — solo cuando se ACTIVA (on=true).
        // Si se escuchara también el off, el ToggleGroup dispararía 2 eventos por
        // clic (off del anterior + on del nuevo) → 2 corrutinas compiten → el
        // scroll salta. Escuchar solo "on" garantiza un solo RefreshDynamicPanels.
        toggleSepAuto?.onValueChanged.AddListener(on     => { if (on) RefreshDynamicPanels(); });
        toggleSepManual?.onValueChanged.AddListener(on   => { if (on) RefreshDynamicPanels(); });
        toggleSepGlobal?.onValueChanged.AddListener(on   => { if (on) RefreshDynamicPanels(); });
        toggleSepPorClase?.onValueChanged.AddListener(on => { if (on) RefreshDynamicPanels(); });

        // Toggles capas y activación — idem
        toggleCapasAuto?.onValueChanged.AddListener(on   => { if (on) RefreshDynamicPanels(); });
        toggleCapasManual?.onValueChanged.AddListener(on => { if (on) RefreshDynamicPanels(); });
        toggleActAuto?.onValueChanged.AddListener(on     => { if (on) RefreshDynamicPanels(); });
        toggleActManual?.onValueChanged.AddListener(on   => { if (on) RefreshDynamicPanels(); });
    }

    // =========================================================================
    // UI dinámica — reconstruye filas por clase cuando cambia n_clases
    // =========================================================================
    private void RefreshDynamicPanels()
    {
        // Guardar posición del scroll ANTES de cualquier cambio de layout
        float savedScroll = _parentScroll != null
            ? _parentScroll.verticalNormalizedPosition
            : 1f;

        int n = ReadClases();

        bool instIguales = toggleInstIguales  == null || toggleInstIguales.isOn;
        bool sepAuto     = toggleSepAuto      != null  && toggleSepAuto.isOn;
        bool sepGlobal   = toggleSepGlobal    == null  || toggleSepGlobal.isOn;
        bool capasAuto   = toggleCapasAuto    == null  || toggleCapasAuto.isOn;
        bool actAuto     = toggleActAuto      == null  || toggleActAuto.isOn;

        // Paneles instancias
        panelInstIguales?.SetActive(instIguales);
        panelInstDiferentes?.SetActive(!instIguales);

        // Paneles separabilidad
        panelSepManual?.SetActive(!sepAuto);
        panelSepGlobal?.SetActive(!sepAuto && sepGlobal);
        panelSepPorClase?.SetActive(!sepAuto && !sepGlobal);

        // Paneles red neuronal
        panelCapasManual?.SetActive(!capasAuto);
        panelActManual?.SetActive(!actAuto);

        // Reconstruir filas de instancias diferentes
        if (!instIguales && containerInstDifer != null && prefabInstRow != null)
            RebuildInstRows(n);

        // Reconstruir filas de separabilidad por clase
        if (!sepAuto && !sepGlobal && containerSepClases != null && prefabSepRow != null)
            RebuildSepRows(n);

        // Restaurar scroll: cancelar cualquier restauración pendiente antes de
        // iniciar una nueva. Así evitamos que dos eventos del mismo ToggleGroup
        // creen dos corrutinas que compiten (off-event guarda posición correcta,
        // on-event podría guardar ya la posición reseteada si llegara tarde).
        if (_parentScroll != null)
        {
            if (_scrollRestore != null) StopCoroutine(_scrollRestore);
            _scrollRestore = StartCoroutine(RestoreScroll(savedScroll));
        }
    }

    private IEnumerator RestoreScroll(float pos)
    {
        // Frame N: SetActive() → layout marcado como dirty.
        // El Canvas rebuild (ContentSizeFitter) ocurre durante el render de frame N.
        // yield return null → esperamos al INICIO del frame N+1 (post-rebuild).
        // Un segundo yield garantiza que cualquier segunda pasada de layout también
        // haya terminado antes de que forcemos la posición del scroll.
        yield return null;
        yield return null;
        if (_parentScroll != null)
            _parentScroll.verticalNormalizedPosition = pos;
        _scrollRestore = null;
    }

    private void RebuildInstRows(int n)
    {
        // Guardar los valores que el usuario ya ingresó antes de destruir las filas.
        // Solo así los datos sobreviven cambios de modo (iguales ↔ diferentes,
        // separabilidad auto ↔ manual, etc.).
        var savedValues = new List<string>();
        foreach (var f in _instDiferFields)
            savedValues.Add(f != null ? f.text : DefaultInst.ToString());

        // Destruir filas previas
        foreach (Transform child in containerInstDifer) Destroy(child.gameObject);
        _instDiferFields.Clear();

        for (int i = 0; i < n; i++)
        {
            GameObject row = Instantiate(prefabInstRow, containerInstDifer);
            row.name = $"InstRow_{i}";

            // Buscar Label y InputField dentro del prefab
            var labels = row.GetComponentsInChildren<TMP_Text>();
            var inputs = row.GetComponentsInChildren<TMP_InputField>();

            if (labels.Length > 0) labels[0].text = $"Clase {i + 1}:";
            TMP_InputField field = inputs.Length > 0 ? inputs[0] : null;
            if (field != null)
            {
                // Si el usuario ya tenía un valor para esta fila, restaurarlo.
                // Para filas nuevas (más clases que antes) usar el valor por defecto.
                field.text = i < savedValues.Count ? savedValues[i] : DefaultInst.ToString();
            }

            _instDiferFields.Add(field);
        }
    }

    private void RebuildSepRows(int n)
    {
        // Guardar los valores actuales de los sliders antes de destruir las filas.
        var savedMean = new List<float>();
        var savedStd  = new List<float>();
        foreach (var s in _meanSepSliders)
            savedMean.Add(s != null ? s.value : DefaultMeanSep);
        foreach (var s in _stdDevSliders)
            savedStd.Add(s != null ? s.value : DefaultStdDev);

        foreach (Transform child in containerSepClases) Destroy(child.gameObject);
        _meanSepSliders.Clear();
        _stdDevSliders.Clear();

        for (int i = 0; i < n; i++)
        {
            GameObject row = Instantiate(prefabSepRow, containerSepClases);
            row.name = $"SepRow_{i}";

            var labels  = row.GetComponentsInChildren<TMP_Text>();
            var sliders = row.GetComponentsInChildren<Slider>();

            if (labels.Length > 0)  labels[0].text = $"Clase {i + 1}";

            // Slider [0] = Mean Sep,  Slider [1] = Std Dev
            Slider meanSl = sliders.Length > 0 ? sliders[0] : null;
            Slider stdSl  = sliders.Length > 1 ? sliders[1] : null;

            if (meanSl != null)
            {
                meanSl.minValue = 0.5f; meanSl.maxValue = 6f;
                // Restaurar valor previo o usar defecto para filas nuevas
                meanSl.value = i < savedMean.Count ? savedMean[i] : DefaultMeanSep;
            }
            if (stdSl != null)
            {
                stdSl.minValue = 0.1f; stdSl.maxValue = 2.5f;
                stdSl.value    = i < savedStd.Count ? savedStd[i] : DefaultStdDev;
            }

            _meanSepSliders.Add(meanSl);
            _stdDevSliders.Add(stdSl);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    private int ReadClases()
    {
        // Sincronizar input → slider ANTES de leer.
        // Si el usuario escribió en el campo sin presionar Enter/Tab y luego hizo
        // clic directo en GENERAR, el onEndEdit nunca disparó y el slider sigue
        // en su valor anterior. Esta sincronización garantiza que siempre se lea
        // lo que hay en el campo de texto en ese momento.
        if (inputClases != null && sliderClases != null &&
            int.TryParse(inputClases.text, out int parsedN))
        {
            sliderClases.value = Mathf.Clamp(parsedN, 2, 10);
        }
        if (sliderClases != null) return Mathf.RoundToInt(sliderClases.value);
        return ReadInt(inputClases, DefaultClases);
    }

    private string ReadActivation()
    {
        if (dropdownActivacion == null) return ActivationNames[0];
        int idx = Mathf.Clamp(dropdownActivacion.value, 0, ActivationNames.Length - 1);
        return ActivationNames[idx];
    }

    private static int[] ParseLayers(TMP_InputField field)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.text)) return Array.Empty<int>();
        var result = new List<int>();
        foreach (string part in field.text.Split(','))
        {
            string t = part.Trim();
            if (int.TryParse(t, out int n) && n > 0) result.Add(n);
            else Debug.LogWarning($"[ConfigPanel] Capa ignorada: '{t}'");
        }
        return result.ToArray();
    }

    private static float ReadFloat(TMP_InputField f, float fallback)
    {
        if (f == null || string.IsNullOrWhiteSpace(f.text)) return fallback;
        return float.TryParse(f.text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float v) ? v : fallback;
    }

    private static int ReadInt(TMP_InputField f, int fallback)
    {
        if (f == null || string.IsNullOrWhiteSpace(f.text)) return fallback;
        return int.TryParse(f.text, out int v) ? v : fallback;
    }

    private static void SetText(TMP_InputField f, string text)
    {
        if (f != null) f.text = text;
    }
}
