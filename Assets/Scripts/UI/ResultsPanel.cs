using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra el panel de resultados del entrenamiento.
///
/// DISEÑO DE VISIBILIDAD:
///   - El GameObject "ResultsPanel" lo activa/desactiva el sistema de tabs.
///   - El GameObject interno "PanelResults" siempre está activo; solo se
///     muestra contenido de placeholder hasta que llegan resultados reales.
///   - Start() re-aplica los últimos resultados recibidos si el panel se
///     activa DESPUÉS de que el entrenamiento terminó (soluciona el bug de
///     tab: antes Hide() borraba los datos al activar el GO por primera vez).
/// </summary>
public class ResultsPanel : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------
    [Header("Panel root")]
    [SerializeField] private GameObject panelResults;

    [Header("Summary labels")]
    [SerializeField] private TextMeshProUGUI txtAccuracy;
    [SerializeField] private TextMeshProUGUI txtArchitecture;
    [SerializeField] private TextMeshProUGUI txtActivation;
    [SerializeField] private TextMeshProUGUI txtEpochs;
    [SerializeField] private TextMeshProUGUI txtParams;

    [Header("Per-class metrics table")]
    [SerializeField] private Transform  tableContainer;
    [SerializeField] private GameObject rowPrefab;

    // Colores alternos de fila
    private static readonly Color RowEven = new Color(0.973f, 0.980f, 0.984f); // #F8FAFB
    private static readonly Color RowOdd  = Color.white;

    // Último resultado recibido — persiste entre activaciones del tab
    private ResultsResponse _lastResults;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Asegurarse de que el panel interior esté visible
        if (panelResults != null) panelResults.SetActive(true);

        if (_lastResults != null)
        {
            // El entrenamiento terminó antes de que el usuario abriera este tab:
            // re-aplicar los datos guardados para que se vean correctamente.
            PopulateData(_lastResults);
        }
        else
        {
            // Mostrar estado vacío con placeholders legibles
            ShowPlaceholder();
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Rellena el panel con los resultados del entrenamiento.
    /// Se puede llamar tanto con el tab activo como inactivo.
    /// </summary>
    public void Show(ResultsResponse results)
    {
        _lastResults = results;

        if (panelResults != null) panelResults.SetActive(true);

        if (results == null) { ShowPlaceholder(); return; }

        PopulateData(results);
    }

    /// <summary>Oculta el panel interior (no es necesario llamarlo normalmente).</summary>
    public void Hide()
    {
        // No ocultar PanelResults — el tab system controla la visibilidad del GO padre.
        // Mantener este método por compatibilidad con referencias antiguas.
    }

    /// <summary>Reinicia el panel a su estado inicial (placeholders).</summary>
    public void Reset()
    {
        _lastResults = null;
        ShowPlaceholder();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void PopulateData(ResultsResponse results)
    {
        // ── Resumen ──────────────────────────────────────────────────────────
        SetText(txtAccuracy,     FormatPercent(results.accuracy));
        SetText(txtArchitecture, "Arquitectura: " + BuildArchString(results.architecture));
        SetText(txtActivation,   "Activación: "   + CapitalizeFirst(results.activation ?? "-"));

        // Épocas: siempre mostrar "usadas / configuradas"
        // Si epochs_configured = 0 (API antigua) solo mostrar el valor
        string epochStr;
        if (results.epochs_configured > 0)
        {
            string suffix = results.early_stopped ? " (!)" : "";
            epochStr = $"Epocas: {results.epochs:N0} / {results.epochs_configured:N0}{suffix}";
        }
        else
            epochStr = $"Épocas: {results.epochs:N0}";
        SetText(txtEpochs, epochStr);
        SetText(txtParams,  $"Parámetros: {results.total_params:N0}");

        // ── Tabla por clase ───────────────────────────────────────────────────
        ClearTable();

        if (results.per_class_metrics == null || tableContainer == null || rowPrefab == null)
            return;

        int rowIndex = 0;
        foreach (PerClassMetric metric in results.per_class_metrics)
        {
            var row = Instantiate(rowPrefab, tableContainer);

            // Color alterno de fila
            var bg = row.GetComponent<Image>();
            if (bg != null) bg.color = (rowIndex % 2 == 0) ? RowEven : RowOdd;

            // Columnas: [0] Clase [1] Total [2] Correctas [3] Precision [4] Recall [5] F1
            var cols = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            void Set(int i, string v) { if (i < cols.Length) cols[i].text = v; }

            Set(0, metric.class_name ?? $"Clase {metric.class_id + 1}");
            Set(1, metric.total.ToString());
            Set(2, metric.correct.ToString());
            Set(3, FormatPercent(metric.precision));
            Set(4, FormatPercent(metric.recall));
            Set(5, FormatPercent(metric.f1));

            rowIndex++;
        }

        // Forzar rebuild del layout para que el scroll funcione desde el primer frame
        if (tableContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void ShowPlaceholder()
    {
        SetText(txtAccuracy,     "—");
        SetText(txtArchitecture, "—");
        SetText(txtActivation,   "—");
        SetText(txtEpochs,       "—");
        SetText(txtParams,       "—");
        ClearTable();
    }

    private void ClearTable()
    {
        if (tableContainer == null) return;
        for (int i = tableContainer.childCount - 1; i >= 0; i--)
            Destroy(tableContainer.GetChild(i).gameObject);
    }

    private static void SetText(TextMeshProUGUI lbl, string val)
    {
        if (lbl != null) lbl.text = val ?? "-";
    }

    private static string FormatPercent(float v) => $"{v:F2} %";

    private static string BuildArchString(int[] layers)
    {
        if (layers == null || layers.Length == 0) return "-";
        return string.Join(" \u2192 ", layers);   // →
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
}
