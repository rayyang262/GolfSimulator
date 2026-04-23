using UnityEngine;
using UnityEngine.UI;
public class DisplayLibraryModeToggle : MonoBehaviour
{
    public enum Mode { Fixed, Dynamic }

    [Header("Toggle Buttons")]
    public Button fixedButton;
    public Button dynamicButton;

    [Header("Button Highlight Backgrounds")]
    [Tooltip("The Image component behind the FIXED label (the active pill highlight).")]
    public Image fixedButtonBg;
    [Tooltip("The Image component behind the DYNAMIC label (the active pill highlight).")]
    public Image dynamicButtonBg;

    [Header("Colors")]
    public Color selectedBgColor    = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color unselectedBgColor  = new Color(0f,    0f,    0f,    0f);
    public Color selectedTextColor   = Color.white;
    public Color unselectedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Content Panels")]
    [Tooltip("Panel shown when FIXED is selected (contains your StatCardToggle items).")]
    public GameObject fixedPanel;
    [Tooltip("Panel shown when DYNAMIC is selected (populate with dynamic stat widgets).")]
    public GameObject dynamicPanel;

    public Mode CurrentMode { get; private set; } = Mode.Fixed;

    void Awake()
    {
        if (fixedButton != null)   fixedButton.onClick.AddListener(() => SetMode(Mode.Fixed));
        if (dynamicButton != null) dynamicButton.onClick.AddListener(() => SetMode(Mode.Dynamic));

        SetMode(Mode.Fixed);
    }

    public void SetMode(Mode mode)
    {
        CurrentMode = mode;

        if (fixedPanel != null)   fixedPanel.SetActive(mode == Mode.Fixed);
        if (dynamicPanel != null) dynamicPanel.SetActive(mode == Mode.Dynamic);

        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        bool isFixed = CurrentMode == Mode.Fixed;

        if (fixedButtonBg != null)   fixedButtonBg.color   = isFixed ? selectedBgColor   : unselectedBgColor;
        if (dynamicButtonBg != null) dynamicButtonBg.color = isFixed ? unselectedBgColor : selectedBgColor;

        var fixedImg   = fixedButton   != null ? fixedButton.GetComponent<Image>()   : null;
        var dynamicImg = dynamicButton != null ? dynamicButton.GetComponent<Image>() : null;

        if (fixedImg != null)   fixedImg.color   = isFixed ? selectedTextColor   : unselectedTextColor;
        if (dynamicImg != null) dynamicImg.color = isFixed ? unselectedTextColor : selectedTextColor;
    }
}
