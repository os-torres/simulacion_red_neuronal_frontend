using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ---------------------------------------------------------------------------
// APIManager.cs
// Singleton MonoBehaviour que centraliza todas las llamadas HTTP a la API
// FastAPI en localhost:8000.
// Compatible con Unity 6 (6000.x). Usa UnityWebRequest.
// ---------------------------------------------------------------------------

public class APIManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────

    public static APIManager Instance { get; private set; }

    private const string BASE_URL = "http://localhost:8000";

    // Tiempo máximo de espera por petición (segundos).
    private const int TIMEOUT_SECONDS = 30;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ========================================================================
    // API pública
    // ========================================================================

    /// <summary>GET /health — comprueba si la API está activa.</summary>
    /// <param name="callback">bool: éxito, string: mensaje.</param>
    public void CheckHealth(Action<bool, string> callback)
    {
        StartCoroutine(GetRequest<HealthResponse>(
            $"{BASE_URL}/health",
            (response, error) =>
            {
                if (error != null)
                    callback?.Invoke(false, error);
                else
                    callback?.Invoke(true, response?.message ?? "OK");
            }
        ));
    }

    /// <summary>GET /api/status — estado actual del entrenamiento.</summary>
    public void GetStatus(Action<StatusResponse> callback)
    {
        StartCoroutine(GetRequest<StatusResponse>(
            $"{BASE_URL}/api/status",
            (response, error) =>
            {
                if (error != null)
                    Debug.LogWarning($"[APIManager] GetStatus error: {error}");
                callback?.Invoke(response);
            }
        ));
    }

    /// <summary>POST /api/generate — genera el dataset.</summary>
    public void GenerateData(GenerateRequest req, Action<GenerateResponse, string> callback)
    {
        StartCoroutine(PostJson<GenerateRequest, GenerateResponse>(
            $"{BASE_URL}/api/generate", req, callback));
    }

    /// <summary>POST /api/train — inicia el entrenamiento.</summary>
    public void StartTraining(TrainRequest req, Action<TrainResponse, string> callback)
    {
        StartCoroutine(PostJson<TrainRequest, TrainResponse>(
            $"{BASE_URL}/api/train", req, callback));
    }

    /// <summary>POST /api/classify — clasifica un punto.</summary>
    public void Classify(ClassifyRequest req, Action<ClassifyResponse, string> callback)
    {
        StartCoroutine(PostJson<ClassifyRequest, ClassifyResponse>(
            $"{BASE_URL}/api/classify", req, callback));
    }

    /// <summary>
    /// GET /api/plot/{plotType} — descarga un gráfico como Texture2D.
    /// <paramref name="plotType"/>: "error", "weights" o "confusion".
    /// </summary>
    public void GetPlotTexture(string plotType, Action<Texture2D, string> callback)
    {
        StartCoroutine(GetTexture($"{BASE_URL}/api/plot/{plotType}", callback));
    }

    /// <summary>GET /api/visualization/3d — datos de visualización 3D.</summary>
    public void Get3DData(Action<VisualizationData3D, string> callback)
    {
        StartCoroutine(GetRequest<VisualizationData3D>(
            $"{BASE_URL}/api/visualization/3d", callback));
    }

    /// <summary>GET /api/results — métricas finales del modelo.</summary>
    public void GetResults(Action<ResultsResponse, string> callback)
    {
        StartCoroutine(GetRequest<ResultsResponse>(
            $"{BASE_URL}/api/results", callback));
    }

    /// <summary>
    /// Descarga un archivo binario (CSV, PDF, etc.) desde
    /// <paramref name="endpoint"/> y lo guarda en
    /// <see cref="Application.persistentDataPath"/>/<paramref name="filename"/>.
    /// </summary>
    /// <param name="callback">bool: éxito, string: ruta completa o mensaje de error.</param>
    public void DownloadFile(string endpoint, string filename, Action<bool, string> callback)
    {
        StartCoroutine(DownloadFileCoroutine(endpoint, filename, callback));
    }

    // ========================================================================
    // Helpers privados
    // ========================================================================

    /// <summary>Coroutine genérica para GET que deserializa JSON a <typeparamref name="T"/>.</summary>
    private IEnumerator GetRequest<T>(string url, Action<T, string> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = TIMEOUT_SECONDS;
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = BuildErrorMessage(request);
                Debug.LogWarning($"[APIManager] GET {url} failed: {error}");
                callback?.Invoke(default, error);
                yield break;
            }

            T result = DeserializeJson<T>(request.downloadHandler.text, url);
            callback?.Invoke(result, result == null ? "Error al deserializar la respuesta JSON." : null);
        }
    }

    /// <summary>
    /// Coroutine genérica para POST con cuerpo JSON.
    /// Serializa <typeparamref name="TReq"/> y deserializa la respuesta como <typeparamref name="TRes"/>.
    /// </summary>
    private IEnumerator PostJson<TReq, TRes>(string url, TReq body, Action<TRes, string> callback)
    {
        string jsonBody;
        try
        {
            jsonBody = JsonUtility.ToJson(body);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[APIManager] Error serializando el cuerpo del POST: {ex.Message}");
            callback?.Invoke(default, $"Error de serialización: {ex.Message}");
            yield break;
        }

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout         = TIMEOUT_SECONDS;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept",       "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = BuildErrorMessage(request);
                Debug.LogWarning($"[APIManager] POST {url} failed: {error}");
                callback?.Invoke(default, error);
                yield break;
            }

            TRes result = DeserializeJson<TRes>(request.downloadHandler.text, url);
            callback?.Invoke(result, result == null ? "Error al deserializar la respuesta JSON." : null);
        }
    }

    /// <summary>Descarga una imagen y la convierte en Texture2D.</summary>
    private IEnumerator GetTexture(string url, Action<Texture2D, string> callback)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = TIMEOUT_SECONDS;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = BuildErrorMessage(request);
                Debug.LogWarning($"[APIManager] GetTexture {url} failed: {error}");
                callback?.Invoke(null, error);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            callback?.Invoke(texture, texture == null ? "No se pudo obtener la textura." : null);
        }
    }

    /// <summary>
    /// Descarga bytes crudos desde <paramref name="endpoint"/> y los guarda en disco.
    /// </summary>
    private IEnumerator DownloadFileCoroutine(string endpoint, string filename, Action<bool, string> callback)
    {
        string url = endpoint.StartsWith("http") ? endpoint : $"{BASE_URL}{endpoint}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout         = TIMEOUT_SECONDS;
            request.downloadHandler = new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = BuildErrorMessage(request);
                Debug.LogWarning($"[APIManager] DownloadFile {url} failed: {error}");
                callback?.Invoke(false, error);
                yield break;
            }

            string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
            try
            {
                System.IO.File.WriteAllBytes(filePath, request.downloadHandler.data);
                Debug.Log($"[APIManager] Archivo guardado en: {filePath}");
                callback?.Invoke(true, filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APIManager] Error guardando archivo: {ex.Message}");
                callback?.Invoke(false, $"Error al guardar el archivo: {ex.Message}");
            }
        }
    }

    // ── Utilidades ───────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta deserializar <paramref name="json"/> como <typeparamref name="T"/>.
    /// Devuelve <c>default</c> y registra un warning si falla.
    /// </summary>
    private static T DeserializeJson<T>(string json, string url)
    {
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[APIManager] Error deserializando respuesta de {url}: {ex.Message}\nJSON: {json}");
            return default;
        }
    }

    /// <summary>Construye un mensaje de error legible a partir del request fallido.</summary>
    private static string BuildErrorMessage(UnityWebRequest request)
    {
        // Intenta incluir el cuerpo de la respuesta si la hay (ej. error 422 de FastAPI).
        string body = request.downloadHandler?.text;
        if (!string.IsNullOrEmpty(body) && body.Length < 512)
            return $"[{request.responseCode}] {request.error} — {body}";
        return $"[{request.responseCode}] {request.error}";
    }
}
