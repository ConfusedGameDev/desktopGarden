using System.Collections.Generic;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using CONFUSEDGAMEDEV.PollenGarden.Helpers;
using CONFUSEDGAMEDEV.PollenGarden.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CONFUSEDGAMEDEV.PollenGarden.UI
{
    /// <summary>
    /// The expanded menu: currencies, the pollen→nectar trade, and the helper shop. Opens from
    /// the flower's centre button; opening exits overlay mode (opaque background, whole window
    /// interactive) and the <see cref="ViewportAnchor"/> slides the flower to the left.
    /// </summary>
    /// <remarks>
    /// The hierarchy is built in code at Awake — no prefab, so the menu never drifts from the
    /// systems it fronts, at the cost of visual polish living in C#. When a real art pass comes
    /// (M4), this becomes a prefab and this class shrinks to the event wiring.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/UI Manager")]
    public sealed class UIManager : MonoBehaviour
    {
        private const float PanelRowHeight = 64f;
        private const float TitleFontSize = 44f;
        private const float BodyFontSize = 28f;

        [SerializeField]
        private WindowModeManager windowModeManager;

        [SerializeField]
        private EconomyManager economyManager;

        [SerializeField]
        private HelperManager helperManager;

        [SerializeField]
        private FlowerController tendedFlower;

        [Header("Palette")]
        [SerializeField]
        private Color panelColor = new Color(0.10f, 0.16f, 0.10f, 0.96f);

        [SerializeField]
        private Color buttonColor = new Color(0.22f, 0.34f, 0.20f, 1f);

        [SerializeField]
        private Color textColor = new Color(0.96f, 0.97f, 0.92f, 1f);

        private GameObject menuRoot;
        private TextMeshProUGUI currencyLabel;
        private Button tradeButton;
        private TextMeshProUGUI tradeLabel;

        private readonly List<(HelperData helper, Button button, TextMeshProUGUI label)> helperRows =
            new List<(HelperData, Button, TextMeshProUGUI)>();

        public WindowModeManager WindowModeManager { get => windowModeManager; set => windowModeManager = value; }
        public EconomyManager EconomyManager { get => economyManager; set => economyManager = value; }
        public HelperManager HelperManager { get => helperManager; set => helperManager = value; }
        public FlowerController TendedFlower { get => tendedFlower; set => tendedFlower = value; }

        public bool IsMenuOpen => menuRoot != null && menuRoot.activeSelf;

        private void Awake()
        {
            BuildMenu();
            menuRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (tendedFlower != null)
            {
                tendedFlower.CenterClicked += ToggleMenu;
            }

            if (economyManager != null)
            {
                economyManager.Model.Changed += RefreshMenu;
            }

            if (helperManager != null)
            {
                helperManager.Changed += RefreshMenu;
            }
        }

        private void OnDisable()
        {
            if (tendedFlower != null)
            {
                tendedFlower.CenterClicked -= ToggleMenu;
            }

            if (economyManager != null)
            {
                economyManager.Model.Changed -= RefreshMenu;
            }

            if (helperManager != null)
            {
                helperManager.Changed -= RefreshMenu;
            }
        }

        public void ToggleMenu()
        {
            bool open = !IsMenuOpen;
            menuRoot.SetActive(open);

            if (windowModeManager != null)
            {
                if (open)
                {
                    windowModeManager.ExitOverlay();
                }
                else
                {
                    windowModeManager.EnterOverlay();
                }
            }

            if (open)
            {
                RefreshMenu();
            }
        }

        private void RefreshMenu()
        {
            if (!IsMenuOpen || economyManager == null)
            {
                return;
            }

            var model = economyManager.Model;
            currencyLabel.text = $"Pollen {model.Pollen}   ·   Nectar {model.Nectar}";
            tradeLabel.text = $"Trade {model.PollenPerNectar} pollen → 1 nectar";
            tradeButton.interactable = model.CanTradePollenForNectar;

            foreach ((HelperData helper, Button button, TextMeshProUGUI label) in helperRows)
            {
                int owned = helperManager != null ? helperManager.GetOwnedCount(helper) : 0;
                label.text = $"{helper.DisplayName} — {helper.NectarCost} nectar   (owned {owned})";
                button.interactable = helperManager != null && helperManager.CanAfford(helper);
            }
        }

        private void BuildMenu()
        {
            var canvasObject = new GameObject("PG_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            menuRoot = CreatePanel(canvasObject.transform);

            CreateLabel(menuRoot.transform, "Pollen Garden", TitleFontSize, FontStyles.Bold);
            currencyLabel = CreateLabel(menuRoot.transform, string.Empty, BodyFontSize, FontStyles.Normal);

            (tradeButton, tradeLabel) = CreateButton(menuRoot.transform, string.Empty,
                () => economyManager.Model.TryTradePollenForNectar());

            helperRows.Clear();
            if (helperManager != null)
            {
                foreach (HelperData helper in helperManager.AvailableHelpers)
                {
                    if (helper == null)
                    {
                        continue;
                    }

                    HelperData captured = helper;
                    (Button button, TextMeshProUGUI label) = CreateButton(menuRoot.transform,
                        string.Empty, () => helperManager.TryPurchase(captured));
                    helperRows.Add((helper, button, label));
                }
            }

            (Button closeButton, TextMeshProUGUI closeLabel) =
                CreateButton(menuRoot.transform, "Close", ToggleMenu);
            closeLabel.text = "Close";
        }

        private GameObject CreatePanel(Transform parent)
        {
            var panel = new GameObject("PG_Menu", typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.52f, 0.08f);
            rect.anchorMax = new Vector2(0.97f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = panelColor;

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return panel;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, FontStyles style)
        {
            var labelObject = new GameObject("PG_Label", typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);
            labelObject.GetComponent<LayoutElement>().preferredHeight = PanelRowHeight;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private (Button, TextMeshProUGUI) CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject("PG_Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = PanelRowHeight;

            Image image = buttonObject.GetComponent<Image>();
            image.color = buttonColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("PG_ButtonLabel", typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = BodyFontSize;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.Center;

            return (button, label);
        }
    }
}
