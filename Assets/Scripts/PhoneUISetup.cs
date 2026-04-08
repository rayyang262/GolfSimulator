using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to IntroCanvas — auto-fixes all phone UI layout, text, and fonts.
/// Right-click the component header and choose "Apply Layout" to reapply anytime.
/// </summary>
[ExecuteAlways]
public class PhoneUISetup : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public RectTransform phoneFrame;
    public RectTransform appScreen;
    public RectTransform appMenu;
    public RectTransform practiceButton;
    public RectTransform realCourtButton;
    public RectTransform titleText;

    [Header("Phone Size")]
    public float phoneWidth  = 200f;
    public float phoneHeight = 390f;

    void OnValidate()
    {
        // Defer Apply() so it runs outside of OnValidate (avoids SendMessage error)
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
    }
    void Start() => Apply();

    [ContextMenu("Apply Layout")]
    public void Apply()
    {
        if (phoneFrame      == null) phoneFrame      = FindRect("PhoneFrame");
        if (appScreen       == null) appScreen       = FindRect("AppScreen");
        if (appMenu         == null) appMenu         = FindRect("App Menu");
        if (practiceButton  == null) practiceButton  = FindRect("PracticeButton");
        if (realCourtButton == null) realCourtButton = FindRect("RealCourtButton");
        if (titleText       == null) titleText       = FindRect("Golf Simulator");

        // ── PhoneFrame ── anchored to bottom-right ──────────────────────
        if (phoneFrame != null)
        {
            phoneFrame.sizeDelta        = new Vector2(phoneWidth, phoneHeight);
            phoneFrame.localScale       = Vector3.one;
            SetColor(phoneFrame, new Color32(28, 28, 30, 255), rounded: true);
        }

        // ── AppScreen ── fills phone with bezel ────────────────────────
        if (appScreen != null)
        {
            Stretch(appScreen, 8, 8, 28, 8);
            appScreen.localScale = Vector3.one;
            SetColor(appScreen, new Color32(0, 0, 0, 255));
            var vlg = appScreen.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
        }

        // ── App Menu ── fills AppScreen, vertical layout ────────────────
        if (appMenu != null)
        {
            Stretch(appMenu, 0, 0, 0, 0);
            appMenu.localScale = Vector3.one;
            SetColor(appMenu, new Color32(15, 30, 70, 255), rounded: true);

            var vlg = appMenu.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = appMenu.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(14, 14, 20, 14);
            vlg.spacing                = 12f;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
        }

        // ── Title Text ── top of App Menu ──────────────────────────────
        if (titleText != null && appMenu != null)
        {
            titleText.SetParent(appMenu, false);
            titleText.SetAsFirstSibling();
            titleText.sizeDelta  = new Vector2(0, 44f);
            titleText.localScale = Vector3.one;
            FixText(titleText.gameObject, "Golf Simulator", 16, true, TextAnchor.MiddleCenter);
        }

        // ── Practice Button ────────────────────────────────────────────
        if (practiceButton != null)
        {
            practiceButton.sizeDelta  = new Vector2(0, 50f);
            practiceButton.localScale = Vector3.one;
            SetColor(practiceButton, new Color32(46, 160, 67, 255));
            FixButtonText(practiceButton.gameObject, "Practice");
        }

        // ── Real Court Button ──────────────────────────────────────────
        if (realCourtButton != null)
        {
            realCourtButton.sizeDelta  = new Vector2(0, 50f);
            realCourtButton.localScale = Vector3.one;
            SetColor(realCourtButton, new Color32(37, 99, 235, 255));
            FixButtonText(realCourtButton.gameObject, "Real Court");
        }
    }

    // ── Fix text inside a button (searches child named "Text" or "Text (TMP)") ──
    static void FixButtonText(GameObject btn, string label)
    {
        // Try TMP first
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text      = label;
            tmp.fontSize  = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            return;
        }
        // Fallback: Legacy Text
        var leg = btn.GetComponentInChildren<Text>();
        if (leg != null)
        {
            leg.text      = label;
            leg.fontSize  = 14;
            leg.alignment = TextAnchor.MiddleCenter;
            leg.color     = Color.white;
        }
    }

    // ── Fix a standalone text object ───────────────────────────────────
    static void FixText(GameObject go, string label, int size, bool bold, TextAnchor align)
    {
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text      = label;
            tmp.fontSize  = size;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            return;
        }
        var leg = go.GetComponent<Text>();
        if (leg != null)
        {
            leg.text      = label;
            leg.fontSize  = size;
            leg.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            leg.alignment = align;
            leg.color     = Color.white;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────
    static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left,    bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void SetColor(RectTransform rt, Color32 color, bool rounded = false)
    {
        var img = rt.GetComponent<Image>();
        if (img == null) return;
        img.color = color;

        if (rounded)
        {
            // Use Unity's built-in rounded Background sprite
            if (img.sprite == null || img.sprite.name != "Background")
            {
                var spr = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
                if (spr != null)
                {
                    img.sprite    = spr;
                    img.type      = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 1f;
                }
            }
        }
    }

    RectTransform FindRect(string objName)
    {
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            if (rt.name == objName) return rt;
        return null;
    }
}
