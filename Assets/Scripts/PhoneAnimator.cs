using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Slides the phone up/down when the player presses I.
/// Attach to the 3D phone model parented under the camera.
/// </summary>
public class PhoneAnimator : MonoBehaviour
{
    [Header("Slide-In Animation")]
    public float slideDuration    = 0.6f;
    public float slideDelay       = 0.4f;
    public float startOffsetBelow = 0.5f;
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Button Tap Feedback")]
    public float tapScaleAmount   = 0.93f;
    public float tapScaleDuration = 0.12f;

    private Vector3 finalPos;
    private bool isUp = false;
    private bool isAnimating = false;
    private Coroutine slideCoroutine;

    /// <summary>True when the phone is up and the user can interact with it.</summary>
    public bool IsPhoneUp => isUp;

    void Start()
    {
        StartCoroutine(SlideInAfterSetup());
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame && !isAnimating)
        {
            if (isUp)
                SlideDown();
            else
                SlideUp();
        }
    }

    public void SlideUp()
    {
        if (isAnimating || isUp) return;
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideTo(finalPos, true));
    }

    public void SlideDown()
    {
        if (isAnimating || !isUp) return;
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        Vector3 hiddenPos = finalPos + new Vector3(0f, -startOffsetBelow, 0f);
        slideCoroutine = StartCoroutine(SlideTo(hiddenPos, false));
    }

    private IEnumerator SlideTo(Vector3 targetPos, bool upAfter)
    {
        isAnimating = true;
        Vector3 startPos = transform.localPosition;
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float curved = slideCurve.Evaluate(t);
            transform.localPosition = Vector3.LerpUnclamped(startPos, targetPos, curved);
            yield return null;
        }

        transform.localPosition = targetPos;
        isUp = upAfter;
        isAnimating = false;
    }

    private IEnumerator SlideInAfterSetup()
    {
        yield return null;

        finalPos = transform.localPosition;
        transform.localPosition = finalPos + new Vector3(0f, -startOffsetBelow, 0f);

        yield return new WaitForSeconds(slideDelay);

        Vector3 startPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float curved = slideCurve.Evaluate(t);
            transform.localPosition = Vector3.LerpUnclamped(startPos, finalPos, curved);
            yield return null;
        }

        transform.localPosition = finalPos;
        isUp = true;
    }

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
