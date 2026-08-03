using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// Builds the whole UI from code.
    ///
    /// A prototype whose scene is a binary .unity file is a prototype nobody else can
    /// diff, review or regenerate. Everything here is constructed at runtime instead,
    /// so the repo stays reviewable and the project drops into an empty Unity project
    /// with no scene wiring at all. The shipping game replaces this with real prefabs.
    /// </summary>
    public static class UiBuilder
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                // Unity 2022+ ships LegacyRuntime.ttf; older versions use Arial.ttf.
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler),
                                    typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Creates the EventSystem, picking whichever input module this project has.
        /// New Unity projects default to the Input System package, older ones to the
        /// legacy manager; guessing wrong means nothing is tappable at all.
        /// </summary>
        public static EventSystem CreateEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModule = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                go.AddComponent(inputSystemModule);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
            return go.GetComponent<EventSystem>();
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string name, string text, int size,
                                 Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            return label;
        }

        public static Button TextButton(Transform parent, string name, string caption,
                                        Color background, Color foreground,
                                        Action onClick)
        {
            RectTransform rect = Panel(parent, name, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            Text label = Label(rect, "Label", caption, 40, foreground);
            Stretch(label.rectTransform);

            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>
        /// A vertical scroll area. Returns the content rect to fill; it is
        /// top-anchored with a pivot at the top, so callers position children
        /// downwards from 0 and then set content.sizeDelta.y to the total.
        /// </summary>
        public static RectTransform ScrollArea(Transform parent, string name,
                                               out ScrollRect scroll)
        {
            RectTransform viewport = Panel(parent, name, new Color(0f, 0f, 0f, 0f));
            Stretch(viewport);
            // RectMask2D rather than Mask: no stencil buffer, and it does not
            // need a Graphic to clip against, which keeps the viewport invisible.
            viewport.gameObject.AddComponent<RectMask2D>();
            Image backdrop = viewport.GetComponent<Image>();
            if (backdrop != null) backdrop.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>Anchors a rect to a corner or edge with a fixed pixel size.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
                                 Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// A material that works whether the project uses URP or the built-in pipeline.
        /// Getting this wrong is the classic "everything is bright magenta" failure.
        /// </summary>
        public static Material CreateLitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Diffuse");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            return material;
        }
    }
}
