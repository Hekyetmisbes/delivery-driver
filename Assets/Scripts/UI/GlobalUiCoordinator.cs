using DeliveryDriver.Quest.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GlobalUiCoordinator : MonoBehaviour
{
    [SerializeField] private bool adoptExistingCanvases = true;
    [SerializeField] private bool includeWorldSpaceCanvases = false;
    [SerializeField] private int rootCanvasSortingOrder = 500;
    [SerializeField] private string rootCanvasName = "GlobalUICanvas";

    private static GlobalUiCoordinator instance;
    private Canvas rootCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
        {
            instance.RefreshSceneBindings();
            return;
        }

        GameObject rootObject = new GameObject("GlobalUIRoot");
        instance = rootObject.AddComponent<GlobalUiCoordinator>();
        DontDestroyOnLoad(rootObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildOrFindRootCanvas();
        EnsurePauseMenuComponent();
        RefreshSceneBindings();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneBindings();
    }

    private void RefreshSceneBindings()
    {
        BuildOrFindRootCanvas();
        EnsurePauseMenuComponent();
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

    private void ReparentSceneCanvases()
    {
        if (rootCanvas == null)
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

            if (canvas.transform.IsChildOf(rootCanvas.transform))
            {
                continue;
            }

            canvas.transform.SetParent(rootCanvas.transform, true);
            canvas.overrideSorting = true;
            canvas.sortingOrder = localSortingOrder++;
        }
    }
}
