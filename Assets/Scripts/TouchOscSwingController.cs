using UnityEngine;

/// <summary>
/// Translates iPhone accelerometer data (received via TouchOSC → OscReceiver)
/// into golf swing input for GolfSwingController.
///
/// HOW TO USE:
///   1. Attach this script to PlayerCapsule alongside GolfSwingController and OscReceiver.
///   2. Assign the GolfSwingController reference in the Inspector.
///   3. On GolfSwingController, check "Use Phone Input" to disable mouse swing.
///
/// TOUCHOSC SETUP (on iPhone):
///   - Install TouchOSC from App Store (by Hexler)
///   - Create a layout → Add control → Sensor → Accelerometer
///   - Set OSC address to "/accxyz", enable X Y Z outputs (3 floats)
///   - Connections → OSC → Host = [this PC's WiFi IP] → Send Port = 9000
///   - Tap Play ▶ to start streaming
///   - Hold phone like a club handle and swing downward for impact
///
/// FINDING YOUR PC's IP:
///   Windows: open Command Prompt → type "ipconfig" → look for IPv4 Address
///   Example: 192.168.1.42
/// </summary>
[RequireComponent(typeof(OscReceiver))]
[RequireComponent(typeof(GolfSwingController))]
public class TouchOscSwingController : MonoBehaviour
{
    // ── Inspector fields ─────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("GolfSwingController on this same GameObject (auto-found if left empty)")]
    public GolfSwingController swingController;

    [Header("OSC Settings")]
    [Tooltip("Must match the OSC address set on the Accelerometer sensor in TouchOSC")]
    public string accelAddress = "/accxyz";

    [Header("Swing Detection Thresholds")]
    [Tooltip("g-units above the 1g resting baseline to start tracking a swing. " +
             "Raise this if walking around triggers false activations.")]
    public float activationThreshold = 0.40f;   // default: 1.40g starts a swing

    [Tooltip("Peak g-magnitude required to register a real hit. " +
             "Lower = easier to trigger. Raise if accidental hits occur.")]
    public float swingThreshold = 2.0f;

    [Tooltip("g-magnitude that maps to 100% (maximum) swing power.")]
    public float maxAccelG = 5.5f;

    [Tooltip("Seconds the swing can stay in ACTIVE state before auto-resetting to IDLE (whiff timeout).")]
    public float maxSwingWindow = 1.5f;

    [Tooltip("Seconds after a hit before the next swing can be registered.")]
    public float cooldownDuration = 1.5f;

    // ── swing state machine ──────────────────────────────────────────────

    private enum SwingState { Idle, Active, Cooldown }
    private SwingState _state = SwingState.Idle;

    // State timers
    private float _windowTimer;
    private float _cooldownTimer;

    // Magnitude tracking
    private float _peakMag;   // highest magnitude seen during current active swing
    private float _prevMag;   // magnitude from previous OSC message (for peak-then-drop detection)

    // ── component refs ───────────────────────────────────────────────────
    private OscReceiver _osc;

    // ── lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        _osc = GetComponent<OscReceiver>();
        if (swingController == null)
            swingController = GetComponent<GolfSwingController>();

        if (_osc == null)
        {
            Debug.LogError("[TouchOSC] OscReceiver missing — add it to the same GameObject.");
            enabled = false;
            return;
        }
        if (swingController == null)
        {
            Debug.LogError("[TouchOSC] GolfSwingController missing — add it to the same GameObject.");
            enabled = false;
            return;
        }

        _osc.OnMessage += OnOscMessage;
        Debug.Log("[TouchOSC] Ready. Swing your phone to hit the ball. " +
                  $"Thresholds: activate>{1f + activationThreshold:F1}g  hit>{swingThreshold:F1}g  maxPower@{maxAccelG:F1}g");
    }

    void OnDestroy()
    {
        if (_osc != null) _osc.OnMessage -= OnOscMessage;
    }

    void Update()
    {
        switch (_state)
        {
            case SwingState.Active:
                _windowTimer += Time.deltaTime;
                if (_windowTimer >= maxSwingWindow)
                {
                    // Phone moved but never hit hard enough — treat as a whiff
                    Debug.Log("[TouchOSC] Swing window expired — whiff.");
                    swingController.PhoneWhiff();
                    EnterCooldown();
                }
                break;

            case SwingState.Cooldown:
                _cooldownTimer += Time.deltaTime;
                if (_cooldownTimer >= cooldownDuration)
                    _state = SwingState.Idle;
                break;
        }
    }

    // ── OSC callback ─────────────────────────────────────────────────────
    // Called on the main thread by OscReceiver.Update() — safe to use Unity APIs here.

    void OnOscMessage(string address, float[] values)
    {
        if (address != accelAddress || values.Length < 3) return;

        float ax = values[0];
        float ay = values[1];
        float az = values[2];
        float mag = Mathf.Sqrt(ax * ax + ay * ay + az * az);

        // Uncomment the next line to see live accelerometer data during testing:
        // Debug.Log($"[TouchOSC] /accxyz  ax={ax:F2} ay={ay:F2} az={az:F2}  mag={mag:F2}  state={_state}");

        switch (_state)
        {
            // ── IDLE: waiting for motion to start ──────────────────────
            case SwingState.Idle:
                if (mag > 1f + activationThreshold)
                {
                    _state       = SwingState.Active;
                    _peakMag     = mag;
                    _prevMag     = mag;
                    _windowTimer = 0f;
                    Debug.Log($"[TouchOSC] Swing started (mag={mag:F2}g)");
                }
                break;

            // ── ACTIVE: tracking the swing arc ────────────────────────
            case SwingState.Active:
            {
                // Drive club backswing visual — progress from activation → threshold
                float normalized = Mathf.InverseLerp(1f + activationThreshold, swingThreshold, mag);
                swingController.PhoneSetBackswingAngle(Mathf.Clamp01(normalized));

                // Update peak
                if (mag > _peakMag) _peakMag = mag;

                // Impact detection: peak exceeded threshold AND magnitude is now falling
                bool hitThreshold = _peakMag >= swingThreshold;
                bool pastPeak     = mag < _prevMag * 0.90f;  // 10% drop = we've passed the peak

                if (hitThreshold && pastPeak)
                {
                    float maxPower = swingController.maxPower;
                    float power = Mathf.Lerp(2f, maxPower,
                                             Mathf.InverseLerp(swingThreshold, maxAccelG, _peakMag));
                    Debug.Log($"[TouchOSC] Hit! peakMag={_peakMag:F2}g  power={power:F1}");
                    swingController.PhoneTriggerHit(power);
                    EnterCooldown();
                }

                _prevMag = mag;
                break;
            }

            // ── COOLDOWN: ignore input until timer expires ─────────────
            case SwingState.Cooldown:
                break;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────

    void EnterCooldown()
    {
        _state        = SwingState.Cooldown;
        _cooldownTimer = 0f;
    }
}
