using UnityEngine;
using UnityEngine.UI;

namespace ChessMobile.UI
{
    public static class UiFactory
    {
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("Arial.ttf");

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            Stretch(go.GetComponent<RectTransform>());
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(go.GetComponent<RectTransform>());
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = bg;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = label;
            text.fontSize = 36;
            text.color = GameTheme.TextPrimary;
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.GetComponent<RectTransform>());

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720, 110);
            return button;
        }

        public static InputField CreateInput(Transform parent, string name, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.24f, 0.20f);

            var input = go.AddComponent<InputField>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = 34;
            text.color = GameTheme.TextPrimary;
            text.supportRichText = false;
            Stretch(text.GetComponent<RectTransform>(), 20, 10, 20, 10);

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<Text>();
            ph.font = DefaultFont;
            ph.fontSize = 34;
            ph.fontStyle = FontStyle.Italic;
            ph.color = GameTheme.TextMuted;
            ph.text = placeholder;
            Stretch(ph.GetComponent<RectTransform>(), 20, 10, 20, 10);

            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = 12;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720, 100);
            return input;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }
    }
}
