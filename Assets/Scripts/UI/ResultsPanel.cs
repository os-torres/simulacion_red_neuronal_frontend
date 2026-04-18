using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays the training results panel.
/// Call Show(results) after a successful API response and Hide() to collapse it.
/// </summary>
public class ResultsPanel : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------
    [Header("Panel root")]
    [SerializeField] private GameObject panelResults;

    [Header("Summary labels")]
    [SerializeField] private TextMeshProUGUI txtAccuracy;      // e.g. "98.23 %"
    [SerializeField] private TextMeshProUGUI txtArchitecture;  // e.g. "3 → 16 → 8 → 3"
    [SerializeField] private TextMeshProUGUI txtActivation;
    [SerializeField] private TextMeshProUGUI txtEpochs;
    [SerializeField] private TextMeshProUGUI txtParams;

    [Header("Per-class metrics table")]
    [SerializeField] private Transform  tableContainer;  // Parent for rows
    [SerializeField] private GameObject rowPrefab;       // Prefab: has TextMeshProUGUI[] children

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        Hide();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Populates and shows the results panel.
    /// </summary>
    /// <param name="results">Data returned by the training API.</param>
    public void Show(ResultsResponse results)
    {
        if (panelResults != null)
            panelResults.SetActive(true);

        if (results == null) return;

        // --- Summary fields -----------------------------------------------
        SetText(txtAccuracy,     FormatPercent(results.accuracy));
        SetText(txtArchitecture, BuildArchitectureString(results.architecture));
        SetText(txtActivation,   results.activation ?? "-");
        SetText(txtEpochs,       results.epochs.ToString());
        SetText(txtParams,       results.total_params.ToString("N0"));

        // --- Per-class metrics table --------------------------------------
        ClearTable();

        if (results.per_class_metrics != null && tableContainer != null && rowPrefab != null)
        {
            foreach (PerClassMetric metric in results.per_class_metrics)
            {
                GameObject row = Instantiate(rowPrefab, tableContainer);

                // Expected column order in the prefab's TextMeshProUGUI children:
                // [0] Class  [1] Total  [2] Correctas  [3] Precision  [4] Recall  [5] F1
                TextMeshProUGUI[] cols = row.GetComponentsInChildren<TextMeshProUGUI>();

                if (cols.Length >= 6)
                {
                    cols[0].text = metric.class_name  ?? metric.class_id.ToString();
                    cols[1].text = metric.total.ToString();
                    cols[2].text = metric.correct.ToString();
                    cols[3].text = FormatPercent(metric.precision);
                    cols[4].text = FormatPercent(metric.recall);
                    cols[5].text = FormatPercent(metric.f1);
                }
                else
                {
                    // Partial fill if the prefab has fewer columns
                    for (int i = 0; i < cols.Length; i++)
                    {
                        cols[i].text = i switch
                        {
                            0 => metric.class_name ?? metric.class_id.ToString(),
                            1 => metric.total.ToString(),
                            2 => metric.correct.ToString(),
                            3 => FormatPercent(metric.precision),
                            4 => FormatPercent(metric.recall),
                            5 => FormatPercent(metric.f1),
                            _ => "-"
                        };
                    }
                }
            }
        }
    }

    /// <summary>Hides the results panel without destroying its data.</summary>
    public void Hide()
    {
        if (panelResults != null)
            panelResults.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value ?? "-";
    }

    private static string FormatPercent(float value)
    {
        // The API already sends values as percentages (0-100).
        return $"{value:F2} %";
    }

    /// <summary>
    /// Turns an int[] layer array into "3 → 16 → 8 → 3".
    /// Falls back to the raw string field if the array is absent.
    /// </summary>
    private static string BuildArchitectureString(int[] layers)
    {
        if (layers == null || layers.Length == 0)
            return "-";
        return string.Join(" \u2192 ", layers); // → (U+2192)
    }

    /// <summary>Destroys all existing rows in the table container.</summary>
    private void ClearTable()
    {
        if (tableContainer == null) return;
        for (int i = tableContainer.childCount - 1; i >= 0; i--)
            Destroy(tableContainer.GetChild(i).gameObject);
    }
}
