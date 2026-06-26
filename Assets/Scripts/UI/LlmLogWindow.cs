using System.Text;
using AISandbox.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AISandbox.UI
{
    /// <summary>
    /// A top-right "LLM Log" button that opens a large, auto-updating window listing
    /// what was sent to the model and what came back, newest first. Reads from the
    /// global <see cref="LlmLog"/> and refreshes whenever a new call is recorded.
    /// Builds its own UI at runtime — drop this on an empty GameObject and Play.
    /// </summary>
    public class LlmLogWindow : MonoBehaviour
    {
        [Tooltip("How many recent calls to show in the window.")]
        [Min(1)] public int maxEntriesShown = 25;

        private Font _font;
        private GameObject _window;
        private Text _content;
        private RectTransform _contentRect;
        private bool _open;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUi();
            ShowWindow(false);
        }

        private void OnEnable() => LlmLog.Changed += OnLogChanged;
        private void OnDisable() => LlmLog.Changed -= OnLogChanged;

        private void OnLogChanged()
        {
            if (_open) Refresh();
        }

        private void Toggle()
        {
            _open = !_open;
            ShowWindow(_open);
            if (_open) Refresh();
        }

        private void ShowWindow(bool visible)
        {
            _open = visible;
            if (_window != null) _window.SetActive(visible);
        }

        private void Refresh()
        {
            if (_content == null) return;

            var entries = LlmLog.Entries;
            var sb = new StringBuilder();
            if (entries.Count == 0)
                sb.AppendLine("No LLM calls yet. Turn on \"Use LLM\" and run a round.");

            int shown = 0;
            for (int i = entries.Count - 1; i >= 0 && shown < maxEntriesShown; i--, shown++)
            {
                var e = entries[i];
                sb.AppendLine($"<b>[{e.time}] {e.agent}</b>");
                sb.AppendLine("SENT:");
                sb.AppendLine(e.prompt);
                sb.AppendLine("GOT:");
                sb.AppendLine(e.response);
                sb.AppendLine("──────────────────────────────");
            }

            _content.text = sb.ToString();
            // Resize content to fit so the ScrollRect can scroll.
            _content.rectTransform.sizeDelta = new Vector2(_content.rectTransform.sizeDelta.x, _content.preferredHeight + 16f);
        }

        // ---- UI construction ----------------------------------------------------

        private void BuildUi()
        {
            var canvasGo = new GameObject("LlmLogCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; // above the HUD
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildButton(canvasGo.transform);
            BuildWindow(canvasGo.transform);
        }

        private void BuildButton(Transform parent)
        {
            var btnGo = new GameObject("LlmLogButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -80f); // just below the turn-controls panel
            rect.sizeDelta = new Vector2(120f, 26f);
            btnGo.GetComponent<Image>().color = new Color(0.28f, 0.4f, 0.6f);
            btnGo.GetComponent<Button>().onClick.AddListener(Toggle);

            var label = AddText(btnGo.transform, "Label", "LLM Log", TextAnchor.MiddleCenter, 13, FontStyle.Bold);
            Stretch(label.rectTransform);
        }

        private void BuildWindow(Transform parent)
        {
            _window = new GameObject("LlmLogWindow", typeof(RectTransform), typeof(Image));
            _window.transform.SetParent(parent, false);
            var rect = _window.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _window.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.96f);

            // Title.
            var title = AddText(_window.transform, "Title", "LLM Call Log  (newest first)", TextAnchor.MiddleLeft, 15, FontStyle.Bold);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0f, 1f);
            tr.offsetMin = new Vector2(12f, -34f); tr.offsetMax = new Vector2(-90f, -6f);

            // Close button.
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_window.transform, false);
            var cr = closeGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(1f, 1f); cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-8f, -6f); cr.sizeDelta = new Vector2(70f, 24f);
            closeGo.GetComponent<Image>().color = new Color(0.5f, 0.25f, 0.25f);
            closeGo.GetComponent<Button>().onClick.AddListener(() => ShowWindow(false));
            var cl = AddText(closeGo.transform, "Label", "Close", TextAnchor.MiddleCenter, 12, FontStyle.Bold);
            Stretch(cl.rectTransform);

            // Scroll area (the ScrollRect's own rect acts as the viewport, masked).
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(_window.transform, false);
            var sr = scrollGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = new Vector2(8f, 8f); sr.offsetMax = new Vector2(-8f, -40f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            // Content holding the log text.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(Text));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _contentRect = contentGo.GetComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0f, 1f);
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.offsetMin = new Vector2(6f, 0f);
            _contentRect.offsetMax = new Vector2(-6f, 0f);
            _contentRect.sizeDelta = new Vector2(0f, 800f);

            _content = contentGo.GetComponent<Text>();
            _content.font = _font;
            _content.fontSize = 12;
            _content.color = Color.white;
            _content.alignment = TextAnchor.UpperLeft;
            _content.horizontalOverflow = HorizontalWrapMode.Wrap;
            _content.verticalOverflow = VerticalWrapMode.Overflow;
            _content.supportRichText = true;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = sr;
            scroll.content = _contentRect;
        }

        // ---- helpers ------------------------------------------------------------

        private Text AddText(Transform parent, string name, string text, TextAnchor anchor, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.alignment = anchor; t.color = Color.white; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

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
    }
}
