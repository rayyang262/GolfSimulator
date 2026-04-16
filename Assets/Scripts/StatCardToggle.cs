using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each stat card inside DisplayLibraryOverlay.
/// Requires a Button on the same GameObject.
/// Click toggles between default and active visuals.
/// </summary>
[RequireComponent(typeof(Button))]
public class StatCardToggle : MonoBehaviour
{
    [Header("Target Image (swap color and/or sprite)")]
    [Tooltip("The Image that changes on toggle. Usually the card's background Image.")]
    public Image background;

    [Header("Sprite swap (optional)")]
    public Sprite defaultSprite;
    public Sprite activeSprite;

    [Header("Color swap (optional — leave both white for no tint)")]
    public Color defaultColor = Color.white;
    public Color activeColor  = Color.white;

    [Header("Active indicator (optional)")]
    [Tooltip("GameObject shown only when the card is active (e.g., a highlight border or checkmark).")]
    public GameObject activeIndicator;

    public bool IsActive { get; private set; }

    /// <summary>Fires every time this card is toggled. Bool is the new state.</summary>
    public System.Action<bool> OnStateChanged;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
        ApplyVisuals();
    }

    public void Toggle()
    {
        IsActive = !IsActive;
        ApplyVisuals();
        OnStateChanged?.Invoke(IsActive);
    }

    public void SetActiveState(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        ApplyVisuals();
        OnStateChanged?.Invoke(IsActive);
    }

    void ApplyVisuals()
    {
        if (background != null)
        {
            background.color = IsActive ? activeColor : defaultColor;

            Sprite target = IsActive ? activeSprite : defaultSprite;
            if (target != null)
                background.sprite = target;
        }
        if (activeIndicator != null)
            activeIndicator.SetActive(IsActive);
    }
}
