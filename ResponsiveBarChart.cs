using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ResponsiveBarChart : MonoBehaviour
{
    // ----- Runtime data (only source of truth) -----
    private List<float> values = new();
    private List<string> xLabels = new();
    private bool hasData;

    // ----- Layout -----
    [Header("Layout")]
    [SerializeField] private Vector2 chartPadding = new Vector2(48, 32); // x: left, y: bottom
    [SerializeField] private float rightPadding = 10f;
    [SerializeField] private float topPadding = 10f;
    [SerializeField] private float barSpacing = 4f;
    [SerializeField] private float axisThickness = 2f;
    [SerializeField] private float tickLength = 6f;
    [SerializeField] private int yTickCount = 5;
    [SerializeField] private bool showYTicks = true;
    [SerializeField] private bool showXLabels = true;
    [SerializeField] private bool showGridLines = true;

    // ----- Scaling -----
    [Header("Scaling")]
    [SerializeField] private bool autoYMax = true;
    [SerializeField] private float yMax = 100f;
    [SerializeField] private bool useNiceNumbersForYMax = true;

    // ----- Style -----
    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0.11f,0.12f,0.14f,1f);
    [SerializeField] private Color barColor = new Color(0.25f,0.68f,0.88f,1f);
    [SerializeField] private Color axisColor = Color.white;
    [SerializeField] private Color tickColor = new Color(1f,1f,1f,0.7f);
    [SerializeField] private Color labelColor = new Color(1f,1f,1f,0.9f);
    [SerializeField] private Color gridColor = new Color(1f,1f,1f,0.12f);

    [Header("Fonts (TMP)")]
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField, Min(6)] private int labelFontSize = 12;

    // ----- Animation -----
    [Header("Animation")]
    [SerializeField] private bool animateOnSet = true;
    [SerializeField, Min(0f)] private float animateDuration = 0.25f;

    // ----- Internals -----
    private RectTransform rt;
    private RectTransform chartArea;
    private RectTransform barsRoot;
    private RectTransform axesRoot;
    private RectTransform ticksRoot;
    private RectTransform labelsRoot;
    private Image backgroundImg;

    private readonly List<Image> barRects = new();
    private readonly List<TextMeshProUGUI> xLabelTexts = new();
    private readonly List<Image> gridLines = new();
    private readonly List<float> animFromHeights = new();
    private float animTime;

    private bool _isRebuilding;
    private bool _suppressDimensionChange;

    // ===== Public API =====

    public void SetData(IList<float> newValues, IList<string> newXLabels = null)
    {
        values = newValues != null ? new List<float>(newValues) : new List<float>();
        if (newXLabels != null)
            xLabels = new List<string>(newXLabels);
        else
        {
            xLabels = new List<string>(values.Count);
            for (int i = 0; i < values.Count; i++) xLabels.Add((i + 1).ToString());
        }
        hasData = true;

        PrepareAnimationFromCurrent();
        BuildInternal();
        animTime = animateOnSet ? 0f : animateDuration;
        ApplyBarHeights(animateOnSet ? 0f : 1f);
    }

    public void SetDataAutoLabels(IList<float> newValues) => SetData(newValues, null);

    public void SetTheme(Color? bg=null, Color? bar=null, Color? axis=null, Color? tick=null, Color? label=null, Color? grid=null)
    {
        if (bg.HasValue) backgroundColor = bg.Value;
        if (bar.HasValue) barColor = bar.Value;
        if (axis.HasValue) axisColor = axis.Value;
        if (tick.HasValue) tickColor = tick.Value;
        if (label.HasValue) labelColor = label.Value;
        if (grid.HasValue) gridColor = grid.Value;
        BuildInternal();
    }

    public void SetYAxisMax(float max, bool auto=false)
    {
        autoYMax = auto;
        yMax = Mathf.Max(0.0001f, max);
        BuildInternal();
    }

    // ===== Unity =====

    private void Awake()
    {
        EnsureHierarchy();
        // Don’t draw anything until SetData is called (avoids any “default/preview” rendering).
        ClearChildren(axesRoot);
        ClearChildren(ticksRoot);
        ClearChildren(labelsRoot);
        ClearBars();
        backgroundImg.color = backgroundColor;
    }

    private void Update()
    {
        if (!hasData) return;
        if (!animateOnSet) return;
        if (animTime >= animateDuration) return;

        animTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(animateDuration <= 0f ? 1f : animTime / animateDuration);
        ApplyBarHeights(t);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_suppressDimensionChange) return;
        if (!hasData) return; // nothing to layout until data arrives
        BuildInternal();
    }

    // ===== Build =====

    private void EnsureHierarchy()
    {
        rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();

        backgroundImg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        backgroundImg.raycastTarget = false;

        chartArea = CreateOrGetRect("ChartArea", transform as RectTransform);
        barsRoot  = CreateOrGetRect("Bars", chartArea);
        axesRoot  = CreateOrGetRect("Axes", chartArea);
        ticksRoot = CreateOrGetRect("Ticks", chartArea);
        labelsRoot= CreateOrGetRect("Labels", chartArea);

        barsRoot.SetAsFirstSibling();
    }

    private RectTransform CreateOrGetRect(string name, RectTransform parent)
    {
        var t = parent.Find(name);
        if (t != null) return (RectTransform)t;
        var go = new GameObject(name, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return r;
    }

    private void BuildInternal()
    {
        if (!hasData) return;
        if (_isRebuilding) return;
        _isRebuilding = true;
        _suppressDimensionChange = true;

        try
        {
            backgroundImg.color = backgroundColor;

            // Layout area
            chartArea.anchorMin = Vector2.zero;
            chartArea.anchorMax = Vector2.one;
            chartArea.offsetMin = new Vector2(chartPadding.x, chartPadding.y);
            chartArea.offsetMax = new Vector2(-rightPadding, -topPadding);

            int n = Mathf.Max(0, values?.Count ?? 0);

            // Rebuild overlays each time (clean slate)
            ClearChildren(axesRoot);
            ClearChildren(ticksRoot);
            ClearChildren(labelsRoot);

            DrawAxes();

            float targetMax = autoYMax ? ComputeAutoYMax(values) : Mathf.Max(0.0001f, yMax);
            if (useNiceNumbersForYMax) targetMax = NiceCeil(targetMax);

            if (yTickCount > 0 && showYTicks) DrawYTicksAndGrid(targetMax);

            // Bars
            BuildBars(n);

            // X labels
            if (showXLabels) BuildXLabels(n, xLabels);
        }
        finally
        {
            _suppressDimensionChange = false;
            _isRebuilding = false;
        }
    }

    // ----- Axes / Ticks / Labels -----

    private void DrawAxes()
    {
        var xAxis = CreateLine("X-Axis", axesRoot);
        var yAxis = CreateLine("Y-Axis", axesRoot);

        // X axis (bottom)
        var xr = xAxis.rectTransform;
        xr.anchorMin = new Vector2(0, 0);
        xr.anchorMax = new Vector2(1, 0);
        xr.pivot = new Vector2(0.5f, 0.5f);
        xr.sizeDelta = new Vector2(0, axisThickness);
        xr.anchoredPosition = Vector2.zero;
        xAxis.color = axisColor;

        // Y axis (left)
        var yr = yAxis.rectTransform;
        yr.anchorMin = new Vector2(0, 0);
        yr.anchorMax = new Vector2(0, 1);
        yr.pivot = new Vector2(0.5f, 0.5f);
        yr.sizeDelta = new Vector2(axisThickness, 0);
        yr.anchoredPosition = Vector2.zero;
        yAxis.color = axisColor;
    }

    private Image CreateLine(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        go.transform.SetParent(parent, false);
        return img;
    }

    private float ComputeAutoYMax(IList<float> vals)
    {
        if (vals == null || vals.Count == 0) return 1f;
        float max = 0f;
        for (int i = 0; i < vals.Count; i++) max = Mathf.Max(max, vals[i]);
        if (Mathf.Approximately(max, 0f)) max = 1f;
        return max;
    }

    private float NiceCeil(float x)
    {
        x = Mathf.Max(0.0001f, x);
        float exp = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(x)));
        float f = x / exp;
        float nf = (f <= 1f) ? 1f : (f <= 2f) ? 2f : (f <= 5f) ? 5f : 10f;
        return nf * exp;
    }

    private void DrawYTicksAndGrid(float yMaxVal)
    {
        if (!showYTicks && !showGridLines) return;

        var area = chartArea.rect;
        float height = Mathf.Max(1f, area.height);

        for (int i = 1; i <= yTickCount; i++)
        {
            float t = (float)i / yTickCount;
            float y = t * height;

            // Tick
            if (showYTicks)
            {
                var tick = CreateLine($"yTick{i}", ticksRoot);
                var tr = tick.rectTransform;
                tr.anchorMin = new Vector2(0, 0);
                tr.anchorMax = new Vector2(0, 0);
                tr.pivot = new Vector2(0, 0.5f);
                tr.sizeDelta = new Vector2(tickLength, axisThickness);
                tr.anchoredPosition = new Vector2(0, y);
                tick.color = tickColor;

                // Label
                var label = CreateLabel($"yLabel{i}", labelsRoot);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.color = labelColor;
                label.font = labelFont;
                label.fontSize = labelFontSize;

                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0, 0);
                lr.anchorMax = new Vector2(0, 0);
                lr.pivot = new Vector2(1f, 0.5f);
                lr.sizeDelta = new Vector2(chartPadding.x - 6f, 18f);
                lr.anchoredPosition = new Vector2(-6f, y);

                float tickValue = t * yMaxVal;
                label.text = FormatNumber(tickValue);
            }

            // Grid
            if (showGridLines)
            {
                var grid = CreateLine($"yGrid{i}", ticksRoot);
                var gr = grid.rectTransform;
                gr.anchorMin = new Vector2(0, 0);
                gr.anchorMax = new Vector2(1, 0);
                gr.pivot = new Vector2(0.5f, 0.5f);
                gr.sizeDelta = new Vector2(0, 1f);
                gr.anchoredPosition = new Vector2(0, y);
                grid.color = gridColor;
                gridLines.Add(grid);
            }
        }
    }

    private TextMeshProUGUI CreateLabel(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        go.transform.SetParent(parent, false);
        return text;
    }

    private string FormatNumber(float v)
    {
        float av = Mathf.Abs(v);
        if (av >= 1_000_000f) return (v / 1_000_000f).ToString("0.#") + "M";
        if (av >= 1_000f)     return (v / 1_000f).ToString("0.#") + "k";
        if (av >= 1f)         return v.ToString("0.#");
        return v.ToString("0.###");
    }

    // ----- Bars & X Labels -----

    private void BuildBars(int n)
    {
        EnsureBarPool(n);

        var area = chartArea.rect;
        float width = Mathf.Max(1f, area.width);

        float totalSpacing = Mathf.Max(0, (n - 1)) * barSpacing;
        float barW = (n > 0) ? Mathf.Max(1f, (width - totalSpacing) / n) : 0f;

        for (int i = 0; i < barRects.Count; i++)
        {
            var img = barRects[i];
            img.enabled = i < n;
            if (i >= n) continue;

            img.color = barColor;
            var r = img.rectTransform;

            r.anchorMin = new Vector2(0, 0);
            r.anchorMax = new Vector2(0, 0);
            r.pivot = new Vector2(0.5f, 0f);

            float x = (barW + barSpacing) * i + barW * 0.5f;
            r.anchoredPosition = new Vector2(x, 0f);
            r.sizeDelta = new Vector2(barW, 1f); // height animated in ApplyBarHeights
        }
    }

    private void EnsureBarPool(int n)
    {
        while (barRects.Count < n)
        {
            var go = new GameObject($"Bar_{barRects.Count}", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            go.transform.SetParent(barsRoot, false);
            barRects.Add(img);
        }
        for (int i = barRects.Count - 1; i >= n; i--)
        {
            var img = barRects[i];
            DestroyImmediateSafe(img.gameObject);
            barRects.RemoveAt(i);
        }
    }

    private void ClearBars()
    {
        for (int i = barsRoot.childCount - 1; i >= 0; i--)
            DestroyImmediateSafe(barsRoot.GetChild(i).gameObject);
        barRects.Clear();
    }

    private void BuildXLabels(int n, IList<string> lbls)
    {
        EnsureXLabelPool(n);

        var area = chartArea.rect;
        float width = Mathf.Max(1f, area.width);
        float totalSpacing = Mathf.Max(0, (n - 1)) * barSpacing;
        float barW = (n > 0) ? Mathf.Max(1f, (width - totalSpacing) / n) : 0f;

        for (int i = 0; i < xLabelTexts.Count; i++)
        {
            var t = xLabelTexts[i];
            t.enabled = i < n;
            if (i >= n) continue;

            string label = (i < (lbls?.Count ?? 0)) ? lbls[i] : (i + 1).ToString();
            t.text = label;
            t.color = labelColor;
            t.font = labelFont;
            t.fontSize = labelFontSize;
            t.alignment = TextAlignmentOptions.Top;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;

            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 0);
            r.anchorMax = new Vector2(0, 0);
            r.pivot = new Vector2(0.5f, 1f);

            float x = (barW + barSpacing) * i + barW * 0.5f;
            r.anchoredPosition = new Vector2(x, -4f);
            r.sizeDelta = new Vector2(Mathf.Max(24f, barW + 6f), 18f);
        }
    }

    private void EnsureXLabelPool(int n)
    {
        while (xLabelTexts.Count < n)
        {
            var go = new GameObject($"XLabel_{xLabelTexts.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            go.transform.SetParent(labelsRoot, false);
            xLabelTexts.Add(text);
        }
        for (int i = xLabelTexts.Count - 1; i >= n; i--)
        {
            var t = xLabelTexts[i];
            DestroyImmediateSafe(t.gameObject);
            xLabelTexts.RemoveAt(i);
        }
    }

    private void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediateSafe(root.GetChild(i).gameObject);

        if (root == labelsRoot) xLabelTexts.Clear();
        if (root == ticksRoot)  gridLines.Clear();
        if (root == axesRoot)   { /* nothing pooled here */ }
    }

    private void DestroyImmediateSafe(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ----- Animation -----

    private void PrepareAnimationFromCurrent()
    {
        animFromHeights.Clear();
        for (int i = 0; i < barRects.Count; i++)
            animFromHeights.Add(barRects[i].rectTransform.sizeDelta.y);
        while (animFromHeights.Count < values.Count) animFromHeights.Add(0f);
    }

    private void ApplyBarHeights(float t)
    {
        if (barRects.Count == 0) return;

        var area = chartArea.rect;
        float height = Mathf.Max(1f, area.height);
        float targetMax = autoYMax ? ComputeAutoYMax(values) : Mathf.Max(0.0001f, yMax);
        if (useNiceNumbersForYMax) targetMax = NiceCeil(targetMax);

        for (int i = 0; i < barRects.Count; i++)
        {
            float v = (i < values.Count) ? Mathf.Max(0f, values[i]) : 0f;
            float endH = (targetMax <= 0f) ? 0f : (v / targetMax) * height;
            float startH = (i < animFromHeights.Count) ? animFromHeights[i] : 0f;
            float h = Mathf.Lerp(startH, endH, t * t * (3f - 2f * t)); // smoothstep

            var r = barRects[i].rectTransform;
            r.sizeDelta = new Vector2(r.sizeDelta.x, h);
        }
    }
}
