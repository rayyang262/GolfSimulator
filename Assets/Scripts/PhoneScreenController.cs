using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the phone screen UI: live camera feed, time/FG display,
/// and tap-to-toggle settings overlay.
/// Attach to the phone's World Space Canvas.
/// </summary>
public class PhoneScreenController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The main camera whose view is shown on the phone")]
    public Camera mainCamera;

    [Header("UI Elements (auto-found by name if left empty)")]
    public RawImage phoneFeed;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI fgText;
    public TextMeshProUGUI csText;
    public GameObject settingsOverlay;
    public Button screenTapArea;

    [Header("Display Library Overlay")]
    [Tooltip("One of the 4 settings buttons that opens the Display Library")]
    public Button displayLibraryButton;
    [Tooltip("The Display Library panel (shown on top of SettingsOverlay). Tapping outside its rect closes it.")]
    public GameObject displayLibraryOverlay;

    [Header("Render Texture Settings")]
    public int textureWidth = 512;
    public int textureHeight = 1024;

    private RenderTexture phoneFeedRT;
    private bool settingsVisible = false;
    private Renderer phoneScreenRenderer;
    private PhoneAnimator phoneAnimator;

    void Start()
    {
        // Auto-find UI elements if not assigned
        if (phoneFeed == null) phoneFeed = FindInChildren<RawImage>("PhoneFeed");
        if (timeText == null) timeText = FindInChildren<TextMeshProUGUI>("TimeText");
        if (fgText == null) fgText = FindInChildren<TextMeshProUGUI>("FGText");
        if (csText == null) csText = FindInChildren<TextMeshProUGUI>("CSText");
        if (settingsOverlay == null)
        {
            var t = FindInChildren<Transform>("SettingsOverlay");
            if (t != null) settingsOverlay = t.gameObject;
        }
        if (screenTapArea == null) screenTapArea = FindInChildren<Button>("ScreenTapArea");

        // Find main camera if not assigned
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Assign Event Camera so World Space canvas receives clicks
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && mainCamera != null)
            canvas.worldCamera = mainCamera;

        // Disable GraphicRaycaster so EventSystem doesn't double-fire clicks
        // that we already handle manually in Update().
        var gr = GetComponent<GraphicRaycaster>();
        if (gr != null) gr.enabled = false;

        // Set up live camera feed
        SetupCameraFeed();

        // Hide settings overlay on start
        if (settingsOverlay != null)
            settingsOverlay.SetActive(false);

        // Hide display library overlay on start and wire its opener button
        if (displayLibraryOverlay != null)
            displayLibraryOverlay.SetActive(false);

        if (displayLibraryButton != null)
            displayLibraryButton.onClick.AddListener(OpenDisplayLibrary);

        // Initial text
        if (fgText != null)
            fgText.text = "FG%: 0/0";
        if (csText != null)
            csText.text = "CS: 0";

        phoneAnimator = FindFirstObjectByType<PhoneAnimator>();

        // Auto-find phone screen mesh for click detection
        var rtSetup = FindFirstObjectByType<PhoneRenderTextureSetup>();
        if (rtSetup != null && rtSetup.phoneScreen != null)
            phoneScreenRenderer = rtSetup.phoneScreen;
        if (phoneScreenRenderer == null && transform.parent != null)
            phoneScreenRenderer = transform.parent.GetComponentInChildren<MeshRenderer>();
    }

    void Update()
    {
        // Update time display
        if (timeText != null)
            timeText.text = System.DateTime.Now.ToString("H:mm tt");

        if (phoneAnimator == null || !phoneAnimator.IsPhoneUp) return;
        if (UnityEngine.InputSystem.Mouse.current == null) return;
        if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        // When display library is open, handle all clicks inside it manually
        // (WorldSpace canvas EventSystem is unreliable for these buttons).
        if (displayLibraryOverlay != null && displayLibraryOverlay.activeSelf)
        {
            // Use local-space projection for each button — more reliable than
            // RectangleContainsScreenPoint on large WorldSpace canvas rects.
            Button bestBtn = null;
            float bestArea = float.MaxValue;
            foreach (var btn in displayLibraryOverlay.GetComponentsInChildren<Button>(true))
            {
                if (!btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                var r = btn.GetComponent<RectTransform>();
                if (r == null) continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, mousePos, mainCamera, out Vector2 local)) continue;
                if (!r.rect.Contains(local)) continue;
                float area = r.rect.width * r.rect.height;
                if (area < bestArea) { bestArea = area; bestBtn = btn; }
            }

            if (bestBtn != null)
            {
                bestBtn.onClick.Invoke();
                return;
            }

            // No button hit — close if click is outside the overlay panel
            bool insidePanel = displayLibraryOverlay.transform is RectTransform panelRect &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, mousePos, mainCamera, out Vector2 panelLocal) &&
                panelRect.rect.Contains(panelLocal);

            if (!insidePanel)
                CloseDisplayLibrary();

            return;
        }

        // Check displayLibraryButton (opens the overlay)
        if (displayLibraryButton != null && displayLibraryButton.gameObject.activeInHierarchy)
        {
            var btnRect = displayLibraryButton.GetComponent<RectTransform>();
            if (btnRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(btnRect, mousePos, mainCamera))
            {
                OpenDisplayLibrary();
                return;
            }
        }

        if (phoneScreenRenderer == null) return;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (phoneScreenRenderer.bounds.IntersectRay(ray) && !IsPointerOverSelectable(mousePos))
        {
            OnScreenTapped();
        }
    }

    void OnScreenTapped()
    {
        // If the library is open, a tap outside its panel closes it and
        // consumes the tap so it doesn't also toggle SettingsOverlay.
        if (displayLibraryOverlay != null && displayLibraryOverlay.activeSelf)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            // If the pointer is over a button or interactive element, let the click through.
            if (IsPointerOverSelectable(mousePos))
                return;

            var panelRect = displayLibraryOverlay.transform as RectTransform;
            if (panelRect == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(panelRect, mousePos, mainCamera))
            {
                CloseDisplayLibrary();
            }
            return;
        }

        if (settingsOverlay == null) return;
        settingsVisible = !settingsVisible;
        settingsOverlay.SetActive(settingsVisible);
    }

    void OpenDisplayLibrary()
    {
        if (displayLibraryOverlay != null)
        {
            displayLibraryOverlay.SetActive(true);
            displayLibraryOverlay.transform.SetAsLastSibling();
        }
    }

    // Returns true if the pointer is over a clickable UI element (Button, Toggle, etc.),
    // excluding the full-screen ScreenTapArea which is itself a Button used as a tap-catcher.
    bool IsPointerOverSelectable(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        // Explicitly check the display library button rect so it isn't blocked
        // by the phone-screen tap handler closing the SettingsOverlay first.
        if (displayLibraryButton != null)
        {
            var btnRect = displayLibraryButton.GetComponent<RectTransform>();
            if (btnRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(btnRect, screenPos, mainCamera))
                return true;
        }

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var r in results)
        {
            var sel = r.gameObject.GetComponentInParent<Selectable>();
            if (sel == null) continue;
            if (screenTapArea != null && sel == screenTapArea) continue;
            return true;
        }
        return false;
    }

    void CloseDisplayLibrary()
    {
        if (displayLibraryOverlay != null)
            displayLibraryOverlay.SetActive(false);
    }

    void SetupCameraFeed()
    {
        if (mainCamera == null || phoneFeed == null) return;

        // Match render texture aspect ratio to the phone feed RawImage
        RectTransform feedRect = phoneFeed.GetComponent<RectTransform>();
        if (feedRect != null)
        {
            float aspect = feedRect.rect.width / feedRect.rect.height;
            textureWidth = Mathf.RoundToInt(textureHeight * aspect);
        }

        // Create a render texture for the camera feed
        phoneFeedRT = new RenderTexture(textureWidth, textureHeight, 16);
        phoneFeedRT.name = "PhoneFeedRT";

        // We can't steal the main camera's target, so create a second camera
        GameObject feedCamObj = new GameObject("PhoneFeedCamera");
        Camera feedCam = feedCamObj.AddComponent<Camera>();
        feedCam.CopyFrom(mainCamera);
        feedCam.targetTexture = phoneFeedRT;
        feedCam.depth = -10;

        // Parent it to main camera so it follows
        feedCamObj.transform.SetParent(mainCamera.transform, false);
        feedCamObj.transform.localPosition = Vector3.zero;
        feedCamObj.transform.localRotation = Quaternion.identity;

        // Don't render UI layer on the feed camera
        feedCam.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

        // Assign to the RawImage
        phoneFeed.texture = phoneFeedRT;
    }

    void OnDestroy()
    {
        if (phoneFeedRT != null)
        {
            phoneFeedRT.Release();
            Destroy(phoneFeedRT);
        }
    }

    /// <summary>Update the FG% display from external scripts.</summary>
    public void UpdateFG(int made, int total)
    {
        if (fgText != null)
            fgText.text = $"FG%: {made}/{total}";
    }

    /// <summary>Update the CS (current score) display.</summary>
    public void UpdateCS(int strokes)
    {
        if (csText != null)
            csText.text = $"CS: {strokes}";
    }

    T FindInChildren<T>(string objName) where T : Component
    {
        foreach (var child in GetComponentsInChildren<T>(true))
        {
            if (child.gameObject.name == objName)
                return child;
        }
        return null;
    }
}
