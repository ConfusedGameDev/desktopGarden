using System.Collections.Generic;
using CONFUSEDGAMEDEV.PollenGarden.Core;
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
    /// The expanded menu: currencies, the pollen→nectar trade, the helper shop, and the flower
    /// gallery (a botanical-park placard view of every species completed so far). Opens from the
    /// flower's centre button; opening exits overlay mode (opaque backdrop, whole window
    /// interactive) and the <see cref="ViewportAnchor"/> slides the flower to the left. While the
    /// gallery is open the helpers are paused and the tended flower steps aside so the exhibited
    /// specimen can take its place.
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

        [Tooltip("Display-only flower shown while the gallery is open (Decorative = true).")]
        [SerializeField]
        private FlowerController galleryFlower;

        [SerializeField]
        private FlowerProgression progression;

        [Header("Palette")]
        [SerializeField]
        private Color panelColor = new Color(0.10f, 0.16f, 0.10f, 0.96f);

        [SerializeField]
        private Color buttonColor = new Color(0.22f, 0.34f, 0.20f, 1f);

        [Tooltip("Quit button tint — set apart from the other rows so it is not hit by accident.")]
        [SerializeField]
        private Color quitButtonColor = new Color(0.34f, 0.20f, 0.18f, 1f);

        [SerializeField]
        private Color textColor = new Color(0.96f, 0.97f, 0.92f, 1f);

        [Tooltip("Tint of the (for now empty) full-window backdrop image behind the expanded view.")]
        [SerializeField]
        private Color backdropColor = new Color(0.88f, 0.91f, 0.84f, 1f);

        [Tooltip("Tint of the empty gallery illustration frame until the hand-drawn plates land.")]
        [SerializeField]
        private Color illustrationPlaceholderColor = new Color(0.96f, 0.97f, 0.92f, 0.1f);

        private GameObject menuRoot;
        private TextMeshProUGUI currencyLabel;
        private Button tradeButton;
        private TextMeshProUGUI tradeLabel;

        private GameObject backdropRoot;
        private GameObject settingsRoot;
        private TextMeshProUGUI entryModeLabel;
        private GameObject galleryRoot;
        private TextMeshProUGUI galleryTitle;
        private TextMeshProUGUI galleryScientific;
        private TextMeshProUGUI galleryRange;
        private TextMeshProUGUI galleryDescription;
        private Image galleryIllustration;
        private Button galleryPreviousButton;
        private Button galleryNextButton;
        private int galleryIndex;

        private readonly List<(HelperData helper, Button button, TextMeshProUGUI label)> helperRows =
            new List<(HelperData, Button, TextMeshProUGUI)>();

        private Transform canvasTransform;

        public WindowModeManager WindowModeManager { get => windowModeManager; set => windowModeManager = value; }
        public EconomyManager EconomyManager { get => economyManager; set => economyManager = value; }
        public HelperManager HelperManager { get => helperManager; set => helperManager = value; }
        public FlowerController TendedFlower { get => tendedFlower; set => tendedFlower = value; }
        public FlowerController GalleryFlower { get => galleryFlower; set => galleryFlower = value; }
        public FlowerProgression Progression { get => progression; set => progression = value; }

        public bool IsMenuOpen => menuRoot != null && menuRoot.activeSelf;

        public bool IsGalleryOpen => galleryRoot != null && galleryRoot.activeSelf;

        /// <summary>The placard's illustration frame — assign the hand-drawn plate here later.</summary>
        public Image GalleryIllustration => galleryIllustration;

        private void Awake()
        {
            BuildBackdrop();
            BuildMenu();
            BuildGallery();
            BuildSettings();
            backdropRoot.SetActive(false);
            menuRoot.SetActive(false);
            galleryRoot.SetActive(false);
            settingsRoot.SetActive(false);
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

        public bool IsSettingsOpen => settingsRoot != null && settingsRoot.activeSelf;

        public void ToggleMenu()
        {
            if (IsGalleryOpen)
            {
                CloseGallery();
            }

            if (IsSettingsOpen)
            {
                CloseSettings();
            }

            bool open = !IsMenuOpen;
            menuRoot.SetActive(open);
            backdropRoot.SetActive(open);

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

        public void OpenSettings()
        {
            if (!IsMenuOpen)
            {
                return;
            }

            menuRoot.SetActive(false);
            settingsRoot.SetActive(true);
            RefreshSettings();
        }

        public void CloseSettings()
        {
            if (!IsSettingsOpen)
            {
                return;
            }

            settingsRoot.SetActive(false);
            menuRoot.SetActive(true);
            RefreshMenu();
        }

        private void ToggleHelperEntryMode()
        {
            if (helperManager == null)
            {
                return;
            }

            helperManager.EntryMode = helperManager.EntryMode == HelperEntryMode.ClosestTwoEdges
                ? HelperEntryMode.AllEdges
                : HelperEntryMode.ClosestTwoEdges;
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            if (helperManager == null)
            {
                return;
            }

            entryModeLabel.text = helperManager.EntryMode == HelperEntryMode.ClosestTwoEdges
                ? "Helpers arrive from:  the two closest edges"
                : "Helpers arrive from:  all edges";
        }

        /// <summary>Menu → gallery: helpers freeze, the tended flower steps aside for the exhibit.</summary>
        public void OpenGallery()
        {
            if (!IsMenuOpen || galleryFlower == null)
            {
                return;
            }

            menuRoot.SetActive(false);
            galleryRoot.SetActive(true);

            if (helperManager != null)
            {
                helperManager.SetPaused(true);
            }

            SetTendedFlowerHidden(true);
            RefreshGallery();
        }

        /// <summary>Gallery → menu: exhibit away, tended flower back, helpers resume.</summary>
        public void CloseGallery()
        {
            if (!IsGalleryOpen)
            {
                return;
            }

            galleryRoot.SetActive(false);
            galleryFlower.gameObject.SetActive(false);
            SetTendedFlowerHidden(false);

            if (helperManager != null)
            {
                helperManager.SetPaused(false);
            }

            menuRoot.SetActive(true);
            RefreshMenu();
        }

        /// <summary>
        /// Hiding must not touch petal state — disabling the FlowerController would rebuild (and
        /// heal) the flower on re-enable. Instead its anchor is suspended and the whole flower
        /// parked far below the viewport; re-enabling the anchor snaps it back next frame.
        /// </summary>
        private void SetTendedFlowerHidden(bool hidden)
        {
            if (tendedFlower == null)
            {
                return;
            }

            var anchor = tendedFlower.GetComponent<ViewportAnchor>();
            if (anchor != null)
            {
                anchor.enabled = !hidden;
            }

            if (hidden)
            {
                tendedFlower.transform.position = Vector3.down * 1000f;
            }
        }

        private void RefreshGallery()
        {
            if (progression == null)
            {
                return;
            }

            IReadOnlyList<FlowerSpeciesData> completed = progression.CompletedSpecies;
            bool hasMultiple = completed.Count > 1;
            galleryPreviousButton.gameObject.SetActive(hasMultiple);
            galleryNextButton.gameObject.SetActive(hasMultiple);

            if (completed.Count == 0)
            {
                galleryTitle.text = "Gallery";
                galleryScientific.text = string.Empty;
                galleryRange.text = string.Empty;
                galleryDescription.text =
                    "No flowers completed yet.\nClear every petal of a flower and it will be exhibited here.";
                galleryFlower.gameObject.SetActive(false);
                return;
            }

            galleryIndex = (galleryIndex % completed.Count + completed.Count) % completed.Count;
            FlowerSpeciesData species = completed[galleryIndex];

            galleryTitle.text = species.DisplayName;
            galleryScientific.text = species.ScientificName;
            galleryRange.text = $"<b>Where it grows.</b>  {species.NativeRange}";
            galleryDescription.text = species.PlacardDescription;

            galleryFlower.gameObject.SetActive(true);
            if (galleryFlower.Species != species)
            {
                galleryFlower.Species = species;
                galleryFlower.Rebuild();
            }
        }

        private void StepGallery(int direction)
        {
            galleryIndex += direction;
            RefreshGallery();
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

            // Screen Space – Camera (not Overlay): the panels then live in the camera's frustum,
            // in front of the backdrop plane and beside the flower, so a plain camera render —
            // including editor captures — shows the complete expanded view.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 8f;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasTransform = canvasObject.transform;
            menuRoot = CreatePanel(canvasTransform);

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

            (Button galleryButton, TextMeshProUGUI galleryButtonLabel) =
                CreateButton(menuRoot.transform, "Gallery", OpenGallery);
            galleryButtonLabel.text = "Gallery";

            (Button settingsButton, TextMeshProUGUI settingsButtonLabel) =
                CreateButton(menuRoot.transform, "Settings", OpenSettings);
            settingsButtonLabel.text = "Settings";

            (Button closeButton, TextMeshProUGUI closeLabel) =
                CreateButton(menuRoot.transform, "Close", ToggleMenu);
            closeLabel.text = "Close";

            (Button quitButton, TextMeshProUGUI quitLabel) =
                CreateButton(menuRoot.transform, "Quit Pollen Garden", QuitGame);
            quitLabel.text = "Quit Pollen Garden";
            quitButton.targetGraphic.color = quitButtonColor;
        }

        /// <summary>
        /// Leaves the game. The only way out on desktop: overlay mode is a borderless,
        /// click-through window with no title bar, so there is no close box to reach for.
        /// </summary>
        /// <remarks>
        /// No explicit save call — <c>Application.Quit</c> raises <c>OnApplicationQuit</c>, which
        /// is where <c>SaveManager</c> already flushes. Calling both would just write twice.
        /// </remarks>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildSettings()
        {
            settingsRoot = CreatePanel(canvasTransform);
            settingsRoot.name = "PG_Settings";

            CreateLabel(settingsRoot.transform, "Settings", TitleFontSize, FontStyles.Bold);

            // The button's label carries the current value; clicking cycles it.
            (Button entryModeButton, TextMeshProUGUI entryButtonLabel) =
                CreateButton(settingsRoot.transform, string.Empty, ToggleHelperEntryMode);
            entryModeLabel = entryButtonLabel;

            (Button backButton, TextMeshProUGUI backLabel) =
                CreateButton(settingsRoot.transform, "Back", CloseSettings);
            backLabel.text = "Back";
        }

        /// <summary>
        /// The full-window backdrop behind the expanded view. A Screen Space – Camera canvas,
        /// pushed far down the view frustum, so the 3D flower (much nearer) draws in front of it
        /// while it still covers every pixel. The Image is deliberately sprite-less for now —
        /// drop the garden art into it later without touching code.
        /// </summary>
        private void BuildBackdrop()
        {
            var backdropCanvasObject = new GameObject("PG_BackdropCanvas", typeof(Canvas));
            backdropCanvasObject.transform.SetParent(transform, false);

            Canvas canvas = backdropCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 80f;

            var imageObject = new GameObject("PG_Backdrop", typeof(Image));
            imageObject.transform.SetParent(backdropCanvasObject.transform, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            imageObject.GetComponent<Image>().color = backdropColor;

            backdropRoot = backdropCanvasObject;
        }

        private void BuildGallery()
        {
            galleryRoot = CreatePanel(canvasTransform);
            galleryRoot.name = "PG_Gallery";

            galleryTitle = CreateLabel(galleryRoot.transform, "Gallery", TitleFontSize, FontStyles.Bold);
            galleryScientific = CreateLabel(galleryRoot.transform, string.Empty,
                BodyFontSize, FontStyles.Italic);
            galleryRange = CreateLabel(galleryRoot.transform, string.Empty,
                BodyFontSize, FontStyles.Normal, PanelRowHeight * 2.2f, TextAlignmentOptions.TopLeft);

            // Flexible: the description absorbs all of the panel's spare height, which is what
            // pins the illustration, nav row and Back button to the bottom of the placard.
            galleryDescription = CreateLabel(galleryRoot.transform, string.Empty,
                BodyFontSize, FontStyles.Normal, PanelRowHeight * 3.4f, TextAlignmentOptions.TopLeft);
            galleryDescription.GetComponent<LayoutElement>().flexibleHeight = 1f;

            // Sprite-less frame reserved for the species' hand-drawn botanical plate.
            var illustrationObject = new GameObject("PG_GalleryIllustration",
                typeof(Image), typeof(LayoutElement));
            illustrationObject.transform.SetParent(galleryRoot.transform, false);
            illustrationObject.GetComponent<LayoutElement>().preferredHeight = PanelRowHeight * 3.5f;
            galleryIllustration = illustrationObject.GetComponent<Image>();
            galleryIllustration.color = illustrationPlaceholderColor;

            var navRow = new GameObject("PG_GalleryNav", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            navRow.transform.SetParent(galleryRoot.transform, false);
            navRow.GetComponent<LayoutElement>().preferredHeight = PanelRowHeight;

            HorizontalLayoutGroup navLayout = navRow.GetComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 16f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = true;

            // Plain ASCII arrows: the default TMP font has no ◀/▶ glyphs.
            (galleryPreviousButton, _) = CreateButton(navRow.transform, "<  Previous",
                () => StepGallery(-1));
            (galleryNextButton, _) = CreateButton(navRow.transform, "Next  >",
                () => StepGallery(+1));

            (Button backButton, TextMeshProUGUI backLabel) =
                CreateButton(galleryRoot.transform, "Back", CloseGallery);
            backLabel.text = "Back";
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

            // Must control height: with this off the group ignores every LayoutElement's
            // preferredHeight, each row keeps its default 100px RectTransform, and a full menu
            // overflows the panel — the bottom button ends up clipped off the window.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return panel;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize,
            FontStyles style, float height = PanelRowHeight,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var labelObject = new GameObject("PG_Label", typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelObject.transform.SetParent(parent, false);
            labelObject.GetComponent<LayoutElement>().preferredHeight = height;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = textColor;
            label.alignment = alignment;
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
