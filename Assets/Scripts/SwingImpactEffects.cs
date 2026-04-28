using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Attach to PlayerCapsule alongside GolfSwingController.
///
/// Handles all visual/timing feedback for a swing:
///   - White screen flash + fade on impact
///   - Brief slow-motion freeze at impact moment
///   - Camera positional shake (intensity scales with power)
///   - "READY — SWING NOW" pulsing green banner when phone is armed
///   - Cooldown countdown timer displayed top-right after each swing
/// </summary>
public class SwingImpactEffects : MonoBehaviour
{
    public static SwingImpactEffects Instance { get; private set; }

    [Header("Screen Flash")]
    [Tooltip("Peak alpha of the white flash.")]
    public float flashPeakAlpha = 0.65f;
    [Tooltip("Seconds for the flash to fade out.")]
    public float flashFadeDuration = 0.45f;

    [Header("Slow Motion")]
    [Tooltip("Time scale during the slow-motion freeze.")]
    [Range(0.05f, 0.5f)]
    public float slowMoScale = 0.15f;
    [Tooltip("Real-world seconds the slow-mo lasts.")]
    public float slowMoDuration = 0.28f;

    [Header("Camera Shake")]
    public float shakeMinMagnitude = 0.03f;
    public float shakeMaxMagnitude = 0.12f;
    [Tooltip("Real-world seconds the shake lasts.")]
    public float shakeDuration = 0.32f;

    // ── private ───────────────────────────────────────────────────────────────

    private Camera   _cam;
    private Vector3  _camLocalOrigin;

    // UI
    private Image    _flashImage;

    private Canvas   _cooldownCanvas;
    private TMP_Text _cooldownText;

    private Canvas   _readyCanvas;
    private Image    _readyBg;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam != null) _camLocalOrigin = _cam.transform.localPosition;

        BuildFlashUI();
        BuildCooldownUI();
        BuildReadyUI();
    }

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>Call from GolfSwingController.TryHitBall after applying force.</summary>
    public void PlayImpactEffects(float power, float maxPower)
    {
        float t = Mathf.InverseLerp(0f, maxPower, power);
        StartCoroutine(FlashScreen());
        StartCoroutine(SlowMotion());
        StartCoroutine(ShakeCamera(t));
    }

    /// <summary>
    /// Shows the countdown timer. waitExtra is polled each frame — timer UI stays up
    /// until both the countdown finishes AND waitExtra() returns false.
    /// </summary>
    public void StartCooldown(float duration, Func<bool> waitExtra = null)
    {
        StopCoroutine("CooldownTimer");   // cancel any previous
        StartCoroutine(CooldownTimer(duration, waitExtra));
    }

    /// <summary>Show or hide the "READY — SWING NOW" banner.</summary>
    public void SetReadyIndicator(bool show)
    {
        if (_readyCanvas != null)
            _readyCanvas.gameObject.SetActive(show);
    }

    // ── effect coroutines ─────────────────────────────────────────────────────

    IEnumerator FlashScreen()
    {
        _flashImage.color = new Color(1f, 1f, 1f, flashPeakAlpha);
        float elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _flashImage.color = new Color(1f, 1f, 1f,
                Mathf.Lerp(flashPeakAlpha, 0f, elapsed / flashFadeDuration));
            yield return null;
        }
        _flashImage.color = Color.clear;
    }

    IEnumerator SlowMotion()
    {
        Time.timeScale       = slowMoScale;
        Time.fixedDeltaTime  = 0.02f * slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoDuration);
        Time.timeScale       = 1f;
        Time.fixedDeltaTime  = 0.02f;
    }

    IEnumerator ShakeCamera(float normalizedPower)
    {
        if (_cam == null) yield break;
        float magnitude = Mathf.Lerp(shakeMinMagnitude, shakeMaxMagnitude, normalizedPower);
        float elapsed   = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - elapsed / shakeDuration;
            _cam.transform.localPosition = _camLocalOrigin + new Vector3(
                UnityEngine.Random.Range(-1f, 1f) * magnitude * decay,
                UnityEngine.Random.Range(-1f, 1f) * magnitude * decay,
                0f);
            yield return null;
        }
        _cam.transform.localPosition = _camLocalOrigin;
    }

    IEnumerator CooldownTimer(float duration, Func<bool> waitExtra)
    {
        _cooldownCanvas.gameObject.SetActive(true);

        float remaining = duration;
        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            _cooldownText.text = $"Next swing: {Mathf.Max(0f, remaining):F1}s";
            yield return null;
        }

        if (waitExtra != null)
        {
            while (waitExtra())
            {
                _cooldownText.text = "Waiting for ball...";
                yield return null;
            }
        }

        _cooldownCanvas.gameObject.SetActive(false);
    }

    // ── pulsing ready indicator ───────────────────────────────────────────────

    void Update()
    {
        if (_readyCanvas == null || !_readyCanvas.gameObject.activeSelf || _readyBg == null)
            return;
        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        _readyBg.color = new Color(0.08f, 0.88f, 0.08f, Mathf.Lerp(0.58f, 0.96f, pulse));
    }

    // ── UI builders ───────────────────────────────────────────────────────────

    void BuildFlashUI()
    {
        var root   = new GameObject("SwingFlashCanvas");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        root.AddComponent<CanvasScaler>();

        var go = new GameObject("Flash");
        go.transform.SetParent(root.transform, false);
        _flashImage               = go.AddComponent<Image>();
        _flashImage.color         = Color.clear;
        _flashImage.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void BuildCooldownUI()
    {
        var root = new GameObject("CooldownCanvas");
        _cooldownCanvas              = root.AddComponent<Canvas>();
        _cooldownCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _cooldownCanvas.sortingOrder = 60;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Dark pill — top right corner
        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        var bgImg         = bg.AddComponent<Image>();
        bgImg.color       = new Color(0f, 0f, 0f, 0.72f);
        bgImg.raycastTarget = false;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin        = new Vector2(1f, 1f);
        bgRt.anchorMax        = new Vector2(1f, 1f);
        bgRt.pivot            = new Vector2(1f, 1f);
        bgRt.sizeDelta        = new Vector2(290f, 52f);
        bgRt.anchoredPosition = new Vector2(-20f, -20f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(bg.transform, false);
        _cooldownText              = textGO.AddComponent<TextMeshProUGUI>();
        _cooldownText.fontSize     = 20f;
        _cooldownText.alignment    = TextAlignmentOptions.Center;
        _cooldownText.color        = new Color(1f, 0.85f, 0.2f, 1f);
        _cooldownText.raycastTarget = false;
        var tRt = textGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(8f, 0f);
        tRt.offsetMax = new Vector2(-8f, 0f);

        root.SetActive(false);
    }

    void BuildReadyUI()
    {
        var root = new GameObject("ReadyCanvas");
        _readyCanvas              = root.AddComponent<Canvas>();
        _readyCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _readyCanvas.sortingOrder = 60;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Pulsing green pill — top centre
        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        _readyBg              = bg.AddComponent<Image>();
        _readyBg.raycastTarget = false;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin        = new Vector2(0.5f, 1f);
        bgRt.anchorMax        = new Vector2(0.5f, 1f);
        bgRt.pivot            = new Vector2(0.5f, 1f);
        bgRt.sizeDelta        = new Vector2(380f, 58f);
        bgRt.anchoredPosition = new Vector2(0f, -95f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(bg.transform, false);
        var txt              = textGO.AddComponent<TextMeshProUGUI>();
        txt.text             = "READY — SWING NOW  ↓";
        txt.fontSize         = 23f;
        txt.fontStyle        = FontStyles.Bold;
        txt.alignment        = TextAlignmentOptions.Center;
        txt.color            = Color.white;
        txt.raycastTarget    = false;
        var tRt = textGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        root.SetActive(false);
    }
}
