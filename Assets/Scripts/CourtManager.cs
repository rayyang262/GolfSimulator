using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Place this script in both PracticeCourt and RealCourt scenes.
/// It knows which mode is active and provides a Back button to return to the intro.
/// </summary>
public class CourtManager : MonoBehaviour
{
    public enum CourtMode { Practice, RealCourt }

    [Header("Court Settings")]
    public CourtMode courtMode = CourtMode.Practice;

    [Header("Navigation")]
    [Tooltip("Name of the intro/main menu scene")]
    public string introSceneName = "SampleScene";

    // ---------------------------------------------------------------
    void Start()
    {
        Debug.Log($"[CourtManager] Loaded into: {courtMode} mode.");

        // You can use courtMode here to configure lighting, objects, etc.
        // e.g. if (courtMode == CourtMode.Practice) { SetupPracticeEnvironment(); }
    }

    // ---------------------------------------------------------------
    /// <summary>
    /// Call from a Back / Exit button to return to the main intro screen.
    /// </summary>
    public void GoBackToIntro()
    {
        SceneManager.LoadScene(introSceneName);
    }
}
