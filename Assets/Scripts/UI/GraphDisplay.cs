using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// GraphDisplay.cs
// Muestra gráficas PNG descargadas desde la API como texturas en un RawImage.
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class GraphDisplay : MonoBehaviour
{
    // ── Referencias Inspector ─────────────────────────────────────────────────

    [Header("Componentes UI")]
    [SerializeField] private RawImage        imgGraph;
    [SerializeField] private GameObject      panelGraphs;
    [SerializeField] private TextMeshProUGUI txtGraphTitle;

    // ── Estado ────────────────────────────────────────────────────────────────

    private string _currentPlotType;

    // =========================================================================
    // API pública
    // =========================================================================

    /// <summary>
    /// Muestra el panel, descarga la imagen <paramref name="plotType"/> desde la API
    /// y la asigna al RawImage. Actualiza el título con <paramref name="title"/>.
    /// </summary>
    /// <param name="plotType">Tipo de gráfica: "error", "weights" o "confusion".</param>
    /// <param name="title">Título visible en la UI.</param>
    public void ShowPlot(string plotType, string title)
    {
        if (string.IsNullOrEmpty(plotType))
        {
            Debug.LogWarning("[GraphDisplay] plotType no puede ser nulo o vacío.");
            return;
        }

        _currentPlotType = plotType;

        // Muestra el panel mientras carga.
        Show();

        // Limpia la imagen anterior para evitar mostrar contenido desactualizado.
        if (imgGraph != null)
            imgGraph.texture = null;

        // Actualiza el título.
        if (txtGraphTitle != null)
            txtGraphTitle.text = title ?? plotType;

        // Solicita la textura a la API.
        APIManager.Instance.GetPlotTexture(plotType, OnTextureReceived);
    }

    /// <summary>Oculta el panel de gráficas.</summary>
    public void Hide()
    {
        if (panelGraphs != null)
            panelGraphs.SetActive(false);
    }

    // =========================================================================
    // Callbacks privados
    // =========================================================================

    private void OnTextureReceived(Texture2D texture, string error)
    {
        if (error != null)
        {
            Debug.LogWarning($"[GraphDisplay] Error descargando gráfica '{_currentPlotType}': {error}");
            Hide();
            return;
        }

        if (texture == null)
        {
            Debug.LogWarning($"[GraphDisplay] Textura nula para '{_currentPlotType}'.");
            Hide();
            return;
        }

        if (imgGraph == null)
        {
            Debug.LogWarning("[GraphDisplay] imgGraph no está asignado en el Inspector.");
            return;
        }

        // Asigna la textura.
        imgGraph.texture = texture;

        // Ajusta la relación de aspecto si hay un AspectRatioFitter en el mismo GameObject.
        AdjustAspectRatio(texture);
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private void Show()
    {
        if (panelGraphs != null)
            panelGraphs.SetActive(true);
    }

    /// <summary>
    /// Ajusta el <see cref="AspectRatioFitter"/> del RawImage para respetar
    /// las proporciones de la textura descargada. Solo actúa si el componente
    /// está presente en el GameObject del RawImage.
    /// </summary>
    private void AdjustAspectRatio(Texture2D texture)
    {
        if (imgGraph == null || texture == null || texture.height == 0)
            return;

        AspectRatioFitter fitter = imgGraph.GetComponent<AspectRatioFitter>();
        if (fitter == null)
            return;

        fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)texture.width / texture.height;
    }
}
