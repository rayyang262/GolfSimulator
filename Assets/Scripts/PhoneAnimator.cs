using UnityEngine;
using System.Collections;

/// <summary>
/// Slides the phone up from below the screen on Start.
/// Attach to the PhoneFrame RectTransform.
/// </summary>
public class PhoneAnimator : MonoBehaviour
{
    [Header("Slide-In Animation")]
    public float slideDuration   = 0.6f;
    public float slideDelay      = 0.4f;
    public float startOffsetBelow = 600f;
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Button Tap Feedback")]
    public float tapScaleAmount   = 0.93f;
    public float tapScaleDuration = 0.12f;

    private RectTransform rect;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        // Wait one frame so PhoneUISetup finishes positioning first
        StartCoroutine(SlideInAfterSetup());
    }

    private IEnumerator SlideInAfterSetup()
    {
        // Wait one frame — lets PhoneUISetup.Start() finish setting final position
        yield return null;

        // Now capture the correct final position
        Vector2 finalPos = rect.anchoredPosition;

        // Move phone below screen
        rect.anchoredPosition = finalPos + new Vector2(0f, -startOffsetBelow);

        // Wait before sliding
        yield return new WaitForSeconds(slideDelay);

        // Slide up to final position
        Vector2 startPos = rect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / slideDuration);
            float curved = slideCurve.Evaluate(t);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPos, finalPos, curved);
            yield return null;
        }

        rect.anchoredPosition = finalPos;
    }

    /// <summary>Call from a Button onClick for a press-scale effect.</summary>
    public void PlayTapFeedback()
    {
        StopCoroutine("TapScale");
        StartCoroutine(TapScale());
    }

    private IEnumerator TapScale()
    {
        float half = tapScaleDuration / 2f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, tapScaleAmount, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(tapScaleAmount, 1f, t / half);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }
}
