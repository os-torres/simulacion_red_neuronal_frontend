using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// ClassifyPanel.cs
// Panel de clasificación de un punto 3D (x1, x2, x3).
// Envía el punto a la API y muestra la clase predicha, la confianza y la
// distribución de probabilidades por clase.
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class ClassifyPanel : MonoBehaviour
{
    // ── Referencias Inspector ─────────────────────────────────────────────────

    [Header("Entradas")]
    [SerializeField] private TMP_InputField inputX1;
    [SerializeField] private TMP_InputField inputX2;
    [SerializeField] private TMP_InputField inputX3;

    [Header("Resultados")]
    [SerializeField] private TextMeshProUGUI txtResult;
    [SerializeField] private TextMeshProUGUI txtProbabilities;
    [SerializeField] private Image           imgClassColor;

    // ── Paleta de colores por clase ───────────────────────────────────────────

    private static readonly string[] ClassHexColors =
    {
        "#E74C3C",   // Clase 0 — rojo
        "#3498DB",   // Clase 1 — azul
        "#2ECC71",   // Clase 2 — verde
        "#F39C12",   // Clase 3 — naranja
        "#9B59B6",   // Clase 4 — violeta
        "#1ABC9C",   // Clase 5 — turquesa
        "#E67E22",   // Clase 6 — naranja oscuro
        "#34495E",   // Clase 7 — gris azulado
    };

    // =========================================================================
    // API pública
    // =========================================================================

    /// <summary>
    /// Valida los inputs, envía el punto a la API y actualiza la UI con el
    /// resultado. Llamado desde el botón "Clasificar" o desde AppController.
    /// </summary>
    public void OnClassifyClicked()
    {
        if (!TryParseInputs(out float x1, out float x2, out float x3))
        {
            ShowError("Introduce valores numéricos válidos en X1, X2 y X3.");
            return;
        }

        ClearResults();

        var request = new ClassifyRequest { x1 = x1, x2 = x2, x3 = x3 };

        APIManager.Instance.Classify(request, (response, error) =>
        {
            if (error != null)
            {
                ShowError("Error al clasificar: " + error);
                return;
            }

            if (response == null)
            {
                ShowError("La API no devolvió un resultado válido.");
                return;
            }

            DisplayResult(response);
        });
    }

    // =========================================================================
    // Helpers privados — parsing
    // =========================================================================

    private bool TryParseInputs(out float x1, out float x2, out float x3)
    {
        x1 = x2 = x3 = 0f;

        bool ok = TryParseField(inputX1, out x1)
               && TryParseField(inputX2, out x2)
               && TryParseField(inputX3, out x3);

        return ok;
    }

    private static bool TryParseField(TMP_InputField field, out float value)
    {
        value = 0f;
        if (field == null || string.IsNullOrWhiteSpace(field.text))
            return false;

        // Acepta '.' y ',' como separador decimal.
        string text = field.text.Replace(',', '.');
        return float.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    // =========================================================================
    // Helpers privados — UI
    // =========================================================================

    private void DisplayResult(ClassifyResponse response)
    {
        // Texto principal.
        // La API ya devuelve confidence como porcentaje (0-100).
        if (txtResult != null)
        {
            txtResult.text = string.Format(
                "CLASE {0} — {1:F2}% confianza",
                response.class_id,
                response.confidence);
        }

        // Color de la clase predicha.
        if (imgClassColor != null)
            imgClassColor.color = GetClassColor(response.class_id);

        // Lista de probabilidades.
        if (txtProbabilities != null)
        {
            if (response.probabilities != null && response.probabilities.Length > 0)
            {
                var sb = new StringBuilder();
                // La API ya devuelve probabilities como porcentajes (0-100).
                for (int i = 0; i < response.probabilities.Length; i++)
                {
                    sb.AppendFormat("Clase {0}: {1:F2}%", i, response.probabilities[i]);
                    if (i < response.probabilities.Length - 1)
                        sb.AppendLine();
                }
                txtProbabilities.text = sb.ToString();
            }
            else
            {
                txtProbabilities.text = string.Empty;
            }
        }
    }

    private void ShowError(string message)
    {
        if (txtResult != null)
            txtResult.text = message;

        if (txtProbabilities != null)
            txtProbabilities.text = string.Empty;

        if (imgClassColor != null)
            imgClassColor.color = Color.gray;
    }

    private void ClearResults()
    {
        if (txtResult        != null) txtResult.text        = "Clasificando...";
        if (txtProbabilities != null) txtProbabilities.text = string.Empty;
        if (imgClassColor    != null) imgClassColor.color   = Color.gray;
    }

    // =========================================================================
    // Color helpers
    // =========================================================================

    /// <summary>
    /// Devuelve el Color Unity correspondiente al <paramref name="classId"/>.
    /// Cicla la paleta si hay más clases que colores definidos.
    /// </summary>
    private static Color GetClassColor(int classId)
    {
        int idx = classId % ClassHexColors.Length;
        return ColorFromHex(ClassHexColors[idx]);
    }

    /// <summary>
    /// Convierte un string hexadecimal (#RRGGBB o #RRGGBBAA) en un Color de Unity.
    /// </summary>
    public static Color ColorFromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        Debug.LogWarning($"[ClassifyPanel] Color hex inválido: {hex}");
        return Color.white;
    }
}
