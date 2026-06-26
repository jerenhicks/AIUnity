using System.Text;
using AISandbox.Agents;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AISandbox.UI
{
    /// <summary>
    /// Click-to-select agents and inspect them.
    ///
    /// Left-click an agent to select it (a yellow outline appears). Click anywhere
    /// that isn't an agent — ground, empty space — to clear the selection. Clicks on
    /// UI (the HUD panels, this inspector) are ignored so they don't deselect.
    ///
    /// While something is selected, a panel in the lower-left shows the agent's name,
    /// stat block, position, brain, and most recent action. The panel is hidden when
    /// nothing is selected. Builds its own UI at runtime — just drop this component on
    /// an empty GameObject (e.g. "AgentInspector") and Play.
    /// </summary>
    public class AgentInspector : MonoBehaviour
    {
        [Tooltip("Max pick distance for the selection raycast.")]
        [SerializeField] private float pickDistance = 1000f;

        private Agent _selected;
        private Camera _camera;
        private Font _font;

        // Panel widgets.
        private GameObject _panel;
        private Text _titleText;
        private Text _bodyText;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPanel();
            ShowPanel(false);
        }

        private void Update()
        {
            HandleClick();
            if (_selected != null) RefreshPanel();
        }

        // ---- Selection ----------------------------------------------------------

        private void HandleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            // Ignore clicks that land on UI (HUD, this panel) — don't change selection.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, pickDistance))
                Select(hit.collider.GetComponentInParent<Agent>()); // null if not an agent → clears
            else
                Select(null); // clicked empty space
        }

        private void Select(Agent agent)
        {
            if (agent == _selected) return;

            if (_selected != null) _selected.SetSelected(false);
            _selected = agent;
            if (_selected != null) _selected.SetSelected(true);

            ShowPanel(_selected != null);
            if (_selected != null) RefreshPanel();
        }

        // ---- Panel content ------------------------------------------------------

        private void RefreshPanel()
        {
            if (_selected == null) return;

            _titleText.text = _selected.AgentId;

            var s = _selected.Stats;
            var sb = new StringBuilder();
            sb.AppendLine($"Move {s.Move}   Observe {s.Observe}   Talk {s.Talk}");
            sb.AppendLine($"Position: ({_selected.Coord.x}, {_selected.Coord.y})");
            sb.AppendLine($"Brain: {BrainLabel(_selected)}");
            sb.Append($"Last: {LastAction(_selected)}");
            _bodyText.text = sb.ToString();
        }

        private static string BrainLabel(Agent agent)
        {
            if (agent.Brain == null) return "none";
            string n = agent.Brain.GetType().Name;
            return n.EndsWith("Brain") ? n.Substring(0, n.Length - 5) : n;
        }

        private static string LastAction(Agent agent)
        {
            var history = agent.Memory?.Data?.history;
            if (history == null || history.Count == 0) return "—";
            var r = history[history.Count - 1];
            return $"R{r.round}: {r.action}";
        }

        // ---- UI construction ----------------------------------------------------

        private void BuildPanel()
        {
            var canvasGo = new GameObject("AgentInspectorCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel anchored to the lower-left.
            _panel = new GameObject("InspectorPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(12f, 12f);
            rect.sizeDelta = new Vector2(260f, 132f);
            _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // Title (agent name).
            _titleText = AddText(_panel.transform, "Title", "", TextAnchor.UpperLeft, 16, FontStyle.Bold);
            var tr = _titleText.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.offsetMin = new Vector2(12f, -34f);
            tr.offsetMax = new Vector2(-12f, -8f);

            // Body (stats etc.).
            _bodyText = AddText(_panel.transform, "Body", "", TextAnchor.UpperLeft, 13, FontStyle.Normal);
            var br = _bodyText.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(12f, 10f);
            br.offsetMax = new Vector2(-12f, -36f);
        }

        private void ShowPanel(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        private Text AddText(Transform parent, string name, string text, TextAnchor anchor, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
