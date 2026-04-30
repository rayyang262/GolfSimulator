using UnityEngine;

/// <summary>
/// Attach to any GameObject in the scene.
///
/// HOW TO USE:
///   1. Set driverPower until the driver distance feels right.
///   2. For each other club, set its percentage until that club's
///      distance feels right in-game.
///
/// IMPORTANT: "% of driver power" is NOT the same as "% of driver distance"
/// because higher loft angles send more energy upward instead of forward.
/// A 5-Iron at 220% power still lands shorter than the driver because of loft.
/// Just tune the numbers until the feel is right — ignore the percentages.
/// </summary>
public class ClubDistanceTuner : MonoBehaviour
{
    [Header("Driver — tune this first")]
    [Range(0.01f, 0.30f)]
    public float driverPower = 0.055f;

    [Header("Other clubs as % of driverPower")]
    [Tooltip("5-Iron needs much more raw power than driver because 25° loft bleeds horizontal distance.")]
    [Range(10f, 500f)] public float fiveIronPercent   = 220f;

    [Tooltip("7-Iron — slightly less power than driver.")]
    [Range(10f, 500f)] public float sevenIronPercent  = 93f;

    [Tooltip("Sand Wedge — short, high-loft approach shot.")]
    [Range(10f, 200f)] public float sandWedgePercent  = 49f;

    [Tooltip("Putter — gentle roll on the green.")]
    [Range(10f, 200f)] public float putterPercent     = 40f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start() => Apply();

#if UNITY_EDITOR
    void OnValidate()
    {
        // Apply live while the game is running so you can tune without restarting.
        if (Application.isPlaying) Apply();
    }
#endif

    // ── Apply ─────────────────────────────────────────────────────────────────

    void Apply()
    {
        var cs = FindFirstObjectByType<ClubSystem>();
        if (cs == null) { Debug.LogWarning("[ClubDistanceTuner] ClubSystem not found."); return; }

        Set(ref cs.driver,    driverPower);
        Set(ref cs.fiveIron,  driverPower * fiveIronPercent  / 100f);
        Set(ref cs.sevenIron, driverPower * sevenIronPercent / 100f);
        Set(ref cs.sandWedge, driverPower * sandWedgePercent / 100f);
        Set(ref cs.putter,    driverPower * putterPercent    / 100f);

        cs.SelectClub();

        Debug.Log($"[ClubDistanceTuner] " +
                  $"Driver={cs.driver.powerScale:F4}  " +
                  $"5i={cs.fiveIron.powerScale:F4}  " +
                  $"7i={cs.sevenIron.powerScale:F4}  " +
                  $"SW={cs.sandWedge.powerScale:F4}  " +
                  $"Putter={cs.putter.powerScale:F4}");
    }

    static void Set(ref ClubSystem.ClubData club, float powerScale)
    {
        club.powerScale = powerScale;
    }
}
