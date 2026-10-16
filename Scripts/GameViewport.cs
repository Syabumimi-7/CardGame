using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One 16:9 presentation area for the scene camera, UI, and pointer input.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class GameViewport : MonoBehaviour
{
    public const float Aspect = 16f / 9f;
    private static GameViewport instance;
    private readonly Dictionary<Canvas, RectTransform> contents = new Dictionary<Canvas, RectTransform>();
    private readonly List<Canvas> stale = new List<Canvas>();
    private Canvas barsCanvas;
    private RectTransform left, right, bottom, top;
    private bool applying;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureStartupDisplay()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Resolution tests deliberately open a window at specific sizes.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--compatibility-audit") >= 0) return;
        // Override a window size/fullscreen mode remembered from an earlier run.
        Resolution desktop = Screen.currentResolution;
        Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.FullScreenWindow);
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;
        instance = new GameObject("GameViewport").AddComponent<GameViewport>();
        DontDestroyOnLoad(instance.gameObject);
        instance.CreateBars();
        instance.Refresh();
    }

    public static Rect CalculateRect(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return new Rect(0, 0, 1, 1);
        float ratio = (float)width / height;
        if (ratio > Aspect)
        {
            float w = Aspect / ratio;
            return new Rect((1 - w) * .5f, 0, w, 1);
        }

        float h = ratio / Aspect;
        return new Rect(0, (1 - h) * .5f, 1, h);
    }

    public static RectTransform ContentFor(Canvas canvas)
    {
        if (canvas == null)
            return null;
        var root = canvas.rootCanvas;
        if (!Application.isPlaying || root.renderMode == RenderMode.WorldSpace)
            return root.transform as RectTransform;
        if (instance == null)
            Install();
        return instance.EnsureContent(root);
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += Refresh;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Refresh;
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (applying || barsCanvas == null || Screen.width <= 0 || Screen.height <= 0)
            return;
        applying = true;
        try
        {
            Rect area = CalculateRect(Screen.width, Screen.height);
            var camera = Camera.main;
            if (camera != null && camera.targetTexture == null)
                camera.rect = area;
            SetBar(left, Vector2.zero, new Vector2(area.xMin, 1));
            SetBar(right, new Vector2(area.xMax, 0), Vector2.one);
            SetBar(bottom, new Vector2(area.xMin, 0), new Vector2(area.xMax, area.yMin));
            SetBar(top, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, 1));
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                if (canvas != null && canvas.isRootCanvas && canvas.gameObject.scene.IsValid() && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas != barsCanvas)
                    EnsureContent(canvas);
            stale.Clear();
            foreach (var pair in contents)
                if (pair.Key == null)
                    stale.Add(pair.Key);
            foreach (var canvas in stale)
                contents.Remove(canvas);
        }
        finally
        {
            applying = false;
        }
    }

    private RectTransform EnsureContent(Canvas canvas)
    {
        if (!contents.TryGetValue(canvas, out var content) || content == null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            Vector2 reference = scaler.referenceResolution;
            if (reference.x <= 0 || reference.y <= 0 || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                reference = new Vector2(1920, 1080);
            // Preserve the authored reference width while giving every canvas the same aspect.
            reference.y = reference.x / Aspect;
            scaler.referenceResolution = reference;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var obj = new GameObject("GameContent16x9", typeof(RectTransform));
            obj.layer = canvas.gameObject.layer;
            content = (RectTransform)obj.transform;
            content.SetParent(canvas.transform, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = reference;
            contents[canvas] = content;
        }

        // New overlays also enter the same area, including those opened while paused.
        for (int i = 0; i < canvas.transform.childCount;)
        {
            var child = canvas.transform.GetChild(i);
            if (child != content)
                child.SetParent(content, false);
            else
                i++;
        }

        return content;
    }

    private void CreateBars()
    {
        var root = new GameObject("Letterbox", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        barsCanvas = root.GetComponent<Canvas>();
        barsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        barsCanvas.sortingOrder = short.MaxValue;
        left = CreateBar("Left");
        right = CreateBar("Right");
        bottom = CreateBar("Bottom");
        top = CreateBar("Top");
    }

    private RectTransform CreateBar(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(barsCanvas.transform, false);
        obj.GetComponent<Image>().color = Color.black;
        return (RectTransform)obj.transform;
    }

    private static void SetBar(RectTransform bar, Vector2 min, Vector2 max)
    {
        bar.anchorMin = min;
        bar.anchorMax = max;
        bar.offsetMin = bar.offsetMax = Vector2.zero;
        bar.gameObject.SetActive(max.x > min.x + .00001f && max.y > min.y + .00001f);
    }
}
