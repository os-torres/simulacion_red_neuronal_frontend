using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// GraphDisplay.cs
// Muestra gráficas PNG descargadas desde la API.
//
// Modo "Ajustar" (por defecto):
//   AspectRatioFitter(FitInParent) → la imagen siempre se ve proporcional y
//   ocupa el espacio disponible. No hay problema de timing de layout.
//
// Modo "Zoom" (al hacer clic en + o −):
//   Se desactiva el ARF y se asignan sizeDelta explícitos a ImgGraph y al
//   Content del ScrollRect. Cuando la imagen supera el viewport, el scroll
//   se activa automáticamente. "Ajustar" regresa al primer modo.
//
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class GraphDisplay : MonoBehaviour
{
    // ── Referencias Inspector ─────────────────────────────────────────────────

    [Header("Componentes UI")]
    [SerializeField] private RawImage        imgGraph;
    [SerializeField] private GameObject      panelGraphs;
    [SerializeField] private TextMeshProUGUI txtGraphTitle;

    [Header("Zoom / Scroll")]
    /// <summary>RectTransform del Content dentro del GraphScrollView.</summary>
    [SerializeField] private RectTransform   scrollContent;
    /// <summary>Label que muestra el porcentaje de zoom o "Ajustar".</summary>
    [SerializeField] private TextMeshProUGUI txtZoomLabel;

    // ── Estado interno ────────────────────────────────────────────────────────

    private string    _currentPlotType;
    private Texture2D _currentTexture;
    private float     _zoomScale  = 1f;
    private bool      _zoomActive = false;   // false = modo Ajustar, true = modo Zoom

    // Referencia cacheada al AspectRatioFitter del ImgGraph
    private AspectRatioFitter _fitter;

    private const float ZoomStep = 1.25f;
    private const float ZoomMin  = 0.10f;
    private const float ZoomMax  = 8.00f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Cachear el AspectRatioFitter que el Wizard coloca en ImgGraph
        if (imgGraph != null)
            _fitter = imgGraph.GetComponent<AspectRatioFitter>();
    }

    // =========================================================================
    // API pública — mostrar gráfica
    // =========================================================================

    public void ShowPlot(string plotType, string title)
    {
        if (string.IsNullOrEmpty(plotType)) return;

        _currentPlotType = plotType;
        Show();

        if (imgGraph    != null) imgGraph.texture     = null;
        if (txtGraphTitle != null) txtGraphTitle.text = title ?? plotType;

        APIManager.Instance.GetPlotTexture(plotType, OnTextureReceived);
    }

    public void Hide()
    {
        if (panelGraphs != null) panelGraphs.SetActive(false);
    }

    // =========================================================================
    // API pública — zoom
    // =========================================================================

    public void ZoomIn()
    {
        EnterZoomMode();
        _zoomScale = Mathf.Min(_zoomScale * ZoomStep, ZoomMax);
        ApplyExplicitZoom();
    }

    public void ZoomOut()
    {
        EnterZoomMode();
        _zoomScale = Mathf.Max(_zoomScale / ZoomStep, ZoomMin);
        ApplyExplicitZoom();
    }

    /// <summary>Vuelve al modo Ajustar (AspectRatioFitter, sin scroll).</summary>
    public void ResetZoom()
    {
        EnterFitMode();
    }

    // =========================================================================
    // Callbacks privados
    // =========================================================================

    private void OnTextureReceived(Texture2D texture, string error)
    {
        if (error != null)
        {
            Debug.LogWarning($"[GraphDisplay] Error '{_currentPlotType}': {error}");
            Hide();
            return;
        }
        if (texture == null || imgGraph == null)
        {
            Hide();
            return;
        }

        imgGraph.texture = texture;
        _currentTexture  = texture;

        // Siempre empezar en modo Ajustar al recibir una nueva imagen:
        // AspectRatioFitter maneja la proporcionalidad sin depender del
        // tamaño del viewport en ese instante (no hay problema de timing).
        EnterFitMode();
    }

    // =========================================================================
    // Modos de visualización
    // =========================================================================

    /// <summary>
    /// Modo Ajustar: AspectRatioFitter(FitInParent) → imagen proporcional,
    /// siempre dentro del panel, sin necesidad de scroll.
    /// </summary>
    private void EnterFitMode()
    {
        _zoomActive = false;
        _zoomScale  = 1f;

        if (imgGraph == null) return;

        // ImgGraph cubre todo el Content con anclas de stretch
        var imgRT = imgGraph.rectTransform;
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;

        // Content también vuelve a las anclas de stretch (viewport completo)
        if (scrollContent != null)
        {
            scrollContent.anchorMin = Vector2.zero;
            scrollContent.anchorMax = Vector2.one;
            scrollContent.offsetMin = scrollContent.offsetMax = Vector2.zero;
        }

        // Activar AspectRatioFitter con la relación real de la textura
        if (_fitter != null)
        {
            _fitter.enabled    = true;
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (_currentTexture != null && _currentTexture.height > 0)
                _fitter.aspectRatio = (float)_currentTexture.width / _currentTexture.height;
        }

        if (txtZoomLabel != null) txtZoomLabel.text = "Ajustar";
    }

    /// <summary>
    /// Modo Zoom: desactiva el ARF y asigna sizeDelta explícito para que el
    /// ScrollRect pueda desplazar la imagen cuando supera el viewport.
    /// </summary>
    private void EnterZoomMode()
    {
        if (_zoomActive) return;   // ya estaba en zoom mode
        _zoomActive = true;

        if (imgGraph == null) return;

        // Desactivar el AspectRatioFitter — el tamaño lo controla ApplyExplicitZoom
        if (_fitter != null) _fitter.enabled = false;

        // Calcular el zoom inicial para que coincida con lo que se veía en Ajustar
        if (_currentTexture != null)
        {
            float viewW = 0f, viewH = 0f;
            if (scrollContent != null)
            {
                var sr = scrollContent.GetComponentInParent<ScrollRect>();
                if (sr?.viewport != null)
                {
                    viewW = sr.viewport.rect.width;
                    viewH = sr.viewport.rect.height;
                }
            }
            _zoomScale = (viewW > 0 && viewH > 0)
                ? Mathf.Min(viewW / _currentTexture.width, viewH / _currentTexture.height)
                : 1f;
        }

        // Cambiar anclaje de ImgGraph a esquina superior-izquierda
        var imgRT = imgGraph.rectTransform;
        imgRT.anchorMin        = new Vector2(0f, 1f);
        imgRT.anchorMax        = new Vector2(0f, 1f);
        imgRT.pivot            = new Vector2(0f, 1f);
        imgRT.anchoredPosition = Vector2.zero;

        // Cambiar anclaje de Content a esquina superior-izquierda
        if (scrollContent != null)
        {
            scrollContent.anchorMin        = new Vector2(0f, 1f);
            scrollContent.anchorMax        = new Vector2(0f, 1f);
            scrollContent.pivot            = new Vector2(0f, 1f);
            scrollContent.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Aplica el zoom actual con sizeDelta explícito.</summary>
    private void ApplyExplicitZoom()
    {
        if (_currentTexture == null || imgGraph == null) return;

        float w  = _currentTexture.width  * _zoomScale;
        float h  = _currentTexture.height * _zoomScale;
        var   sz = new Vector2(w, h);

        imgGraph.rectTransform.sizeDelta = sz;
        if (scrollContent != null) scrollContent.sizeDelta = sz;
        if (txtZoomLabel  != null) txtZoomLabel.text = $"{Mathf.RoundToInt(_zoomScale * 100)} %";
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void Show()
    {
        if (panelGraphs != null) panelGraphs.SetActive(true);
    }
}
