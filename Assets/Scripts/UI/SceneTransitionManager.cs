using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeliveryDriver.UI
{
    public class SceneTransitionManager : MonoBehaviour
    {
        private static SceneTransitionManager instance;
        public static SceneTransitionManager Instance => instance;

        private Canvas transitionCanvas;
        private Image fadeImage;
        private bool isTransitioning;

        private const float FadeDuration = 0.5f;
        private const int SortingOrder = 999;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateTransitionCanvas();
        }

        public static void TransitionToScene(string sceneName)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SceneTransitionManager", typeof(SceneTransitionManager));
                DontDestroyOnLoad(go);
            }

            if (instance.isTransitioning) return;
            instance.StartCoroutine(instance.TransitionCoroutine(sceneName));
        }

        private IEnumerator TransitionCoroutine(string sceneName)
        {
            isTransitioning = true;
            Time.timeScale = 1f;

            if (fadeImage == null) CreateTransitionCanvas();
            fadeImage.gameObject.SetActive(true);
            transitionCanvas.gameObject.SetActive(true);

            // Fade to black
            yield return StartCoroutine(Fade(0f, 1f, FadeDuration));

            // Show loading screen
            LoadingScreenUI loadingScreen = GetComponentInChildren<LoadingScreenUI>(true);
            if (loadingScreen == null)
            {
                GameObject loadingObj = new GameObject("LoadingScreen", typeof(RectTransform), typeof(LoadingScreenUI));
                loadingObj.transform.SetParent(transitionCanvas.transform, false);
                RectTransform loadingRect = loadingObj.GetComponent<RectTransform>();
                loadingRect.anchorMin = Vector2.zero;
                loadingRect.anchorMax = Vector2.one;
                loadingRect.offsetMin = Vector2.zero;
                loadingRect.offsetMax = Vector2.zero;
                loadingScreen = loadingObj.GetComponent<LoadingScreenUI>();
            }

            loadingScreen.Show();

            // Start async load
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = false;

                while (asyncLoad.progress < 0.9f)
                {
                    loadingScreen.UpdateProgress(asyncLoad.progress / 0.9f);
                    yield return null;
                }

                loadingScreen.UpdateProgress(1f);
                yield return new WaitForSecondsRealtime(0.3f);

                asyncLoad.allowSceneActivation = true;

                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            loadingScreen.Hide();

            // Fade from black
            yield return StartCoroutine(Fade(1f, 0f, FadeDuration));

            fadeImage.gameObject.SetActive(false);
            isTransitioning = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            Color c = fadeImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(from, to, t);
                fadeImage.color = c;
                yield return null;
            }

            c.a = to;
            fadeImage.color = c;
        }

        private void CreateTransitionCanvas()
        {
            if (transitionCanvas != null) return;

            GameObject canvasObj = new GameObject("TransitionCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObj.transform.SetParent(transform, false);

            transitionCanvas = canvasObj.GetComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject fadeObj = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            fadeObj.transform.SetParent(canvasObj.transform, false);

            RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            fadeImage = fadeObj.GetComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = true;

            fadeObj.SetActive(false);
        }
    }
}
