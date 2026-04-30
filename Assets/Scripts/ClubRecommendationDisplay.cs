using UnityEngine;
using TMPro;

/// <summary>
/// Mirrors the active ClubSystem club to two text displays.
/// After each ball landing, picks the appropriate club based on distance to hole
/// and applies it via ClubSystem.SetNextClub() so text and actual physics always match.
///
/// Distance thresholds (tune in Inspector):
///   > fiveIronThreshold              → 5-Iron
///   > sevenIronThreshold             → 7-Iron
///   <= sevenIronThreshold, off green → Sand Wedge (short approach)
///   On green                         → Putter  (overrides all)
/// Sand Wedge zone trigger still overrides via sandWedgeOverride when player walks into bunker.
/// </summary>
public class ClubRecommendationDisplay : MonoBehaviour
{
    [Header("Text Displays")]
    public TMP_Text canvasText;
    public TMP_Text phoneText;

    [Header("Hole Target")]
    [Tooltip("Auto-found by common names if left empty.")]
    public Transform holeTransform;

    [Header("Distance Thresholds (metres)")]
    [Tooltip("Use 5-Iron when ball lands farther than this from the hole.")]
    public float fiveIronThreshold = 100f;
    [Tooltip("Use 7-Iron when ball lands farther than this but within fiveIronThreshold.")]
    public float sevenIronThreshold = 45f;
    // Ball within sevenIronThreshold but not on green → Sand Wedge (short approach)

    private GolfSwingController _swing;
    private ClubSystem          _clubs;
    private BoxCollider         _greenBox;
    private Transform           _greenTransform;
    private bool                _prevInFlight;
    private string              _lastText = "";

    void Start()
    {
        _swing = FindFirstObjectByType<GolfSwingController>();
        _clubs = FindFirstObjectByType<ClubSystem>();

        if (holeTransform == null)
        {
            foreach (var n in new[] { "Fiveth Hole", "Fifth Hole", "FifthHole", "Hole", "GolfHole" })
            {
                var go = GameObject.Find(n);
                if (go != null) { holeTransform = go.transform; break; }
            }
        }

        var green = FindFirstObjectByType<PuttingGreenTrigger>();
        if (green != null)
        {
            _greenBox       = green.GetComponent<BoxCollider>();
            _greenTransform = green.transform;
        }
        else
        {
            Debug.LogWarning("[ClubRec] PuttingGreenTrigger not found — putter auto-detect disabled.");
        }

        RefreshText();
    }

    void Update()
    {
        RefreshText();

        if (_swing == null) return;
        bool inFlight = _swing.ballInFlight;
        if (_prevInFlight && !inFlight)
            OnBallLanded();
        _prevInFlight = inFlight;
    }

    void OnBallLanded()
    {
        if (_clubs == null) return;

        GameObject ballGO = null;
        try { ballGO = GameObject.FindWithTag("GolfBall"); } catch { }
        if (ballGO == null) ballGO = GameObject.Find("GolfBall_Active");

        // Putter if ball is on the green
        if (IsBallOnGreen(ballGO))
        {
            _clubs.putterOverride = true;
            _clubs.SelectClub();
            return;
        }

        _clubs.putterOverride = false;

        // Distance-based club selection
        if (holeTransform != null && ballGO != null)
        {
            Vector3 b = ballGO.transform.position;
            Vector3 h = holeTransform.position;
            float dist = Vector2.Distance(new Vector2(b.x, b.z), new Vector2(h.x, h.z));

            ClubSystem.ClubType next;
            if      (dist > fiveIronThreshold)   next = ClubSystem.ClubType.FiveIron;
            else if (dist > sevenIronThreshold)  next = ClubSystem.ClubType.SevenIron;
            else                                 next = ClubSystem.ClubType.SandWedge;

            Debug.Log($"[ClubRec] Ball landed  dist={dist:F1}m  → {next}");
            _clubs.SetNextClub(next);
        }
        else
        {
            _clubs.SetNextClub(ClubSystem.ClubType.SevenIron);
        }
    }

    bool IsBallOnGreen(GameObject ballGO)
    {
        if (ballGO == null || _greenBox == null || _greenTransform == null) return false;
        Vector3 local = _greenTransform.InverseTransformPoint(ballGO.transform.position);
        Vector3 half  = _greenBox.size * 0.5f;
        Vector3 ctr   = _greenBox.center;
        bool result   = Mathf.Abs(local.x - ctr.x) <= half.x &&
                        Mathf.Abs(local.z - ctr.z) <= half.z;
        Debug.Log($"[ClubRec] Green check: local={local:F1}  half=({half.x:F1},_,{half.z:F1})  result={result}");
        return result;
    }

    void RefreshText()
    {
        if (_clubs == null) return;
        string text = $"Club: {_clubs.CurrentData.name}";
        if (text == _lastText) return;
        _lastText = text;
        if (canvasText != null) canvasText.text = text;
        if (phoneText  != null) phoneText.text  = text;
    }
}
