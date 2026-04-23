using UnityEngine;

/// <summary>
/// Accelerometer-only swing controller for TouchOSC MK1 (no gyroscope needed).
///
/// Uses /accxyz for TWO things simultaneously:
///   1. ORIENTATION — smoothed (low-pass) gravity vector → pitch/roll → club rotation mirrors phone
///   2. SWING DETECTION — raw magnitude spike above threshold → impact → ball launches
///
/// TOUCHOSC MK1: Settings ⚙ → Accelerometer (/accxyz) ON
///               Connections → Host = PC WiFi IP, Port outgoing = 9000
/// </summary>
[RequireComponent(typeof(OscReceiver))]
[RequireComponent(typeof(GolfSwingController))]
public class TouchOscSwingController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────

    [Header("References")]
    public GolfSwingController swingController;

    [Header("OSC Address")]
    public string accelAddress = "/accxyz";

    [Header("Orientation Tracking")]
    [Tooltip("How fast the club follows the phone tilt. Higher = more responsive, lower = smoother.")]
    [Range(1f, 25f)]
    public float orientationSmoothing = 8f;

    [Tooltip("Scales forward/back tilt → backswing angle. Flip to -1 if club goes the wrong way.")]
    public float pitchMultiplier = 1f;

    [Tooltip("Scales left/right tilt → club face angle. Set 0 to disable.")]
    public float rollMultiplier = 0.4f;

    [Header("Swing Detection")]
    [Tooltip("g-magnitude above which we start tracking a swing (filters normal movement).")]
    public float activationThreshold = 1.4f;   // start tracking when mag > 1.4g

    [Tooltip("Peak g-magnitude required for a valid hit.")]
    public float swingThreshold = 2.0f;

    [Tooltip("g-magnitude that maps to maximum swing power.")]
    public float maxAccelG = 5.5f;

    [Tooltip("Seconds after activation before swing auto-resets if no impact detected (whiff timeout).")]
    public float maxSwingWindow = 1.5f;

    [Tooltip("Seconds after a hit before next swing can register.")]
    public float cooldownDuration = 1.5f;

    // ── raw OSC values (written by callback) ─────────────────────────────
    private float _rawAx, _rawAy, _rawAz;

    // ── smoothed values for orientation (updated in Update) ──────────────
    private float _smoothAx, _smoothAy, _smoothAz;

    // ── swing state machine ───────────────────────────────────────────────
    private enum SwingState { Idle, Active, Cooldown }
    private SwingState _state = SwingState.Idle;

    private float _peakMag;         // highest magnitude seen in current Active window
    private float _prevMag;         // magnitude from previous frame (for peak detection)
    private float _windowTimer;
    private float _cooldownTimer;

    private OscReceiver _osc;

    // ── lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        _osc = GetComponent<OscReceiver>();
        if (swingController == null)
            swingController = GetComponent<GolfSwingController>();

        if (_osc == null || swingController == null)
        {
            Debug.LogError("[TouchOSC] Missing OscReceiver or GolfSwingController on this GameObject.");
            enabled = false;
            return;
        }

        // Seed smoothing to "phone held upright" so club doesn't snap on first frame
        _rawAy = _smoothAy = -1f;

        _osc.OnMessage += OnOscMessage;
        Debug.Log("[TouchOSC] Ready — accelerometer orientation + swing detection active.\n" +
                  $"Thresholds: activate>{activationThreshold:F1}g  hit>{swingThreshold:F1}g  maxPower@{maxAccelG:F1}g");
    }

    void OnDestroy()
    {
        if (_osc != null) _osc.OnMessage -= OnOscMessage;
    }

    // ── Update: smoothing + state machine once per frame ─────────────────

    void Update()
    {
        // ── Low-pass filter: smooth out rapid motion, keep gravity direction ──
        float t = Time.deltaTime * orientationSmoothing;
        _smoothAx = Mathf.Lerp(_smoothAx, _rawAx, t);
        _smoothAy = Mathf.Lerp(_smoothAy, _rawAy, t);
        _smoothAz = Mathf.Lerp(_smoothAz, _rawAz, t);

        // ── Mirror phone orientation onto club (skip during downswing animation) ──
        if (_state != SwingState.Cooldown)
            UpdateClubOrientation();

        // ── Swing state machine ──────────────────────────────────────────
        // Raw magnitude (not smoothed) is used for detection so we catch fast spikes
        float mag = Mathf.Sqrt(_rawAx * _rawAx + _rawAy * _rawAy + _rawAz * _rawAz);

        switch (_state)
        {
            case SwingState.Idle:
                if (mag > activationThreshold)
                {
                    _state       = SwingState.Active;
                    _peakMag     = mag;
                    _prevMag     = mag;
                    _windowTimer = 0f;
                    Debug.Log($"[TouchOSC] Swing armed (mag={mag:F2}g)");
                }
                break;

            case SwingState.Active:
                _windowTimer += Time.deltaTime;

                if (mag > _peakMag) _peakMag = mag;

                // Impact = peak exceeded threshold AND magnitude is now falling off the peak
                bool hitThreshold = _peakMag >= swingThreshold;
                bool pastPeak     = mag < _prevMag * 0.88f;   // 12% drop = past the impact moment

                if (hitThreshold && pastPeak)
                {
                    float power = Mathf.Lerp(2f, swingController.maxPower,
                                             Mathf.InverseLerp(swingThreshold, maxAccelG, _peakMag));
                    Debug.Log($"[TouchOSC] Hit!  peakMag={_peakMag:F2}g  power={power:F1}");
                    swingController.PhoneTriggerHit(power);
                    EnterCooldown();
                    break;
                }

                if (_windowTimer >= maxSwingWindow)
                {
                    Debug.Log("[TouchOSC] Whiff — swing timed out.");
                    swingController.PhoneWhiff();
                    EnterCooldown();
                }

                _prevMag = mag;
                break;

            case SwingState.Cooldown:
                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer >= cooldownDuration)
                    _state = SwingState.Idle;
                break;
        }
    }

    // ── OSC callback (fires on main thread via OscReceiver.Update) ────────

    void OnOscMessage(string address, float[] values)
    {
        if (address != accelAddress || values.Length < 3) return;
        _rawAx = values[0];
        _rawAy = values[1];
        _rawAz = values[2];
    }

    // ── Orientation mapping ───────────────────────────────────────────────

    void UpdateClubOrientation()
    {
        // Normalise so only the direction of gravity matters
        float mag = Mathf.Sqrt(_smoothAx * _smoothAx + _smoothAy * _smoothAy + _smoothAz * _smoothAz);
        if (mag < 0.05f) return;

        float nx = _smoothAx / mag;
        float ny = _smoothAy / mag;
        float nz = _smoothAz / mag;

        // Pitch: forward/back tilt → backswing X rotation
        //   Phone upright portrait: ny≈-1, nz≈0 → pitch=0 (club at rest)
        //   Top of phone tilts away: nz goes negative → pitch increases → club goes back
        float pitch = Mathf.Atan2(-nz, -ny) * Mathf.Rad2Deg;

        // Roll: left/right tilt → club face Z rotation
        float roll = Mathf.Atan2(nx, -ny) * Mathf.Rad2Deg;

        swingController.PhoneSetClubOrientation(
            pitch * pitchMultiplier,
            roll  * rollMultiplier
        );
    }

    void EnterCooldown()
    {
        _state         = SwingState.Cooldown;
        _cooldownTimer = 0f;
    }
}
