using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Controls the intro phone UI in the bottom-right corner.
/// Handles the app menu with Practice and Real Court buttons.
/// </summary>
public class PhoneMenuController : MonoBehaviour
{
    [Header("Phone UI Panels")]
    [Tooltip("The root phone frame object (bottom-right corner)")]
    public GameObject phoneFrame;

    [Tooltip("The app home screen panel (shown inside the phone)")]
    public GameObject appMenuPanel;

    [Header("Menu Buttons")]
    public Button practiceButton;
    public Button realCourtButton;

    [Header("Scene Names")]
    [Tooltip("Exact name of the Practice Court scene")]
    public string practiceSceneName = "PracticeCourt";

    [Tooltip("Exact name of the Real Court scene")]
    public string realCourtSceneName = "RealCourt";

    [Header("Transition")]
    [Tooltip("Optional loading overlay that fades in before scene load")]
    public CanvasGroup loadingOverlay;

    [Tooltip("Duration of fade transition in seconds")]
    public float fadeDuration = 0.5f;

    // ---------------------------------------------------------------
    void Start()
    {
        // Make sure phone is visible on start
        if (phoneFrame != null)
            phoneFrame.SetActive(true);

        if (appMenuPanel != null)
            appMenuPanel.SetActive(true);

        // Hide loading overlay
        if (loadingOverlay != null)
            loadingOverlay.alpha = 0f;

        // Wire up buttons
        if (practiceButton != null)
            practiceButton.onClick.AddListener(OnPracticePressed);

        if (realCourtButton != null)
            realCourtButton.onClick.AddListener(OnRealCourtPressed);
    }

    // ---------------------------------------------------------------
    public void OnPracticePressed()
    {
        Debug.Log("[PhoneMenu] Practice button tapped.");
        StartCoroutine(LoadSceneWithFade(practiceSceneName));
    }

    public void OnRealCourtPressed()
    {
        Debug.Log("[PhoneMenu] Real Court button tapped.");
        StartCoroutine(LoadSceneWithFade(realCourtSceneName));
    }

    // ---------------------------------------------------------------
    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        // Fade out if overlay exists
        if (loadingOverlay != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                loadingOverlay.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            loadingOverlay.alpha = 1f;
        }

        // Load the target scene
        SceneManager.LoadScene(sceneName);
    }
}
