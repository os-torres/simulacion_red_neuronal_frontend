using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// AppController.cs
// Controlador principal de la aplicación. Orquesta el flujo entre la UI y
// la API. Adjuntar a un GameObject raíz en la escena.
// Compatible con Unity 6 (6000.x).
// ---------------------------------------------------------------------------

public class AppController : MonoBehaviour
{
    // ── Referencias a subsistemas UI ─────────────────────────────────────────

    [Header("UI Panels")]
    [SerializeField] private StatusBar     statusBar;
    [SerializeField] private ConfigPanel   configPanel;
    [SerializeField] private GraphDisplay  graphDisplay;
    [SerializeField] private Visualizer3D  visualizer3D;
    [SerializeField] private ClassifyPanel classifyPanel;
    [SerializeField] private ResultsPanel  resultsPanel;

    // ── Parámetros de polling ─────────────────────────────────────────────────

    [Header("Polling")]
    [SerializeField] private float healthCheckInterval  = 2f;
    [SerializeField] private float trainingPollInterval = 0.5f;

    // ── Estado interno ────────────────────────────────────────────────────────

    private bool      _connected       = false;
    private bool      _polling         = false;
    private Coroutine _healthCoroutine;
    private Coroutine _trainingCoroutine;

    // =========================================================================
    // Unity lifecycle
    // =========================================================================

    private void Start()
    {
        statusBar?.SetConnecting();
        _healthCoroutine = StartCoroutine(HealthCheckLoop());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    // =========================================================================
    // Coroutines
    // =========================================================================

    /// <summary>
    /// Llama a CheckHealth cada <see cref="healthCheckInterval"/> segundos
    /// hasta obtener conexión exitosa.
    /// </summary>
    private IEnumerator HealthCheckLoop()
    {
        while (!_connected)
        {
            bool done = false;

            APIManager.Instance.CheckHealth((success, message) =>
            {
                if (success)
                {
                    _connected = true;
                    statusBar?.SetConnected();
                    Debug.Log("[AppController] API conectada: " + message);
                }
                else
                {
                    statusBar?.SetConnecting();
                    Debug.LogWarning("[AppController] API no disponible: " + message);
                }
                done = true;
            });

            // Espera a que el callback termine antes de medir el intervalo.
            yield return new WaitUntil(() => done);

            if (!_connected)
                yield return new WaitForSeconds(healthCheckInterval);
        }

        _healthCoroutine = null;
    }

    /// <summary>
    /// Polling de estado durante el entrenamiento: interroga la API cada
    /// <see cref="trainingPollInterval"/> segundos y actualiza la StatusBar.
    /// Se detiene cuando <c>phase == "done"</c> o cuando hay error.
    /// </summary>
    private IEnumerator TrainingPollLoop()
    {
        _polling = true;

        while (_polling)
        {
            yield return new WaitForSeconds(trainingPollInterval);

            bool done = false;

            APIManager.Instance.GetStatus(status =>
            {
                if (status == null)
                {
                    done = true;
                    return;
                }

                if (status.phase == "done")
                {
                    statusBar?.SetDone(status);
                    _polling = false;
                    OnTrainingCompleted(status);
                }
                else if (status.phase == "error")
                {
                    statusBar?.SetError(status.message ?? "Error desconocido durante el entrenamiento.");
                    _polling = false;
                }
                else
                {
                    statusBar?.SetTraining(status);
                }

                done = true;
            });

            yield return new WaitUntil(() => done);
        }

        _trainingCoroutine = null;
    }

    // =========================================================================
    // Callbacks internos
    // =========================================================================

    private void OnTrainingCompleted(StatusResponse status)
    {
        // Solicita resultados finales al terminar el entrenamiento.
        APIManager.Instance.GetResults((results, error) =>
        {
            if (error != null)
            {
                Debug.LogWarning("[AppController] GetResults error: " + error);
                return;
            }

            resultsPanel?.Show(results);
        });
    }

    // =========================================================================
    // Métodos públicos para botones UI
    // =========================================================================

    /// <summary>Genera el dataset con la configuración actual del ConfigPanel.</summary>
    public void OnGenerateClicked()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        GenerateRequest req = configPanel?.BuildGenerateRequest();
        if (req == null)
        {
            statusBar?.SetError("Error al construir la solicitud de datos.");
            return;
        }

        statusBar?.SetGenerating();

        APIManager.Instance.GenerateData(req, (response, error) =>
        {
            if (error != null)
            {
                statusBar?.SetError("Error generando datos: " + error);
                return;
            }

            if (response != null && response.ok)
            {
                statusBar?.SetConnected();
                Debug.Log($"[AppController] Datos generados — total: {response.total}, clases: {response.n_classes}");
            }
            else
            {
                statusBar?.SetError(response?.message ?? "Error desconocido al generar datos.");
            }
        });
    }

    /// <summary>Inicia el entrenamiento con la configuración actual.</summary>
    public void OnTrainClicked()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        if (_polling)
        {
            Debug.LogWarning("[AppController] Ya hay un entrenamiento en curso.");
            return;
        }

        TrainRequest req = configPanel?.BuildTrainRequest();
        if (req == null)
        {
            statusBar?.SetError("Error al construir la solicitud de entrenamiento.");
            return;
        }

        APIManager.Instance.StartTraining(req, (response, error) =>
        {
            if (error != null)
            {
                statusBar?.SetError("Error iniciando entrenamiento: " + error);
                return;
            }

            if (response != null && response.ok)
            {
                Debug.Log("[AppController] Entrenamiento iniciado.");
                _trainingCoroutine = StartCoroutine(TrainingPollLoop());
            }
            else
            {
                statusBar?.SetError(response?.message ?? "No se pudo iniciar el entrenamiento.");
            }
        });
    }

    /// <summary>Delega la clasificación al ClassifyPanel.</summary>
    public void OnClassifyClicked()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        classifyPanel?.OnClassifyClicked();
    }

    // ── Gráficas ──────────────────────────────────────────────────────────────

    /// <summary>Muestra la gráfica de error/pérdida.</summary>
    public void OnShowErrorPlot()
    {
        graphDisplay?.ShowPlot("error", "Curva de Pérdida");
    }

    /// <summary>Muestra la gráfica de pesos.</summary>
    public void OnShowWeightsPlot()
    {
        graphDisplay?.ShowPlot("weights", "Distribución de Pesos");
    }

    /// <summary>Muestra la matriz de confusión.</summary>
    public void OnShowConfusionPlot()
    {
        graphDisplay?.ShowPlot("confusion", "Matriz de Confusión");
    }

    // ── Visualización 3D ──────────────────────────────────────────────────────

    /// <summary>Solicita los datos 3D y los pasa al Visualizer3D.</summary>
    public void OnShow3D()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        APIManager.Instance.Get3DData((data, error) =>
        {
            if (error != null)
            {
                statusBar?.SetError("Error obteniendo datos 3D: " + error);
                return;
            }

            visualizer3D?.LoadData(data);
        });
    }

    // ── Exportación ───────────────────────────────────────────────────────────

    /// <summary>Descarga y guarda el reporte CSV.</summary>
    public void OnExportCSV()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        APIManager.Instance.DownloadFile("/api/export/csv", "resultados.csv", (success, pathOrError) =>
        {
            if (success)
            {
                statusBar?.SetConnected();
                Debug.Log("[AppController] CSV exportado en: " + pathOrError);
            }
            else
            {
                statusBar?.SetError("Error exportando CSV: " + pathOrError);
            }
        });
    }

    /// <summary>Descarga y guarda el reporte PDF.</summary>
    public void OnExportPDF()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        APIManager.Instance.DownloadFile("/api/export/pdf", "resultados.pdf", (success, pathOrError) =>
        {
            if (success)
            {
                statusBar?.SetConnected();
                Debug.Log("[AppController] PDF exportado en: " + pathOrError);
            }
            else
            {
                statusBar?.SetError("Error exportando PDF: " + pathOrError);
            }
        });
    }
}
