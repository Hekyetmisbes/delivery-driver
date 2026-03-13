using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public class ConfirmationDialog : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private Action confirmAction;
        private Action cancelAction;

        public static void Show(string title, string message, Action onConfirm, Action onCancel = null)
        {
            Canvas canvas = GlobalUiCoordinator.PrimaryCanvas;
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            }

            if (canvas == null) return;

            GameObject dialogRoot = new GameObject("ConfirmationDialog", typeof(RectTransform), typeof(CanvasGroup));
            dialogRoot.transform.SetParent(canvas.transform, false);

            // Ensure it renders on top
            Canvas dialogCanvas = dialogRoot.AddComponent<Canvas>();
            dialogCanvas.overrideSorting = true;
            dialogCanvas.sortingOrder = 999;
            dialogRoot.AddComponent<GraphicRaycaster>();

            ConfirmationDialog dialog = dialogRoot.AddComponent<ConfirmationDialog>();
            dialog.confirmAction = onConfirm;
            dialog.cancelAction = onCancel;
            dialog.canvasGroup = dialogRoot.GetComponent<CanvasGroup>();
            dialog.BuildUI(dialogRoot.transform, title, message);

            dialog.canvasGroup.alpha = 0f;
            UIAnimationHelper.FadeIn(dialog, dialog.canvasGroup, 0.2f);
        }

        private void BuildUI(Transform parent, string title, string message)
        {
            // Overlay background
            GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(parent, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // Add button to overlay for dismiss on background click
            Button overlayButton = overlay.AddComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.onClick.AddListener(OnCancelClicked);

            // Dialog panel
            Sprite panelSprite = RuntimeUiSkinLoader.LoadSprite(
                "UI/Kenney/panel_bg",
                "Assets/kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png");

            Sprite buttonSprite = RuntimeUiSkinLoader.LoadSprite(
                "UI/Kenney/button_bg",
                "Assets/kenney_ui-pack/PNG/Grey/Default/button_rectangle_depth_flat.png");

            GameObject panel = new GameObject("DialogPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, 260f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 16f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            // Title
            CreateText(panel.transform, "Title", title, 28, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, 40f);

            // Message
            CreateText(panel.transform, "Message", message, 22, FontStyles.Normal, TextAlignmentOptions.Center,
                new Color(0.85f, 0.9f, 0.95f), 60f);

            // Button row
            GameObject buttonRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttonRow.transform.SetParent(panel.transform, false);
            HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 16f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            buttonRow.GetComponent<LayoutElement>().minHeight = 54f;

            // Cancel button
            Button cancelBtn = CreateDialogButton(buttonRow.transform, "Iptal", new Color(0.35f, 0.38f, 0.42f, 1f), buttonSprite);
            cancelBtn.onClick.AddListener(OnCancelClicked);
            UIButtonEnhancer.EnhanceButton(cancelBtn);

            // Confirm button
            Button confirmBtn = CreateDialogButton(buttonRow.transform, "Onayla", new Color(0.16f, 0.62f, 0.3f, 1f), buttonSprite);
            confirmBtn.onClick.AddListener(OnConfirmClicked);
            UIButtonEnhancer.EnhanceButton(confirmBtn);

            // Scale-in animation
            UIAnimationHelper.ScaleIn(this, panelRect, 0.25f);
        }

        private void CreateText(Transform parent, string name, string content, float fontSize, FontStyles style,
            TextAlignmentOptions alignment, Color color, float minHeight)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().minHeight = minHeight;

            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }

        private Button CreateDialogButton(Transform parent, string label, Color color, Sprite bgSprite)
        {
            GameObject btnObj = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnObj.transform.SetParent(parent, false);
            btnObj.GetComponent<LayoutElement>().minHeight = 48f;

            Image btnImage = btnObj.GetComponent<Image>();
            btnImage.color = color;
            if (bgSprite != null)
            {
                btnImage.sprite = bgSprite;
                btnImage.type = Image.Type.Sliced;
            }

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return btnObj.GetComponent<Button>();
        }

        private void OnConfirmClicked()
        {
            Close();
            confirmAction?.Invoke();
        }

        private void OnCancelClicked()
        {
            Close();
            cancelAction?.Invoke();
        }

        private void Close()
        {
            if (canvasGroup != null)
            {
                UIAnimationHelper.FadeOut(this, canvasGroup, 0.15f, () => Destroy(gameObject));
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
