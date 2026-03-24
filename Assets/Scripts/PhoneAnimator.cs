using UnityEngine;
using System.Collections;

/// <summary>
/// Adds a subtle idle animation to the phone (gentle bob + tap scale feedback).
/// Attach this to the Phone Frame RectTransform.
/// </summary>
public class PhoneAnimator : MonoBehaviour
{
    [Header("Idle Bob")]
    public float bobHeight   = 6f;     // pixels up/down
    public float bobSpeed    = 1.2f;   // cycles per second

    [Header("Button Tap Feedback")]
    public float tapScaleAmount = 0.92f;
    public float tapScaleDuration = 0.1f;

    private RectTransform rect;
    private Vector2 startPos;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        startPos = rect.anchoredPosition;
    }

    void Update()
    {
        // Gentle up/down bob
        float offset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
        rect.anchoredPosition = startPos + new Vector2(0f, offset);
    }

    /// <summary>
    /// Call this from a Button's onClick to get a scale-press effect.
    /// </summary>
    public void PlayTapFeedback()
    {
        StopAllCoroutines();
        StartCoroutine(TapScale());
    }

    private IEnumerator TapScale()
    {
        float half = tapScaleDuration / 2f;
        float t = 0f;

        // Shrink
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, tapScaleAmount, t / half);
            rect.localScale = Vector3.one * s;
            yield return null;
        }

        t = 0f;
        // Grow back
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(tapScaleAmount, 1f, t / half);
            rect.localScale = Vector3.one * s;
            yield return null;
        }

        rect.localScale = Vector3.one;
    }
}
