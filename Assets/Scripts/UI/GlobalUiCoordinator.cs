using DeliveryDriver.Quest.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GlobalUiCoordinator : MonoBehaviour
{
    [SerializeField] private bool adoptExistingCanvases = true;
    [SerializeField] private bool includeWorldSpaceCanvases = false;
    [SerializeField] private int rootCanvasSortingOrder = 500;
    [SerializeField] private string rootCanvasName = "GlobalUICanvas";
    [SerializeField] private string canvasGroupRootName = "GlobalUICanvasGroups";
    [SerializeField] private float runtimeAdoptionInterval = 0.5f;
    [SerializeField] private bool continuousRuntimeAdoption = false;

    private static GlobalUiCoordinator instance;
    private Canvas rootCanvas;
    private RectTransform canvasGroupRoot;
    private float lastRuntimeAdoptionTime;

    public static Canvas PrimaryCanvas
    {
        get
        {
            EnsureInstance();
            return instance != null ? instance.rootCanvas : null;
        }
    }

    public static Transform CanvasGroupRoot
    {
        get
        {
            EnsureInstance();
            return instance != null ? instance.canvasGroupRoot : null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (!ResolveExistingInstance() && instance == null)
        {
            GameObject rootObject = new GameObject("GlobalUIRoot");
            instance = rootObject.AddComponent<GlobalUiCoordinator>();
        }

        if (instance != null)
        {
            instance.RefreshSceneBindings();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        BuildOrFindRootCanvas();
        BuildOrFindCanvasGroupRoot();
        EnsurePauseMenuComponent();
        EnsureBalanceHudComponent();
        EnsureBorderRespawnGuardComponent();
        EnsureMinimapComponent();
        EnsureFuelHudComponent();
        RefreshSceneBindings();
    }

    private void OnDestroy()
    {
        if (instance == this && Application.isPlaying)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneBindings();
    }

    private void LateUpdate()
    {
        if (!adoptExistingCanvases)
        {
            return;
        }

        if (!Application.isPlaying || !continuousRuntimeAdoption)
        {
            return;
        }

        if (Time.unscaledTime - lastRuntimeAdoptionTime < Mathf.Max(0.1f, runtimeAdoptionInterval))
        {
            return;
        }

        lastRuntimeAdoptionTime = Time.unscaledTime;
        ReparentSceneCanvases();
    }

    private static bool ResolveExistingInstance()
    {
        if (instance != null)
        {
            return true;
        }

        instance = FindFirstObjectByType<GlobalUiCoordinator>();
        return instance != null;
    }

    private void RefreshSceneBindings()
    {
        BuildOrFindRootCanvas();
        BuildOrFindCanvasGroupRoot();
        EnsurePauseMenuComponent();
        EnsureBalanceHudComponent();
        EnsureBorderRespawnGuardComponent();
        EnsureMinimapComponent();
        EnsureFuelHudComponent();
        if (adoptExistingCanvases)
        {
            ReparentSceneCanvases();
        }
    }

    private void BuildOrFindRootCanvas()
    {
        if (rootCanvas != null)
        {
            return;
        }

        Transform existing = transform.Find(rootCanvasName);
        if (existing != null)
        {
            rootCanvas = existing.GetComponent<Canvas>();
            if (rootCanvas != null)
            {
                return;
            }
        }

        GameObject canvasObject = new GameObject(rootCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = rootCanvasSortingOrder;
        rootCanvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildOrFindCanvasGroupRoot()
    {
        if (rootCanvas == null)
        {
            return;
        }

        if (canvasGroupRoot != null)
        {
            return;
        }

        Transform existing = rootCanvas.transform.Find(canvasGroupRootName);
        if (existing != null)
        {
            canvasGroupRoot = existing as RectTransform;
            if (canvasGroupRoot != null)
            {
                EnsureCanvasGroup(canvasGroupRoot.gameObject);
                return;
            }
        }

        GameObject groupRootObject = new GameObject(canvasGroupRootName, typeof(RectTransform), typeof(CanvasGroup));
        groupRootObject.transform.SetParent(rootCanvas.transform, false);
        canvasGroupRoot = groupRootObject.GetComponent<RectTransform>();
        canvasGroupRoot.anchorMin = Vector2.zero;
        canvasGroupRoot.anchorMax = Vector2.one;
        canvasGroupRoot.pivot = new Vector2(0.5f, 0.5f);
        canvasGroupRoot.offsetMin = Vector2.zero;
        canvasGroupRoot.offsetMax = Vector2.zero;
    }

    private void EnsurePauseMenuComponent()
    {
        if (FindFirstObjectByType<PauseMenuUI>() != null)
        {
            return;
        }

        PauseMenuUI pauseMenu = gameObject.GetComponent<PauseMenuUI>();
        if (pauseMenu == null)
        {
            gameObject.AddComponent<PauseMenuUI>();
        }
    }

    private void EnsureBalanceHudComponent()
    {
        if (GetComponent<BalanceHudUI>() == null)
        {
            gameObject.AddComponent<BalanceHudUI>();
        }
    }

    private void EnsureBorderRespawnGuardComponent()
    {
        if (GetComponent<BorderRespawnGuard>() == null)
        {
            gameObject.AddComponent<BorderRespawnGuard>();
        }
    }

    private void EnsureFuelHudComponent()
    {
        if (GetComponent<FuelHudUI>() == null)
        {
            gameObject.AddComponent<FuelHudUI>();
        }
    }

    private void EnsureMinimapComponent()
    {
        if (GetComponent<MinimapUI>() == null)
        {
            gameObject.AddComponent<MinimapUI>();
        }
    }

    private void ReparentSceneCanvases()
    {
        if (rootCanvas == null || canvasGroupRoot == null)
        {
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        int localSortingOrder = rootCanvasSortingOrder + 1;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas == rootCanvas)
            {
                continue;
            }

            if (!includeWorldSpaceCanvases && canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            // Skip canvases that have their own ScaleWithScreenSize scaler
            // (e.g. Quest UI Canvas) to preserve their CanvasScaler behavior
            CanvasScaler existingScaler = canvas.GetComponent<CanvasScaler>();
            if (existingScaler != null && existingScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                continue;
            }

            if (canvas.transform.IsChildOf(rootCanvas.transform))
            {
                if (!canvas.transform.IsChildOf(canvasGroupRoot))
                {
                    canvas.transform.SetParent(canvasGroupRoot, false);
                }
            }
            else
            {
                canvas.transform.SetParent(canvasGroupRoot, false);
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = localSortingOrder++;
            EnsureCanvasGroup(canvas.gameObject);
        }
    }

    private static void EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EnsureEditorSceneInstance()
    {
        EditorApplication.delayCall += TryEnsureEditorSceneInstance;
    }

    private static void TryEnsureEditorSceneInstance()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (ResolveExistingInstance())
        {
            instance.RefreshSceneBindings();
            return;
        }

        GameObject rootObject = new GameObject("GlobalUIRoot");
        instance = rootObject.AddComponent<GlobalUiCoordinator>();
        instance.RefreshSceneBindings();
    }
#endif
}
