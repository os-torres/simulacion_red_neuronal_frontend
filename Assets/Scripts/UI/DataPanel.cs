using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra una tabla scrollable con todos los puntos de datos generados.
/// Columnas: X1 | X2 | X3 | CLASE
/// Cada fila se colorea sutilmente según la clase del punto.
/// </summary>
public class DataPanel : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------
    [Header("Panel root")]
    [SerializeField] private GameObject panelData;

    [Header("Status label")]
    [SerializeField] private TextMeshProUGUI txtStatus;

    [Header("Per-point table")]
    [SerializeField] private Transform  tableContainer;
    [SerializeField] private GameObject rowPrefab;

    // Colores de clase — mismos tonos que el Visualizer3D para coherencia visual
    private static readonly Color[] ClassColors =
    {
        C("#E74C3C"), C("#3498DB"), C("#2ECC71"), C("#F39C12"),
        C("#9B59B6"), C("#1ABC9C"), C("#E67E22"), C("#34495E"),
        C("#EC407A"), C("#00BCD4"),
    };

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        if (panelData != null) panelData.SetActive(true);
        SetStatus("Genera datos y haz clic en DATOS para cargar la tabla.");
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Rellena la tabla con los puntos recibidos de la API.</summary>
    public void LoadData(DataPointsResponse response)
    {
        if (panelData != null) panelData.SetActive(true);

        if (response == null || response.points == null || response.points.Length == 0)
        {
            SetStatus("Sin datos disponibles. Genera los datos primero.");
            ClearTable();
            return;
        }

        SetStatus($"{response.total} puntos cargados.");
        ClearTable();

        if (tableContainer == null || rowPrefab == null) return;

        for (int i = 0; i < response.points.Length; i++)
        {
            var pt  = response.points[i];
            var row = Instantiate(rowPrefab, tableContainer);

            // Color de fondo: tono de clase con alfa bajo, alterno para legibilidad
            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                int   cls       = pt.class_id;
                Color baseColor = cls >= 0 && cls < ClassColors.Length
                                  ? ClassColors[cls]
                                  : Color.gray;
                float alpha     = (i % 2 == 0) ? 0.18f : 0.08f;
                bg.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            // Columnas: [0] X1  [1] X2  [2] X3  [3] Clase
            var cols = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            void Set(int idx, string v) { if (idx < cols.Length) cols[idx].text = v; }

            Set(0, $"{pt.x:F3}");
            Set(1, $"{pt.y:F3}");
            Set(2, $"{pt.z:F3}");
            Set(3, $"Clase {pt.class_id + 1}");
        }

        // Forzar rebuild del layout para que el scroll funcione desde el primer frame
        if (tableContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    /// <summary>Muestra un mensaje de "cargando..." y limpia la tabla.</summary>
    public void ShowLoading()
    {
        SetStatus("Cargando datos...");
        ClearTable();
    }

    /// <summary>Muestra un mensaje de error.</summary>
    public void ShowError(string message)
    {
        SetStatus($"Error: {message}");
    }

    /// <summary>Reinicia el panel a su estado inicial.</summary>
    public void Reset()
    {
        ClearTable();
        SetStatus("Genera datos y haz clic en DATOS para cargar la tabla.");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void SetStatus(string msg)
    {
        if (txtStatus != null) txtStatus.text = msg ?? "";
    }

    private void ClearTable()
    {
        if (tableContainer == null) return;
        for (int i = tableContainer.childCount - 1; i >= 0; i--)
            Destroy(tableContainer.GetChild(i).gameObject);
    }

    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
