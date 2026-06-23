using AISandbox.Brains;
using AISandbox.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AISandbox.UI
{
    /// <summary>
    /// Builds the in-scene HUD entirely at runtime — no editor setup required.
    /// Drop this component on an empty GameObject (e.g. "HUD") and Play.
    ///
    /// Two panels are created at the top corners of the screen:
    /// <list type="bullet">
    ///   <item><b>Top-left — LLM status</b>: colored dot + short label (green = ready,
    ///         yellow = waiting on the model, red = error), plus a "Use LLM" checkbox.
    ///         The checkbox is locked while a request is in flight.</item>
    ///   <item><b>Top-right — turn controls</b>: a Play button that runs the next
    ///         round manually, a colored status label ("Agents working…" yellow /
    ///         "Agents ready" green), and an "Allow continuous actions" checkbox.
    ///         The Play button is only interactable when agents are idle, the
    ///         continuous toggle is off, and the LLM isn't blocked.</item>
    /// </list>
    ///
    /// Also ensures an <see cref="EventSystem"/> exists so the UI is clickable
    /// (Unity does not auto-create one when a Canvas is built at runtime).
    /// </summary>
    public class HudController : MonoBehaviour
    {
        // Layout constants (kept here rather than serialized so stale Inspector
        // values from earlier script versions can't override them).
        private const float PanelWidth = 220f;
        private const float PanelHeight = 70f;

        [Header("Colors")]
        [SerializeField] private Color connectedColor = new Color(0.25f, 0.78f, 0.35f); // green
        [SerializeField] private Color waitingColor = new Color(0.95f, 0.78f, 0.20f);   // yellow
        [SerializeField] private Color errorColor = new Color(0.86f, 0.27f, 0.27f);     // red
        [SerializeField] private Color unknownColor = new Color(0.55f, 0.55f, 0.55f);   // gray

        // LLM panel widgets.
        private Image _llmDot;
        private Text _llmLabel;
        private Toggle _llmToggle;

        // Turn panel widgets.
        private Button _playButton;
        private Image _playButtonBg;
        private Text _agentStatusLabel;
        private Toggle _continuousToggle;

        // External references.
        private TurnManager _turnManager;
        private Font _font;

        // ---- Lifecycle ----------------------------------------------------------

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUi();
        }

        private void OnEnable()
        {
            LlmStatus.Changed += OnLlmStatusChanged;
            BrainSelector.Changed += OnBrainSelectorChanged;
        }

        private void OnDisable()
        {
            LlmStatus.Changed -= OnLlmStatusChanged;
            BrainSelector.Changed -= OnBrainSelectorChanged;
        }

        private void Start()
        {
            // Sync LLM widgets with whatever the rest of the app already reported.
            if (_llmToggle != null) _llmToggle.SetIsOnWithoutNotify(BrainSelector.UseLlm);
            OnLlmStatusChanged(LlmStatus.Current, LlmStatus.Message);
        }

        private void Update()
        {
            // TurnManager may not exist yet on the first frame (Awake order varies).
            if (_turnManager == null)
            {
                _turnManager = FindFirstObjectByType<TurnManager>();
                if (_turnManager == null) return;
                // Sync the continuous toggle to whatever was set in the Inspector.
                if (_continuousToggle != null)
                    _continuousToggle.SetIsOnWithoutNotify(_turnManager.mode == TurnManager.RunMode.Continuous);
            }

            bool busy = _turnManager.IsBusy;
            bool continuous = _turnManager.mode == TurnManager.RunMode.Continuous;
            bool llmBlocked = BrainSelector.UseLlm && LlmStatus.Current == LlmStatus.State.Error;

            // Agents status colored text. Yellow while a round is running, green between rounds.
            if (_agentStatusLabel != null)
            {
                if (busy)
                {
                    _agentStatusLabel.text = "Agents working…";
                    _agentStatusLabel.color = waitingColor;
                }
                else
                {
                    _agentStatusLabel.text = "Agents ready";
                    _agentStatusLabel.color = connectedColor;
                }
            }

            // Play button enabled only when agents are idle, continuous is off, and the LLM isn't blocked.
            if (_playButton != null)
            {
                bool canPlay = !busy && !continuous && !llmBlocked;
                if (_playButton.interactable != canPlay)
                    _playButton.interactable = canPlay;
                // Subtle visual gray-out on the bg in addition to Unity's built-in tint.
                if (_playButtonBg != null)
                    _playButtonBg.color = canPlay ? new Color(0.28f, 0.55f, 0.85f) : new Color(0.35f, 0.35f, 0.35f);
            }
        }

        /// <summary>
        /// Create an EventSystem if one isn't present. Without it, a runtime Canvas
        /// receives no pointer events (clicks fall through to the 3D scene below).
        /// Prefer the new Input System's UI module since the rest of the project uses it.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---- UI construction ----------------------------------------------------

        private void BuildUi()
        {
            // Shared Screen Space Overlay canvas.
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildLlmPanel(canvasGo.transform);
            BuildTurnPanel(canvasGo.transform);
        }

        // ---- LLM panel (top-left) -----------------------------------------------

        private void BuildLlmPanel(Transform canvas)
        {
            var panel = CreatePanel(canvas, "LlmHudPanel", anchorLeft: true);

            // Row 1: indicator dot + status label.
            var row = CreateFullWidthRow(panel.transform, "StatusRow", topOffset: -6f, height: 24f);

            _llmDot = CreateDot(row.transform, "Indicator", unknownColor);
            _llmLabel = AddText(row.transform, "StatusLabel", "LLM: Initializing", TextAnchor.MiddleLeft, 13);
            StretchLabelRight(_llmLabel, leftOffset: 32f);

            // Row 2: "Use LLM" checkbox.
            _llmToggle = CreateCheckbox(panel.transform, "UseLlmToggle", "Use LLM", topOffset: -36f,
                isOn: BrainSelector.UseLlm, onChanged: OnLlmToggleChanged);
        }

        // ---- Turn panel (top-right) ---------------------------------------------

        private void BuildTurnPanel(Transform canvas)
        {
            var panel = CreatePanel(canvas, "TurnHudPanel", anchorLeft: false);

            // Row 1: Play button + agents status text.
            var row = CreateFullWidthRow(panel.transform, "TurnRow", topOffset: -6f, height: 26f);

            // Play button.
            var btnGo = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(row.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0.5f);
            btnRect.anchorMax = new Vector2(0f, 0.5f);
            btnRect.pivot = new Vector2(0f, 0.5f);
            btnRect.anchoredPosition = new Vector2(8f, 0f);
            btnRect.sizeDelta = new Vector2(64f, 22f);
            _playButtonBg = btnGo.GetComponent<Image>();
            _playButtonBg.color = new Color(0.28f, 0.55f, 0.85f);
            _playButton = btnGo.GetComponent<Button>();
            _playButton.targetGraphic = _playButtonBg;
            _playButton.onClick.AddListener(OnPlayClicked);

            var btnLabel = AddText(btnGo.transform, "Label", "▶ Next", TextAnchor.MiddleCenter, 12);
            var btnLabelRect = btnLabel.GetComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;

            // Status text (colored). Filled by Update() each frame.
            _agentStatusLabel = AddText(row.transform, "AgentsStatus", "Agents ready", TextAnchor.MiddleLeft, 13);
            var statusRect = _agentStatusLabel.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0f, 0.5f);
            statusRect.offsetMin = new Vector2(80f, 0f);
            statusRect.offsetMax = new Vector2(-8f, 0f);

            // Row 2: "Allow continuous actions" checkbox.
            _continuousToggle = CreateCheckbox(panel.transform, "ContinuousToggle", "Allow continuous actions",
                topOffset: -38f, isOn: false, onChanged: OnContinuousChanged);
        }

        // ---- Reusable widget builders -------------------------------------------

        private RectTransform CreatePanel(Transform parent, string name, bool anchorLeft)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            float xAnchor = anchorLeft ? 0f : 1f;
            float xPivot = anchorLeft ? 0f : 1f;
            rect.anchorMin = new Vector2(xAnchor, 1f);
            rect.anchorMax = new Vector2(xAnchor, 1f);
            rect.pivot = new Vector2(xPivot, 1f);
            rect.anchoredPosition = Vector2.zero; // flush to corner
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            return rect;
        }

        private static RectTransform CreateFullWidthRow(Transform parent, string name, float topOffset, float height)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, topOffset);
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        private static Image CreateDot(Transform parent, string name, Color color)
        {
            var dot = new GameObject(name, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(parent, false);
            var rect = dot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, 0f);
            rect.sizeDelta = new Vector2(14f, 14f);
            var img = dot.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void StretchLabelRight(Text label, float leftOffset)
        {
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(leftOffset, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
        }

        private Toggle CreateCheckbox(Transform parent, string name, string labelText, float topOffset,
            bool isOn, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var toggleGo = new GameObject(name, typeof(RectTransform));
            toggleGo.transform.SetParent(parent, false);
            var toggleRect = toggleGo.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.pivot = new Vector2(0.5f, 1f);
            toggleRect.anchoredPosition = new Vector2(0f, topOffset);
            toggleRect.sizeDelta = new Vector2(0f, 22f);

            var toggle = toggleGo.AddComponent<Toggle>();

            // Box background.
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(toggleGo.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = new Vector2(10f, 0f);
            bgRect.sizeDelta = new Vector2(16f, 16f);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.85f);

            // Checkmark — an actual ✓ glyph (not a filled square) so the "on"
            // state is unmistakable at a glance.
            var check = AddText(bg.transform, "Checkmark", "✓", TextAnchor.MiddleCenter, 16);
            check.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            check.fontStyle = FontStyle.Bold;
            var checkRect = check.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            // Label.
            var label = AddText(toggleGo.transform, "Label", labelText, TextAnchor.MiddleLeft, 12);
            StretchLabelRight(label, leftOffset: 32f);

            toggle.targetGraphic = bgImage;
            toggle.graphic = check; // Text is a Graphic; Toggle fades it in/out on state change
            toggle.isOn = isOn;
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private Text AddText(Transform parent, string name, string text, TextAnchor anchor, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---- Event handlers -----------------------------------------------------

        private void OnLlmStatusChanged(LlmStatus.State state, string message)
        {
            if (_llmDot == null || _llmLabel == null || _llmToggle == null) return;

            string body = string.IsNullOrEmpty(message) ? DefaultLlmLabel(state) : message;

            switch (state)
            {
                case LlmStatus.State.Connected:
                    _llmDot.color = connectedColor;
                    _llmToggle.interactable = true;
                    break;
                case LlmStatus.State.Waiting:
                    _llmDot.color = waitingColor;
                    _llmToggle.interactable = false; // locked while a request is in flight
                    break;
                case LlmStatus.State.Error:
                    _llmDot.color = errorColor;
                    _llmToggle.interactable = true;
                    break;
                default:
                    _llmDot.color = unknownColor;
                    _llmToggle.interactable = true;
                    break;
            }

            _llmLabel.text = $"LLM: {Truncate(body, 28)}";
        }

        private static string DefaultLlmLabel(LlmStatus.State state)
        {
            switch (state)
            {
                case LlmStatus.State.Connected: return "Ready";
                case LlmStatus.State.Waiting:   return "Thinking…";
                case LlmStatus.State.Error:     return "Error";
                default:                        return "Initializing";
            }
        }

        private void OnBrainSelectorChanged(bool useLlm)
        {
            if (_llmToggle != null && _llmToggle.isOn != useLlm)
                _llmToggle.SetIsOnWithoutNotify(useLlm);
        }

        private void OnLlmToggleChanged(bool value) => BrainSelector.SetUseLlm(value);

        private void OnPlayClicked()
        {
            if (_turnManager == null || _turnManager.IsBusy) return;
            StartCoroutine(_turnManager.StepRound());
        }

        private void OnContinuousChanged(bool value)
        {
            if (_turnManager == null) return;
            _turnManager.mode = value ? TurnManager.RunMode.Continuous : TurnManager.RunMode.Manual;
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
