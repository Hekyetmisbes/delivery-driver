using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryDriver.Cinematic
{
    /// <summary>
    /// Ekran üzeri sinematik kontrol paneli, durum göstergesi ve klavye kısayolları.
    /// OnGUI tabanlı olduğu için herhangi bir Canvas kurulumu gerektirmeden anında çalışır.
    /// </summary>
    public class CinematicCaptureHUD : MonoBehaviour
    {
        public static CinematicCaptureHUD Instance { get; private set; }

        [Header("Görünüm Ayarları")]
        [SerializeField] private bool showHUD = true;
        [SerializeField] private bool showHelpLegend = true;
        [Tooltip("Kayıt esnasında oyun içi HUD ve bu panel tamamen gizlensin mi? (Temiz sinematik video için)")]
        [SerializeField] private bool cleanFootageOnRecord = true;

        [Header("Kısayol Tuşları")]
        [SerializeField] private KeyCode toggleDirectorKey = KeyCode.F7;
        [SerializeField] private KeyCode toggleFormatKey = KeyCode.F8;
        [SerializeField] private KeyCode recordKey = KeyCode.F9;
        [SerializeField] private KeyCode slowMotionKey = KeyCode.F10;
        [SerializeField] private KeyCode toggleHudKey = KeyCode.H;

        // Kayıt durum köprüsü
        public static event Action OnStartRecordingRequested;
        public static event Action OnStopRecordingRequested;
        public static bool IsRecordingActive { get; set; }
        public static float CurrentRecordingTime { get; set; }

        private CinematicCameraController cameraController;
        private CinematicDirector director;
        private List<Canvas> disabledCanvases = new List<Canvas>();
        private float recordStartTime = 0f;

        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle badgeStyle;
        private GUIStyle recordStyle;
        private GUIStyle helpStyle;
        private bool stylesInitialized = false;
        private bool isGameplayScene;

        public bool CleanFootageOnRecord
        {
            get => cleanFootageOnRecord;
            set => cleanFootageOnRecord = value;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            cameraController = CinematicCameraController.Instance;
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CinematicCameraController>();
            }

            director = CinematicDirector.Instance;
            if (director == null)
            {
                director = FindFirstObjectByType<CinematicDirector>();
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            isGameplayScene = IsGameplayScene(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            isGameplayScene = IsGameplayScene(next);
        }

        private void Update()
        {
            if (ShouldShowGameplayControls()) HandleHotkeys();

            if (IsRecordingActive)
            {
                CurrentRecordingTime = Time.realtimeSinceStartup - recordStartTime;
            }
        }

        private void HandleHotkeys()
        {
            if (cameraController == null)
            {
                cameraController = CinematicCameraController.Instance ?? FindFirstObjectByType<CinematicCameraController>();
                if (cameraController == null) return;
            }

            if (director == null)
            {
                director = CinematicDirector.Instance ?? FindFirstObjectByType<CinematicDirector>();
            }

            // 1 - 7 Tuşları (Hem üst sayı sırası hem Numpad): Kamera Açıları
            for (int i = 0; i <= (int)CinematicCameraAngle.RoadsideFlyby; i++)
            {
                KeyCode numKey = KeyCode.Alpha1 + i;
                KeyCode padKey = KeyCode.Keypad1 + i;
#if ENABLE_INPUT_SYSTEM
                Key newNumKey = Key.Digit1 + i;
                Key newPadKey = Key.Numpad1 + i;
#endif
                bool pressed = WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                    newNumKey,
#endif
                    numKey) ||
                    WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                    newPadKey,
#endif
                    padKey);

                if (pressed)
                {
                    // Manuel kamera seçildiğinde Director modunu devre dışı bırak
                    if (director != null && director.IsDirectorActive)
                    {
                        director.SetDirector(false);
                    }
                    cameraController.SetAngle((CinematicCameraAngle)i, instantCut: true);
                }
            }

            // F7: Yönetmen Modu
            if (WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                Key.F7,
#endif
                toggleDirectorKey))
            {
                if (director != null)
                {
                    director.ToggleDirector();
                }
            }

            // F8: En-Boy Formatı (16:9 Trailer <-> 9:16 Reels)
            if (WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                Key.F8,
#endif
                toggleFormatKey))
            {
                cameraController.ToggleVideoFormat();
            }

            // F9: Kayıt Başlat / Durdur
            if (WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                Key.F9,
#endif
                recordKey))
            {
                ToggleRecording();
            }

            // F10: Ağır Çekim (Slow-Motion)
            if (WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                Key.F10,
#endif
                slowMotionKey))
            {
                cameraController.ToggleSlowMotion();
            }

            // H: HUD Göster / Gizle
            if (WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
                Key.H,
#endif
                toggleHudKey))
            {
                showHUD = !showHUD;
            }
        }

        private bool WasKeyPressed(
#if ENABLE_INPUT_SYSTEM
            Key newKey,
#endif
            KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                try
                {
                    var control = Keyboard.current[newKey];
                    if (control != null && control.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
                catch { }
            }
#endif
            try
            {
                if (Input.GetKeyDown(legacyKey))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public void ToggleRecording()
        {
            if (IsRecordingActive)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        public void StartRecording()
        {
            if (IsRecordingActive) return;

            IsRecordingActive = true;
            recordStartTime = Time.realtimeSinceStartup;
            CurrentRecordingTime = 0f;

            if (cleanFootageOnRecord)
            {
                HideSceneCanvases();
                ReverseCameraHUD.IsSuppressed = true;
                if (ReverseCameraHUD.Instance != null && ReverseCameraHUD.Instance.ReverseCamera != null)
                {
                    ReverseCameraHUD.Instance.ReverseCamera.enabled = false;
                }
            }

            OnStartRecordingRequested?.Invoke();
        }

        public void StopRecording()
        {
            if (!IsRecordingActive) return;

            IsRecordingActive = false;

            if (cleanFootageOnRecord)
            {
                RestoreSceneCanvases();
                if (cameraController == null || !cameraController.IsCinematicActive)
                {
                    ReverseCameraHUD.IsSuppressed = false;
                }
            }

            OnStopRecordingRequested?.Invoke();
        }

        private void HideSceneCanvases()
        {
            disabledCanvases.Clear();
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                Canvas c = allCanvases[i];
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                {
                    c.enabled = false;
                    disabledCanvases.Add(c);
                }
            }
        }

        private void RestoreSceneCanvases()
        {
            for (int i = 0; i < disabledCanvases.Count; i++)
            {
                if (disabledCanvases[i] != null)
                {
                    disabledCanvases[i].enabled = true;
                }
            }
            disabledCanvases.Clear();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.88f));

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 13;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

            badgeStyle = new GUIStyle(GUI.skin.label);
            badgeStyle.fontSize = 11;
            badgeStyle.normal.textColor = Color.white;

            recordStyle = new GUIStyle(GUI.skin.label);
            recordStyle.fontSize = 13;
            recordStyle.fontStyle = FontStyle.Bold;
            recordStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);

            helpStyle = new GUIStyle(GUI.skin.label);
            helpStyle.fontSize = 10;
            helpStyle.normal.textColor = new Color(0.75f, 0.8f, 0.85f);

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            if (!ShouldShowGameplayControls()) return;
            // Eğer kayıt yapılıyorsa ve temiz çekim aktifse HUD çizilmez
            if (IsRecordingActive && cleanFootageOnRecord) return;
            if (!showHUD) return;

            InitializeStyles();

            float panelWidth = 260f;
            float panelHeight = showHelpLegend ? 240f : 110f;
            Rect panelRect = new Rect(16f, 16f, panelWidth, panelHeight);

            GUI.Box(panelRect, GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 10f, panelRect.y + 8f, panelWidth - 20f, panelHeight - 16f));

            // Başlık
            GUILayout.Label("🎬 CINEMATIC CAPTURE", titleStyle);
            GUILayout.Space(2f);

            // Format ve Açı
            string formatText = cameraController != null && cameraController.CurrentFormat == CinematicVideoFormat.Reels_9_16
                ? "<color=#FF66CC><b>[REELS 9:16]</b></color>"
                : "<color=#33CCFF><b>[TRAILER 16:9]</b></color>";

            string angleText = cameraController != null ? cameraController.CurrentAngle.ToString() : "Chase";
            GUILayout.Label($"Format: {formatText}  |  Açı: <b>{angleText}</b>", badgeStyle);

            // Yönetmen Modu Durumu
            if (director != null && director.IsDirectorActive)
            {
                GUILayout.Label($"<color=#FFCC00><b>● YÖNETMEN MODU AKTİF</b></color> (Kesim: {director.TimeUntilNextCut:F1}s)", badgeStyle);
            }
            else
            {
                GUILayout.Label("<color=#888888>Yönetmen Modu: Kapalı</color>", badgeStyle);
            }

            // Kayıt Durumu
            if (IsRecordingActive)
            {
                bool blink = (int)(Time.realtimeSinceStartup * 2f) % 2 == 0;
                string dot = blink ? "● " : "  ";
                int mins = (int)(CurrentRecordingTime / 60f);
                int secs = (int)(CurrentRecordingTime % 60f);
                GUILayout.Label($"{dot}REC [{mins:00}:{secs:00}] (60 FPS)", recordStyle);
            }
            else
            {
                GUILayout.Label("<color=#66FF66>● Hazır (Kayıt Bekliyor)</color>", badgeStyle);
            }

            // Slow-Mo
            if (cameraController != null && cameraController.IsSlowMotionActive)
            {
                GUILayout.Label("<color=#FF9900><b>⚡ SLOW-MOTION (0.25x)</b></color>", badgeStyle);
            }

            if (showHelpLegend)
            {
                GUILayout.Space(6f);
                GUILayout.Box("", GUILayout.Height(1f), GUILayout.ExpandWidth(true));
                GUILayout.Label("<b>Kısayollar:</b>", helpStyle);
                GUILayout.Label("<b>1-7:</b> Kamera Açıları  |  <b>F7:</b> Yönetmen", helpStyle);
                GUILayout.Label("<b>F8:</b> Format (16:9 / 9:16)  |  <b>F9:</b> Kayıt", helpStyle);
                GUILayout.Label("<b>F10:</b> Ağır Çekim  |  <b>H:</b> Paneli Gizle", helpStyle);
            }

            GUILayout.EndArea();
        }

        private bool ShouldShowGameplayControls()
        {
            return isGameplayScene && Time.timeScale > 0f &&
                   !DeliveryDriver.UI.SceneTransitionManager.IsTransitioning;
        }

        private static bool IsGameplayScene(Scene scene)
        {
            return scene.name.Equals("Game", StringComparison.OrdinalIgnoreCase) ||
                   scene.name.Equals("GameScene", StringComparison.OrdinalIgnoreCase);
        }
    }
}
