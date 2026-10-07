using System;
using TFY.CatAdventure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TFY.CatAdventure.UI
{
    /// <summary>
    /// Builds the child-friendly UI in code (uGUI): big rounded buttons with a bounce and a click sound, bold outlined labels,
    /// icons, panels and scroll lists. Reference resolution 1080×1920 portrait, safe-area aware. Buttons are at least 120 px tall
    /// (large touch targets for small hands).
    /// </summary>
    public static class UIKit
    {
        public static Font Font;
        public static AudioService Audio;
        public static readonly Color Purple = Hex("#6C4AB6"), Pink = Hex("#FF5DA2"), Orange = Hex("#FF9F1C"), Green = Hex("#2EC4B6"),
            Blue = Hex("#3A86FF"), Yellow = Hex("#FFD23F"), Dark = Hex("#2B2D42"), Light = Hex("#FFF8F0"), Grey = Hex("#9A9AB0"), Red = Hex("#EF476F");

        public static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        public static Canvas CreateCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(parent, false);
            }
            if (!Font) Font = Resources.Load<Font>("Fonts/TFYFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fill(Transform parent, string name = "Fill") => Rect(parent, name, Vector2.zero, Vector2.one);

        /// <summary>Anchored at a point with a fixed size (pivot = anchor).</summary>
        public static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            var rt = Rect(parent, name, anchor, anchor); rt.pivot = anchor; rt.sizeDelta = size; rt.anchoredPosition = offset; return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, int radius = 36)
        {
            var rt = Fill(parent, name); var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Icons.Rounded(radius); img.type = UnityEngine.UI.Image.Type.Sliced; img.color = color; return img;
        }

        public static Image Image(RectTransform rt, Sprite sprite, Color? color = null, bool preserve = true)
        {
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite; img.color = color ?? Color.white; img.preserveAspect = preserve; img.raycastTarget = false; return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool outline = true, string name = "Label")
        {
            var rt = Fill(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(14, size / 2); t.resizeTextMaxSize = size;
            if (outline) { var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.35f); o.effectDistance = new Vector2(3, -3); }
            return t;
        }

        public static Button Button(Transform parent, string text, Color color, Action onClick, string icon = null, int fontSize = 56, string name = null)
        {
            var rt = Fill(parent, name ?? "Button:" + text);
            var bg = rt.gameObject.AddComponent<Image>(); bg.sprite = Icons.Rounded(40); bg.type = UnityEngine.UI.Image.Type.Sliced; bg.color = color;
            var shadow = rt.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, 0.3f); shadow.effectDistance = new Vector2(0, -10);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
            var cols = b.colors; cols.pressedColor = new Color(0.85f, 0.85f, 0.85f); cols.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f); b.colors = cols;
            rt.gameObject.AddComponent<Bouncy>();
            if (icon != null && string.IsNullOrEmpty(text))
            {
                var ic = Rect(rt, "Icon", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f)); Image(ic, Icons.Get(icon));
            }
            else if (icon != null)
            {
                var ic = Rect(rt, "Icon", new Vector2(0, 0.15f), new Vector2(0, 0.85f)); ic.pivot = new Vector2(0, 0.5f); ic.sizeDelta = new Vector2(0, 0); ic.offsetMin = new Vector2(30, ic.offsetMin.y); ic.offsetMax = new Vector2(30, ic.offsetMax.y);
                var fitter = ic.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                Image(ic, Icons.Get(icon));
                var l = Label(rt, text, fontSize, Color.white); ((RectTransform)l.transform).offsetMin = new Vector2(110, 0);
            }
            else Label(rt, text, fontSize, Color.white);
            b.onClick.AddListener(() => { Audio?.Sfx("sfx_button", 0.7f); Haptics.Tap(15); onClick?.Invoke(); });
            return b;
        }

        public static void SetButtonText(Button b, string text) { var t = b.GetComponentInChildren<Text>(); if (t) t.text = text; }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, int padding = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static LayoutElement Size(Component c, float height = -1, float width = -1, float flex = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>(); // (no "??": Unity's fake-null objects break it in the editor)
            if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) le.preferredHeight = le.minHeight = height;
            if (width >= 0) le.preferredWidth = le.minWidth = width;
            if (flex >= 0) le.flexibleWidth = flex;
            return le;
        }

        /// <summary>Vertical scroll list; returns the content transform (children laid out top to bottom).</summary>
        public static RectTransform ScrollList(RectTransform area, float spacing, bool grid = false, Vector2 cell = default, int columns = 3)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30;
            var viewport = Fill(area, "Viewport"); viewport.gameObject.AddComponent<RectMask2D>(); var vImg = viewport.gameObject.AddComponent<Image>(); vImg.color = new Color(1, 1, 1, 0.001f);
            var content = Rect(viewport, "Content", new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(0.5f, 1);
            if (grid)
            {
                var g = content.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = cell; g.spacing = new Vector2(spacing, spacing); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns; g.childAlignment = TextAnchor.UpperCenter; g.padding = new RectOffset(10, 10, 10, 30);
            }
            else { var v = Column(content, spacing, 10); v.padding.bottom = 40; }
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

        /// <summary>A pill showing an icon and a number (coins, gems…).</summary>
        public static Text Counter(Transform parent, string icon, Vector2 anchor, Vector2 offset, float width = 280)
        {
            var rt = At(parent, "Counter:" + icon, anchor, new Vector2(width, 96), offset);
            var bg = rt.gameObject.AddComponent<Image>(); bg.sprite = Icons.Rounded(44); bg.type = UnityEngine.UI.Image.Type.Sliced; bg.color = new Color(0, 0, 0, 0.35f); bg.raycastTarget = false;
            var ic = At(rt, "Icon", new Vector2(0, 0.5f), new Vector2(86, 86), new Vector2(8, 0)); Image(ic, Icons.Get(icon));
            var t = Label(rt, "0", 52, Color.white, TextAnchor.MiddleCenter); ((RectTransform)t.transform).offsetMin = new Vector2(90, 0); ((RectTransform)t.transform).offsetMax = new Vector2(-12, 0);
            return t;
        }
    }

    /// <summary>Squishy press animation on buttons.</summary>
    public class Bouncy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private float target = 1f;
        public void OnPointerDown(PointerEventData e) => target = 0.92f;
        public void OnPointerUp(PointerEventData e) => target = 1f;
        private void Update() { float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime)); transform.localScale = new Vector3(s, s, 1); }
    }

    /// <summary>Keeps content inside the phone's safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        private Rect last;
        private void Update()
        {
            var sa = Screen.safeArea; if (sa == last || Screen.width == 0) return; last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height); rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
