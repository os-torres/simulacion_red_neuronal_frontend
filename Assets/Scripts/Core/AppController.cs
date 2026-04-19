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
    [SerializeField] private DataPanel     dataPanel;

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

    // ── Reinicio ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reinicia el servidor y toda la UI al estado inicial.
    /// Equivale a "empezar de nuevo" sin cerrar la aplicación.
    /// </summary>
    public void OnResetClicked()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexion con la API.");
            return;
        }

        // Detener polling si está activo
        if (_trainingCoroutine != null)
        {
            StopCoroutine(_trainingCoroutine);
            _trainingCoroutine = null;
        }
        _polling = false;

        // Llamar al endpoint de reset
        APIManager.Instance.Reset((success, msg) =>
        {
            if (!success)
            {
                statusBar?.SetError("Error al reiniciar: " + msg);
                return;
            }

            // Limpiar todos los paneles
            resultsPanel?.Reset();
            dataPanel?.Reset();
            graphDisplay?.Hide();
            visualizer3D?.Clear();

            statusBar?.SetConnected();
            Debug.Log("[AppController] Reset completado.");
        });
    }

    // ── Tabla de datos ────────────────────────────────────────────────────────

    /// <summary>Carga y muestra todos los puntos generados en el DataPanel.</summary>
    public void OnShowData()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        dataPanel?.ShowLoading();

        APIManager.Instance.GetDataPoints((response, error) =>
        {
            if (error != null)
            {
                dataPanel?.ShowError(error);
                statusBar?.SetError("Error cargando datos: " + error);
                return;
            }

            dataPanel?.LoadData(response);
        });
    }

    // ── Exportación ───────────────────────────────────────────────────────────

    // Carpeta donde se guardan CSV y PDF.
    // Se crea automáticamente si no existe.
    private const string ExportFolder =
        "/Users/oscartorres/Desarrollo/Universidad/Inteligencia Artificial" +
        "/Segunda Entrega/Exportaciones_NN";

    private static string ExportPath(string filename)
    {
        System.IO.Directory.CreateDirectory(ExportFolder);
        return System.IO.Path.Combine(ExportFolder, filename);
    }

    /// <summary>Descarga y guarda el reporte CSV.</summary>
    public void OnExportCSV()
    {
        if (!_connected)
        {
            statusBar?.SetError("Sin conexión con la API.");
            return;
        }

        APIManager.Instance.DownloadFile("/api/export/csv", ExportPath("resultados.csv"),
            (success, pathOrError) =>
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

        APIManager.Instance.DownloadFile("/api/export/pdf", ExportPath("resultados.pdf"),
            (success, pathOrError) =>
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
