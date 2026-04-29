using UnityEngine;

/// <summary>
/// Backswing-first swing controller for TouchOSC MK1 accelerometer (/accxyz).
///
/// STATE MACHINE:
///   IDLE
///     │  Phone tilts back past backswingPitchThreshold (club "cocked")
///     ▼
///   BACKSWING — held for minBackswingHold seconds
///     │  Pitch drops below backswingResetThreshold → back to IDLE (abort)
///     ▼
///   ARMED — "READY — SWING NOW" banner pulses on screen
///     │  Pitch drops before swing (abort) → IDLE
///     │  Magnitude spike ≥ swingThreshold AND starts falling →
///     ▼
///   HIT — PhoneTriggerHit(power), impact effects fire
///     ▼
///   COOLDOWN — countdown timer on screen; locked until timer done
///              AND GolfSwingController.ballInFlight == false
///     ▼
///   IDLE
/// </summary>
[RequireComponent(typeof(OscReceiver))]
[RequireComponent(typeof(GolfSwingController))]
public class TouchOscSwingController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    public GolfSwingController swingController;

    [Header("OSC Address")]
    public string accelAddress = "/accxyz";

    [Header("Orientation Tracking")]
    [Range(1f, 25f)]
    public float orientationSmoothing = 8f;
    [Tooltip("Flip to -1 if the club tilts the wrong way.")]
    public float pitchMultiplier = 1f;
    public float rollMultiplier  = 0.4f;

    [Header("Backswing Detection")]
    [Tooltip("Pitch angle (°) the phone must reach to count as cocked.")]
    public float backswingPitchThreshold  = 35f;
    [Tooltip("Seconds phone must hold that angle before arming.")]
    public float minBackswingHold         = 0.15f;
    [Tooltip("Pitch (°) below which backswing is considered aborted.")]
    public float backswingResetThreshold  = 18f;

    [Header("Swing Detection")]
    public float swingThreshold  = 2.0f;   // g at which a swing is a valid hit
    public float maxAccelG       = 5.5f;   // g that maps to max power
    public float maxSwingWindow  = 1.5f;   // seconds before auto-whiff

    [Header("Cooldown")]
    public float cooldownDuration = 2.0f;  // min seconds between swings

    // ── private ───────────────────────────────────────────────────────────────

    private float _rawAx, _rawAy, _rawAz;
    private float _smoothAx, _smoothAy, _smoothAz;

    private enum SwingState { Idle, Backswing, Armed, Cooldown }
    private SwingState _state = SwingState.Idle;

    private float _backswingTimer;
    private float _peakMag;
    private float _prevMag;
    private float _windowTimer;
    private float _cooldownTimer;

    private OscReceiver        _osc;
    private SwingImpactEffects _effects;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _osc = GetComponent<OscReceiver>();
        if (swingController == null)
            swingController = GetComponent<GolfSwingController>();

        _effects = GetComponent<SwingImpactEffects>();
        if (_effects == null) _effects = FindFirstObjectByType<SwingImpactEffects>();

        if (_osc == null || swingController == null)
        {
            Debug.LogError("[TouchOSC] Missing OscReceiver or GolfSwingController.");
            enabled = false;
            return;
        }

        _rawAy = _smoothAy = -1f;   // seed: phone held upright
        _osc.OnMessage += OnOscMessage;

        Debug.Log("[TouchOSC] Backswing-first swing detection ready.\n" +
                  $"  Cock phone back >{backswingPitchThreshold:F0}° → hold {minBackswingHold:F2}s → swing!\n" +
                  $"  Hit threshold: {swingThreshold:F1}g  |  Cooldown: {cooldownDuration:F1}s");
    }

    void OnDestroy()
    {
        if (_osc != null) _osc.OnMessage -= OnOscMessage;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        // Smooth accelerometer → stable gravity direction for orientation
        float t = Time.deltaTime * orientationSmoothing;
        _smoothAx = Mathf.Lerp(_smoothAx, _rawAx, t);
        _smoothAy = Mathf.Lerp(_smoothAy, _rawAy, t);
        _smoothAz = Mathf.Lerp(_smoothAz, _rawAz, t);

        // Mirror phone tilt onto club (skip during cooldown so club rests)
        if (_state != SwingState.Cooldown)
            UpdateClubOrientation();

        float pitch = GetPitch();
        float mag   = Mathf.Sqrt(_rawAx * _rawAx + _rawAy * _rawAy + _rawAz * _rawAz);

        // ── Stance guard ──────────────────────────────────────────────────────
        // If the player is not locked into stance (hasn't pressed B), cancel any
        // active backswing / armed state and stay in Idle.
        if (!swingController.readyToHit && _state != SwingState.Cooldown)
        {
            if (_state == SwingState.Backswing || _state == SwingState.Armed)
            {
                _effects?.SetReadyIndicator(false);
                _state = SwingState.Idle;
                Debug.Log("[TouchOSC] Stance lost — swing cancelled. Press B to get into position.");
            }
        }

        switch (_state)
        {
            // ── IDLE ─────────────────────────────────────────────────────────
            case SwingState.Idle:
                // Only start tracking the backswing once the player is in stance
                if (!swingController.readyToHit) break;
                if (pitch > backswingPitchThreshold)
                {
                    _state          = SwingState.Backswing;
                    _backswingTimer = 0f;
                    Debug.Log($"[TouchOSC] Backswing detected (pitch={pitch:F1}°) — hold...");
                }
                break;

            // ── BACKSWING ────────────────────────────────────────────────────
            case SwingState.Backswing:
                if (pitch > backswingPitchThreshold)
                {
                    _backswingTimer += Time.deltaTime;
                    if (_backswingTimer >= minBackswingHold)
                    {
                        _state       = SwingState.Armed;
                        _peakMag     = mag;
                        _prevMag     = mag;
                        _windowTimer = 0f;
                        _effects?.SetReadyIndicator(true);
                        Debug.Log("[TouchOSC] ARMED — swing now!");
                    }
                }
                else if (pitch < backswingResetThreshold)
                {
                    _state = SwingState.Idle;   // aborted before arming
                }
                break;

            // ── ARMED ────────────────────────────────────────────────────────
            case SwingState.Armed:
                _windowTimer += Time.deltaTime;
                if (mag > _peakMag) _peakMag = mag;

                bool hitThreshold = _peakMag >= swingThreshold;
                bool pastPeak     = mag < _prevMag * 0.88f;  // 12% drop = past impact

                if (hitThreshold && pastPeak)
                {
                    _effects?.SetReadyIndicator(false);
                    float power = Mathf.Lerp(2f, swingController.maxPower,
                                             Mathf.InverseLerp(swingThreshold, maxAccelG, _peakMag));

                    Debug.Log($"[TouchOSC] HIT! peakMag={_peakMag:F2}g  power={power:F2}");
                    swingController.PhoneTriggerHit(power);
                    _effects?.PlayImpactEffects(power, swingController.maxPower);
                    EnterCooldown();
                    break;
                }

                if (_windowTimer >= maxSwingWindow)
                {
                    _effects?.SetReadyIndicator(false);
                    Debug.Log("[TouchOSC] Swing window expired — whiff.");
                    swingController.PhoneWhiff();
                    EnterCooldown();
                    break;
                }

                // Phone returned to neutral before swinging → silent abort
                if (pitch < backswingResetThreshold && _windowTimer > 0.3f)
                {
                    _effects?.SetReadyIndicator(false);
                    _state = SwingState.Idle;
                    Debug.Log("[TouchOSC] Swing aborted (phone returned to neutral).");
                }

                _prevMag = mag;
                break;

            // ── COOLDOWN ─────────────────────────────────────────────────────
            case SwingState.Cooldown:
                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer >= cooldownDuration && !swingController.ballInFlight)
                {
                    _state = SwingState.Idle;
                    Debug.Log("[TouchOSC] Ready for next shot.");
                }
                break;
        }
    }

    // ── OSC callback ─────────────────────────────────────────────────────────

    void OnOscMessage(string address, float[] values)
    {
        if (address != accelAddress || values.Length < 3) return;
        _rawAx = values[0];
        _rawAy = values[1];
        _rawAz = values[2];
    }

    // ── Orientation ───────────────────────────────────────────────────────────

    void UpdateClubOrientation()
    {
        float mag = Mathf.Sqrt(_smoothAx * _smoothAx + _smoothAy * _smoothAy + _smoothAz * _smoothAz);
        if (mag < 0.05f) return;
        float nx = _smoothAx / mag, ny = _smoothAy / mag, nz = _smoothAz / mag;
        float pitch = Mathf.Atan2(-nz, -ny) * Mathf.Rad2Deg;
        float roll  = Mathf.Atan2(nx,  -ny) * Mathf.Rad2Deg;
        swingController.PhoneSetClubOrientation(pitch * pitchMultiplier, roll * rollMultiplier);
    }

    float GetPitch()
    {
        float mag = Mathf.Sqrt(_smoothAx * _smoothAx + _smoothAy * _smoothAy + _smoothAz * _smoothAz);
        if (mag < 0.05f) return 0f;
        return Mathf.Atan2(-_smoothAz / mag, -_smoothAy / mag) * Mathf.Rad2Deg;
    }

    void EnterCooldown()
    {
        _state         = SwingState.Cooldown;
        _cooldownTimer = 0f;
        _effects?.StartCooldown(cooldownDuration, () => swingController.ballInFlight);
    }
}
