using System;

// ---------------------------------------------------------------------------
// APIModels.cs
// Clases de datos serializables que mapean las respuestas/peticiones de la API
// Compatible con JsonUtility de Unity (no soporta Dictionary ni jagged arrays
// directamente, por eso se usan clases wrapper para arrays de arrays).
// ---------------------------------------------------------------------------

// ── Wrappers para arrays de arrays ─────────────────────────────────────────

[System.Serializable]
public class FloatArray3
{
    public float[] data;

    public FloatArray3() { data = new float[0]; }
    public FloatArray3(float[] d) { data = d; }

    public UnityEngine.Vector3 ToVector3()
    {
        if (data == null || data.Length < 3)
            return UnityEngine.Vector3.zero;
        return new UnityEngine.Vector3(data[0], data[1], data[2]);
    }
}

[System.Serializable]
public class IntArray3
{
    public int[] data;

    public IntArray3() { data = new int[0]; }
    public IntArray3(int[] d) { data = d; }
}

[System.Serializable]
public class IntArray
{
    public int[] data;

    public IntArray() { data = new int[0]; }
    public IntArray(int[] d) { data = d; }
}

// ── Health ──────────────────────────────────────────────────────────────────

[System.Serializable]
public class HealthResponse
{
    public string status;
    public string message;
}

// ── Status ──────────────────────────────────────────────────────────────────

[System.Serializable]
public class StatusResponse
{
    public string phase;
    public string message;
    public int epoch;
    public int total_epochs;
    public float loss;
    public float accuracy;
    public float accuracy_pct;
    public float progress;
    public bool has_data;
    public bool has_model;
}

// ── Generate ────────────────────────────────────────────────────────────────

[System.Serializable]
public class GenerateRequest
{
    public int n_classes;
    public bool equal_instances;
    public int n_instances;
    public int[] instances_per_class;
    public bool sep_auto;
    public string sep_mode;
    public float mean_sep;
    public float std_dev;
    public float[] mean_sep_per_class;
    public float[] std_dev_per_class;
    public int seed;
}

[System.Serializable]
public class GenerateResponse
{
    public bool ok;
    public int total;
    public int n_classes;
    public float separability;
    public string level;
    public int[] per_class;
    public string message;
}

// ── Train ───────────────────────────────────────────────────────────────────

[System.Serializable]
public class TrainRequest
{
    public bool layers_auto;
    public int[] hidden_layers;
    public bool act_auto;
    public string activation;
    public float learning_rate;
    public int epochs;
    public float momentum;
    public int record_every;
}

[System.Serializable]
public class TrainResponse
{
    public bool ok;
    public string message;
    public int[] layer_sizes;
    public string activation;
}

// ── Classify ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class ClassifyRequest
{
    public float x1;
    public float x2;
    public float x3;
}

[System.Serializable]
public class ClassifyResponse
{
    public int class_id;
    public float confidence;
    public float[] probabilities;
}

// ── Visualization 3D ─────────────────────────────────────────────────────────

[System.Serializable]
public class DataPoint3D
{
    public float x;
    public float y;
    public float z;
    public int class_id;
    public int predicted_id;
    public bool correct;
    public float confidence;
}

/// <summary>
/// Malla de frontera de decisión para una clase.
/// JsonUtility no puede deserializar float[][] directamente, por eso se usan
/// <see cref="FloatArray3"/> e <see cref="IntArray3"/> como wrappers.
/// Usa <see cref="GetVertices"/> y <see cref="GetTriangles"/> para acceder a
/// los datos en formato nativo de Unity.
/// </summary>
[System.Serializable]
public class BoundaryMesh
{
    public int class_id;
    public string color;

    /// <summary>Cada elemento contiene [x, y, z] de un vértice.</summary>
    public FloatArray3[] vertices_raw;

    /// <summary>Cada elemento contiene [a, b, c] de un triángulo (índices).</summary>
    public IntArray3[] triangles_raw;

    /// <summary>Devuelve los vértices como array de Vector3.</summary>
    public UnityEngine.Vector3[] GetVertices()
    {
        if (vertices_raw == null) return Array.Empty<UnityEngine.Vector3>();
        var result = new UnityEngine.Vector3[vertices_raw.Length];
        for (int i = 0; i < vertices_raw.Length; i++)
            result[i] = vertices_raw[i] != null ? vertices_raw[i].ToVector3() : UnityEngine.Vector3.zero;
        return result;
    }

    /// <summary>
    /// Devuelve los índices de triángulos como array plano [a0,b0,c0, a1,b1,c1, ...].
    /// </summary>
    public int[] GetTriangles()
    {
        if (triangles_raw == null) return Array.Empty<int>();
        var result = new int[triangles_raw.Length * 3];
        for (int i = 0; i < triangles_raw.Length; i++)
        {
            int[] tri = triangles_raw[i]?.data;
            if (tri != null && tri.Length >= 3)
            {
                result[i * 3]     = tri[0];
                result[i * 3 + 1] = tri[1];
                result[i * 3 + 2] = tri[2];
            }
        }
        return result;
    }
}

[System.Serializable]
public class ClassCenter3D
{
    public int class_id;
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class Bounds3D
{
    public float x_min;
    public float x_max;
    public float y_min;
    public float y_max;
    public float z_min;
    public float z_max;
}

[System.Serializable]
public class VisualizationData3D
{
    public Bounds3D bounds;
    public DataPoint3D[] points;
    public ClassCenter3D[] centers;
    public BoundaryMesh[] boundaries;
    public string[] colors;
    public int n_classes;
    public float accuracy;
    public int total_points;
}

// ── Results ──────────────────────────────────────────────────────────────────

[System.Serializable]
public class PerClassMetric
{
    public int class_id;
    public string class_name;   // opcional, para mostrar en tabla
    public int total;
    public int correct;
    public float precision;
    public float recall;
    public float f1;
}

/// <summary>
/// Resultados del entrenamiento.
/// La matriz de confusión (int[][]) se representa como <see cref="IntArray"/>[]
/// para ser compatible con JsonUtility.
/// Usa <see cref="GetConfusionMatrix"/> para obtener la matriz int[][].
/// </summary>
[System.Serializable]
public class ResultsResponse
{
    public float accuracy;

    /// <summary>Cada elemento contiene una fila de la matriz de confusión.</summary>
    public IntArray[] confusion_matrix;

    public PerClassMetric[] per_class_metrics;
    public int[] architecture;
    public string activation;
    public int epochs;
    public int total_params;

    /// <summary>Devuelve la matriz de confusión como int[][].</summary>
    public int[][] GetConfusionMatrix()
    {
        if (confusion_matrix == null) return Array.Empty<int[]>();
        var result = new int[confusion_matrix.Length][];
        for (int i = 0; i < confusion_matrix.Length; i++)
            result[i] = confusion_matrix[i]?.data ?? Array.Empty<int>();
        return result;
    }
}
