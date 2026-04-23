using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class GolfGameManager : MonoBehaviour
{
    public static GolfGameManager Instance { get; private set; }

    public Vector3 waterPenaltyPosition = new Vector3(320f, 105f, 246f);

    private int _strokes;
    private int _penalties;
    public int TotalPar => _strokes + _penalties;

    private GameObject _gameOverCanvas;
    private TMP_Text _statsText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        BuildGameOverUI();
    }

    public void RecordStroke()
    {
        _strokes++;
        Debug.Log($"[GolfGameManager] Stroke recorded. Total strokes: {_strokes}");
    }

    public void AddWaterPenalty(GolfSwingController swing)
    {
        _penalties++;
        Debug.Log($"[GolfGameManager] Water penalty added. Total penalties: {_penalties}");
        swing.PenaltyRespawn(waterPenaltyPosition);
    }

    public void HoleComplete()
    {
        if (_statsText != null)
            _statsText.text = $"Strokes: {_strokes}  |  Penalties: {_penalties}  |  Par: {TotalPar}";

        _gameOverCanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log($"[GolfGameManager] Hole complete! Strokes: {_strokes}, Penalties: {_penalties}, Par: {TotalPar}");
    }

    private void BuildGameOverUI()
    {
        // EventSystem
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // Canvas
        _gameOverCanvas = new GameObject("GameOverCanvas");
        var canvas = _gameOverCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        _gameOverCanvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _gameOverCanvas.AddComponent<GraphicRaycaster>();

        // Dark overlay
        var overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(_gameOverCanvas.transform, false);
        var overlayImg = overlayGO.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.75f);
        overlayImg.raycastTarget = true;
        var overlayRect = overlayGO.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        // Title text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(_gameOverCanvas.transform, false);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "HOLE COMPLETE!";
        titleText.fontSize = 72;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.55f);
        titleRect.anchorMax = new Vector2(1f, 0.85f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Stats text
        var statsGO = new GameObject("StatsText");
        statsGO.transform.SetParent(_gameOverCanvas.transform, false);
        _statsText = statsGO.AddComponent<TextMeshProUGUI>();
        _statsText.text = "Strokes: 0  |  Penalties: 0  |  Par: 0";
        _statsText.fontSize = 36;
        _statsText.color = Color.white;
        _statsText.alignment = TextAlignmentOptions.Center;
        var statsRect = statsGO.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 0.4f);
        statsRect.anchorMax = new Vector2(1f, 0.55f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;

        // Play Again button
        var buttonGO = new GameObject("PlayAgainButton");
        buttonGO.transform.SetParent(_gameOverCanvas.transform, false);
        var buttonImg = buttonGO.AddComponent<Image>();
        buttonImg.color = new Color(0.18f, 0.65f, 0.23f, 1f);
        buttonImg.sprite = CreateRoundedSprite();
        buttonImg.type = Image.Type.Sliced;
        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImg;
        button.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
        var buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.35f, 0.25f);
        buttonRect.anchorMax = new Vector2(0.65f, 0.37f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        var btnLabelGO = new GameObject("Label");
        btnLabelGO.transform.SetParent(buttonGO.transform, false);
        var btnLabel = btnLabelGO.AddComponent<TextMeshProUGUI>();
        btnLabel.text = "Play Again";
        btnLabel.fontSize = 32;
        btnLabel.color = Color.white;
        btnLabel.alignment = TextAlignmentOptions.Center;
        var labelRect = btnLabelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        _gameOverCanvas.SetActive(false);
    }

    private Sprite CreateRoundedSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size);
        var pixels = new Color[size * size];
        float r = size * 0.5f;
        float cr = size * 0.2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, cr, size - cr - 1);
                float cy = Mathf.Clamp(y, cr, size - cr - 1);
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                pixels[y * size + x] = dist <= cr ? Color.white : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(cr, cr, cr, cr));
    }
}
