using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// StatusBar.cs
// Panel de estado superior. Muestra el estado de la conexión, el progreso
// del entrenamiento y mensajes de error con código de color.
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class StatusBar : MonoBehaviour
{
    // ── Referencias Inspector ─────────────────────────────────────────────────

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtDetails;

    [Header("Indicador visual")]
    [SerializeField] private Image  imgIndicator;
    [SerializeField] private Slider progressBar;

    // ── Colores de estado ─────────────────────────────────────────────────────

    private static readonly Color ColorRed    = HexToColor("#E74C3C");
    private static readonly Color ColorYellow = HexToColor("#F39C12");
    private static readonly Color ColorGreen  = HexToColor("#2ECC71");

    // =========================================================================
    // API pública
    // =========================================================================

    /// <summary>Estado: intentando conectar con la API.</summary>
    public void SetConnecting()
    {
        SetIndicator(ColorYellow);
        SetText("CONECTANDO...", "Esperando respuesta de la API en localhost:8000");
        SetProgress(0f, false);
    }

    /// <summary>Estado: API disponible y en reposo.</summary>
    public void SetConnected()
    {
        SetIndicator(ColorGreen);
        SetText("CONECTADO", "API disponible — listo para operar.");
        SetProgress(0f, false);
    }

    /// <summary>Estado: generando dataset.</summary>
    public void SetGenerating()
    {
        SetIndicator(ColorYellow);
        SetText("GENERANDO DATOS...", "Creando el dataset según la configuración.");
        SetProgress(0f, true);
    }

    /// <summary>
    /// Estado: entrenamiento en curso.
    /// Muestra época, pérdida y precisión actuales.
    /// </summary>
    public void SetTraining(StatusResponse status)
    {
        if (status == null) return;

        SetIndicator(ColorYellow);

        string main = string.Format(
            "ENTRENANDO... ÉPOCA {0}/{1} | PÉRDIDA: {2:F4} | PRECISIÓN: {3:F1}%",
            status.epoch,
            status.total_epochs,
            status.loss,
            status.accuracy_pct);

        string detail = status.message ?? string.Empty;

        SetText(main, detail);
        SetProgress(status.progress, true);
    }

    /// <summary>
    /// Estado: entrenamiento finalizado.
    /// Muestra precisión final y número de épocas.
    /// </summary>
    public void SetDone(StatusResponse status)
    {
        if (status == null) return;

        SetIndicator(ColorGreen);

        string main = string.Format(
            "ENTRENAMIENTO FINALIZADO | PRECISION: {0:F2}% | EPOCAS: {1}",
            status.accuracy_pct,
            status.total_epochs);

        SetText(main, status.message ?? "El modelo está listo para clasificar.");
        SetProgress(1f, true);
    }

    /// <summary>Estado: error. Muestra el mensaje en rojo.</summary>
    public void SetError(string message)
    {
        SetIndicator(ColorRed);
        SetText("ERROR", message ?? "Se produjo un error desconocido.");
        SetProgress(0f, false);
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private void SetText(string main, string detail)
    {
        if (txtStatus  != null) txtStatus.text  = main;
        if (txtDetails != null) txtDetails.text = detail;
    }

    private void SetIndicator(Color color)
    {
        if (imgIndicator != null)
            imgIndicator.color = color;
    }

    private void SetProgress(float value, bool visible)
    {
        if (progressBar == null) return;

        progressBar.gameObject.SetActive(visible);

        // value debe estar en [0, 1].
        progressBar.value = Mathf.Clamp01(value);
    }

    /// <summary>Convierte un color hexadecimal (#RRGGBB o #RRGGBBAA) a Color.</summary>
    private static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        Debug.LogWarning($"[StatusBar] Color hex inválido: {hex}");
        return Color.white;
    }
}
