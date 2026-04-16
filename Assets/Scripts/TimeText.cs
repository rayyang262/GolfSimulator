using UnityEngine;
using TMPro;

/// <summary>
/// Attach to any TextMeshProUGUI to display the current time.
/// Works on any Canvas (screen-space or world-space).
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TimeText : MonoBehaviour
{
    [Tooltip("Standard .NET date/time format string. Examples: \"H:mm\", \"h:mm tt\", \"HH:mm:ss\".")]
    public string format = "h:mm tt";

    private TextMeshProUGUI label;
    private string lastShown;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        string now = System.DateTime.Now.ToString(format);
        if (now != lastShown)
        {
            label.text = now;
            lastShown = now;
        }
    }
}
