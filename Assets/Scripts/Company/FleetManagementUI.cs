using System;
using System.Collections.Generic;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Company
{
    /// <summary>
    /// Chrome sekmeleri tarzında, sekmeli Filo Yönetim ve Araç Satın Alma Arayüzü.
    /// </summary>
    public class FleetManagementUI : MonoBehaviour
    {
        private enum TabType { MyVehicles, Gallery, Upgrade }

        public static FleetManagementUI Instance { get; private set; }
        public bool IsOpen => canvasObject != null && canvasObject.activeSelf;

        [Header("Settings")]
        [SerializeField] private int truckPrice = 10000;
        [SerializeField] private int baseUpgradePrice = 500;
        [SerializeField] private int maxUpgradeLevel = 5;

        private GameObject canvasObject;
        private CanvasGroup canvasGroup;
        private RectTransform mainPanelRect;
        private Transform contentArea;
        private TabType currentTab = TabType.MyVehicles;
        private List<Button> tabButtons = new List<Button>();
        private bool isInitialized;
        private float previousTimeScale = 1f;

        // Upgrade state - would ideally be persisted in DB
        private Dictionary<VehicleType, VehicleUpgradeData> vehicleUpgrades = new Dictionary<VehicleType, VehicleUpgradeData>();

        private float openedTimestamp;

        public void Open()
        {
            if (IsOpen) return;
            
            // Force reset and reload localization to be absolutely sure
            LocalizationTable.EnsureLoaded();
            
            EnsureInitialized();
            
            if (canvasObject == null) return;

            openedTimestamp = Time.unscaledTime;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            
            canvasObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            
            // Always start with MyVehicles tab
            SwitchToTab(TabType.MyVehicles);
            
            UIAnimationHelper.FadeIn(this, canvasGroup, UIThemeConstants.PanelFadeDuration);
            UIAnimationHelper.ScaleIn(this, mainPanelRect, UIThemeConstants.PanelScaleDuration);
            
            ApplyUpgradesToActiveVehicle();
        }

        private void Update()
        {
            if (IsOpen)
            {
                // Prevent immediate close on same frame as open
                if (Time.unscaledTime - openedTimestamp < 0.2f) return;

                // Menü açıkken E veya ESC'ye basılırsa kapat
                if (WasInteractionKeyPressed() || WasExitKeyPressed())
                {
                    Close();
                }
            }
        }

        private bool WasInteractionKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.eKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.E);
        }

        private bool WasExitKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        public void Close()
        {
            if (canvasGroup == null || !IsOpen) return;

            UIAnimationHelper.FadeOut(this, canvasGroup, 0.15f, () => {
                if (canvasObject != null) canvasObject.SetActive(false);
                Time.timeScale = previousTimeScale;
            });
        }

        private void EnsureInitialized()
        {
            if (isInitialized && canvasObject != null) return;
            Instance = this;

            canvasObject = new GameObject("FleetManagementCanvasRoot");
            canvasObject.transform.SetParent(transform);
            
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();

            GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvasObject.transform, false);
            Image overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = UIThemeConstants.OverlayBackground;
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            GameObject mainPanel = new GameObject("MainPanel", typeof(RectTransform));
            mainPanel.transform.SetParent(canvasObject.transform, false);
            mainPanelRect = mainPanel.GetComponent<RectTransform>();
            
            Image panelImg = mainPanel.AddComponent<Image>();
            panelImg.color = UIThemeConstants.PanelBackground;
            panelImg.sprite = null;
            panelImg.type = Image.Type.Simple;

            mainPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            mainPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            mainPanelRect.pivot = new Vector2(0.5f, 0.5f);
            mainPanelRect.sizeDelta = new Vector2(1100, 750);

            CreateTabHeader(mainPanel.transform);

            GameObject contentObj = new GameObject("ContentArea", typeof(RectTransform));
            contentObj.transform.SetParent(mainPanel.transform, false);
            contentArea = contentObj.transform;
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.82f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.offsetMin = new Vector2(40, 40);
            contentRect.offsetMax = new Vector2(-40, -10);

            CreateCloseButton(mainPanel.transform);

            canvasObject.SetActive(false);
            isInitialized = true;
        }

        private void CreateTabHeader(Transform parent)
        {
            GameObject header = new GameObject("TabHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            header.transform.SetParent(parent, false);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.88f);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup layout = header.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 70, 10, 0);
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            tabButtons.Clear();
            tabButtons.Add(CreateTabButton(header.transform, LocalizationTable.Get("fleet_tab_vehicles"), TabType.MyVehicles));
            tabButtons.Add(CreateTabButton(header.transform, LocalizationTable.Get("fleet_tab_gallery"), TabType.Gallery));
            tabButtons.Add(CreateTabButton(header.transform, LocalizationTable.Get("fleet_tab_upgrade"), TabType.Upgrade));

        }

        private Button CreateTabButton(Transform parent, string label, TabType type)
        {
            GameObject btnObj = new GameObject("Tab_" + label, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            
            Image img = btnObj.AddComponent<Image>();
            img.color = UIThemeConstants.SectionBackground;
            img.type = Image.Type.Simple;
            
            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => SwitchToTab(type));
            UIButtonEnhancer.EnhanceButton(btn);

            GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private void CreateCloseButton(Transform parent)
        {
            GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
            closeBtnObj.transform.SetParent(parent, false);
            
            Image img = closeBtnObj.AddComponent<Image>();
            img.color = UIThemeConstants.ButtonRed;
            img.type = Image.Type.Simple;
            
            Button btn = closeBtnObj.AddComponent<Button>();
            btn.onClick.AddListener(Close);
            
            RectTransform rect = closeBtnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(50, 50);
            rect.anchoredPosition = new Vector3(-15, -15, 0);

            GameObject txtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(closeBtnObj.transform, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.text = "X";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontSize = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            
            RectTransform txtRect = txtObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            
            UIButtonEnhancer.EnhanceButton(btn);
        }

        private void SwitchToTab(TabType type)
        {
            currentTab = type;
            LocalizationTable.EnsureLoaded();
            
            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Count; i++)
                {
                    if (tabButtons[i] != null && tabButtons[i].image != null)
                    {
                        tabButtons[i].image.color = (int)type == i ? UIThemeConstants.ButtonBlue : UIThemeConstants.SectionBackground;
                    }
                }
            }

            if (contentArea != null)
            {
                List<Transform> children = new List<Transform>();
                foreach (Transform child in contentArea) children.Add(child);
                foreach (Transform child in children) Destroy(child.gameObject);
            }
            else
            {
                return; 
            }

            switch (type)
            {
                case TabType.MyVehicles: BuildMyVehiclesTab(); break;
                case TabType.Gallery: BuildGalleryTab(); break;
                case TabType.Upgrade: BuildUpgradeTab(); break;
            }
        }

        private void BuildMyVehiclesTab()
        {
            Transform list = CreateScrollList();
            
            CreateVehicleRow(list, VehicleType.Van, LocalizationTable.Get("vehicle_van_display"), true);
            bool hasTruck = PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.IsVehicleOwned(VehicleType.Truck);
            CreateVehicleRow(list, VehicleType.Truck, LocalizationTable.Get("vehicle_truck_display"), hasTruck);
        }

        private void BuildGalleryTab()
        {
            Transform list = CreateScrollList();
            
            bool hasTruck = PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.IsVehicleOwned(VehicleType.Truck);
            CreateGalleryRow(list, VehicleType.Truck, LocalizationTable.Get("vehicle_truck_display"), truckPrice, hasTruck);
        }

        private void BuildUpgradeTab()
        {
            PlayerVehicleManager pvm = PlayerVehicleManager.Instance;
            if (pvm == null || pvm.ActiveVehicleType == (VehicleType)(-1))
            {
                GameObject warning = new GameObject("NoVehicleWarning", typeof(RectTransform), typeof(TextMeshProUGUI));
                warning.transform.SetParent(contentArea, false);
                TextMeshProUGUI tmp = warning.GetComponent<TextMeshProUGUI>();
                tmp.text = "\n\nActive vehicle required for upgrades.";
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 32;
                tmp.color = UIThemeConstants.Negative;
                return;
            }

            VehicleType activeType = pvm.ActiveVehicleType;
            Transform list = CreateScrollList();
            VehicleUpgradeData upgrades = GetUpgradeData(activeType);

            CreateUpgradeRow(list, "engine", LocalizationTable.Get("upgrade_engine_title"), upgrades.EngineLevel, (lvl) => upgrades.EngineLevel = lvl);
            CreateUpgradeRow(list, "brake", LocalizationTable.Get("upgrade_brake_title"), upgrades.BrakeLevel, (lvl) => upgrades.BrakeLevel = lvl);
            CreateUpgradeRow(list, "handling", LocalizationTable.Get("upgrade_handling_title"), upgrades.HandlingLevel, (lvl) => upgrades.HandlingLevel = lvl);
        }

        private void CreateUpgradeRow(Transform parent, string id, string title, int currentLevel, Action<int> onUpgrade)
        {
            GameObject row = new GameObject("UpgradeRow_" + id, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            
            Image img = row.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
            img.type = Image.Type.Simple;
            
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.spacing = 20;

            row.GetComponent<LayoutElement>().minHeight = 120f;

            GameObject txtObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            string levelLabel = LocalizationTable.Format("upgrade_level_label", currentLevel);
            tmp.text = $"{title}\n<size=20><color=#AAAAAA>{levelLabel}</color></size>";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;

            bool isMax = currentLevel >= maxUpgradeLevel;
            int cost = baseUpgradePrice * (currentLevel + 1);

            GameObject btnObj = new GameObject("UpgradeButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(row.transform, false);
            Button btn = btnObj.GetComponent<Button>();
            
            int balance = PlayerProgressionManager.Instance != null ? PlayerProgressionManager.Instance.CurrentMoney : 0;
            bool canAfford = balance >= cost;
            
            btn.interactable = !isMax && canAfford;
            btn.onClick.AddListener(() => {
                if (PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.SpendMoney(cost))
                {
                    onUpgrade(currentLevel + 1);
                    ApplyUpgradesToActiveVehicle();
                    SwitchToTab(TabType.Upgrade);
                }
            });
            
            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = isMax ? UIThemeConstants.Positive : (canAfford ? UIThemeConstants.ButtonBlue : UIThemeConstants.ButtonNeutral);
            btnImg.type = Image.Type.Simple;
            
            GameObject btnTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI btnTmp = btnTxtObj.GetComponent<TextMeshProUGUI>();
            btnTmp.text = isMax ? LocalizationTable.Get("upgrade_max_level") : LocalizationTable.Format("upgrade_button_buy", cost);
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontSize = 18;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.color = Color.white;
            
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 54);
            UIButtonEnhancer.EnhanceButton(btn);
        }

        private VehicleUpgradeData GetUpgradeData(VehicleType type)
        {
            if (!vehicleUpgrades.ContainsKey(type))
                vehicleUpgrades[type] = VehicleUpgradeData.Default;
            return vehicleUpgrades[type];
        }

        private void ApplyUpgradesToActiveVehicle()
        {
            PlayerVehicleManager pvm = PlayerVehicleManager.Instance;
            if (pvm == null || pvm.ActiveVehicleType == (VehicleType)(-1)) return;

            CarController car = pvm.GetComponentInChildren<CarController>();
            if (car == null) car = FindFirstObjectByType<CarController>();
            
            if (car != null)
            {
                VehicleUpgradeData data = GetUpgradeData(pvm.ActiveVehicleType);
                car.ApplyUpgrades(data.EngineLevel, data.BrakeLevel, data.HandlingLevel);
            }
        }

        private Transform CreateScrollList()
        {
            GameObject scrollObj = new GameObject("ScrollList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            scrollObj.transform.SetParent(contentArea, false);
            VerticalLayoutGroup layout = scrollObj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(10, 10, 20, 10);
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            
            RectTransform rect = scrollObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            
            return scrollObj.transform;
        }

        private void CreateVehicleRow(Transform parent, VehicleType type, string name, bool isOwned)
        {
            GameObject row = new GameObject("Row_" + type, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            
            Image img = row.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
            img.type = Image.Type.Simple;
            
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.spacing = 20;

            row.GetComponent<LayoutElement>().minHeight = 120f;

            // Vehicle Icon
            Sprite vehicleSprite = GetVehicleSprite(type);
            if (vehicleSprite != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = vehicleSprite;
                iconImg.preserveAspect = true;
                iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 80);
            }

            GameObject txtObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            string status = isOwned ? LocalizationTable.Get("fleet_owned_status") : LocalizationTable.Get("fleet_locked_status");
            string statusColor = isOwned ? ColorUtility.ToHtmlStringRGB(UIThemeConstants.Positive) : ColorUtility.ToHtmlStringRGB(UIThemeConstants.Negative);
            tmp.text = $"{name} <color=#{statusColor}>{status}</color>";
            tmp.fontSize = 24;

            if (isOwned)
            {
                GameObject btnObj = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(row.transform, false);
                Button btn = btnObj.GetComponent<Button>();
                btn.onClick.AddListener(() => {
                    PlayerVehicleManager.Instance.SwitchVehicle(type);
                    Close();
                });
                
                Image btnImg = btnObj.GetComponent<Image>();
                btnImg.color = UIThemeConstants.ButtonGreen;
                btnImg.type = Image.Type.Simple;
                
                GameObject btnTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                btnTxtObj.transform.SetParent(btnObj.transform, false);
                TextMeshProUGUI btnTmp = btnTxtObj.GetComponent<TextMeshProUGUI>();
                btnTmp.text = LocalizationTable.Get("fleet_use_vehicle");
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.fontSize = 20;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = Color.white;
                
                btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 54);
                UIButtonEnhancer.EnhanceButton(btn);
            }
        }

        private void CreateGalleryRow(Transform parent, VehicleType type, string name, int price, bool isOwned)
        {
            GameObject row = new GameObject("GalleryRow_" + type, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            
            Image img = row.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
            img.type = Image.Type.Simple;
            
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.spacing = 20;

            row.GetComponent<LayoutElement>().minHeight = 130f;

            // Vehicle Icon
            Sprite vehicleSprite = GetVehicleSprite(type);
            if (vehicleSprite != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(row.transform, false);
                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.sprite = vehicleSprite;
                iconImg.preserveAspect = true;
                iconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 80);
            }

            GameObject txtObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            string priceLabel = LocalizationTable.Get("fleet_price_label");
            string priceStr = isOwned ? LocalizationTable.Get("fleet_purchased_status") : $"{priceLabel} ${price:N0}";
            tmp.text = $"{name}\n<size=20><color=#FFD700>{priceStr}</color></size>";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;

            if (!isOwned)
            {
                GameObject btnObj = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(row.transform, false);
                Button btn = btnObj.GetComponent<Button>();
                
                int balance = PlayerProgressionManager.Instance != null ? PlayerProgressionManager.Instance.CurrentMoney : 0;
                bool canAfford = balance >= price;
                
                btn.interactable = canAfford;
                btn.onClick.AddListener(() => {
                    if (PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.BuyVehicle(type, price))
                    {
                        SwitchToTab(TabType.Gallery);
                    }
                });
                
                Image btnImg = btnObj.GetComponent<Image>();
                btnImg.color = canAfford ? UIThemeConstants.ButtonBlue : UIThemeConstants.ButtonNeutral;
                btnImg.type = Image.Type.Simple;
                
                GameObject btnTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                btnTxtObj.transform.SetParent(btnObj.transform, false);
                TextMeshProUGUI btnTmp = btnTxtObj.GetComponent<TextMeshProUGUI>();
                btnTmp.text = canAfford ? LocalizationTable.Get("fleet_buy_vehicle") : LocalizationTable.Get("fleet_insufficient_funds");
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.fontSize = 20;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = Color.white;
                
                btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(230, 58);
                UIButtonEnhancer.EnhanceButton(btn);
            }
            else
            {
                GameObject icon = new GameObject("CheckIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
                icon.transform.SetParent(row.transform, false);
                TextMeshProUGUI iconTmp = icon.GetComponent<TextMeshProUGUI>();
                iconTmp.text = $"✔ {LocalizationTable.Get("fleet_owned_status")}";
                iconTmp.color = UIThemeConstants.Positive;
                iconTmp.fontSize = 26;
                iconTmp.fontStyle = FontStyles.Bold;
                iconTmp.alignment = TextAlignmentOptions.Right;
            }
        }

        private Sprite GetVehicleSprite(VehicleType type)
        {
            // Try to load a specialized icon if it exists
            Sprite s = Resources.Load<Sprite>($"UI/VehicleIcons/{type}");
            if (s != null) return s;

            // Fallback to logo
            s = Resources.Load<Sprite>("UI/MainMenu/Logo");
            if (s != null) return s;

            return null; // Return null instead of white square fallback
        }
    }
}
