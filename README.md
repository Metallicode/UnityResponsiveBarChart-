# UnityResponsiveBarChart-
A Unity UI oriented ResponsiveBarChart element


# ResponsiveBarChart (Unity, TMP)

A tiny, flexible **UI bar chart** for Unity (uGUI + TextMeshPro).

* **Always fits its parent** (1 to 100+ bars)
* **Auto bar width** based on data count
* **Runtime only** (no editor preview to avoid mixing)
* **Axes, ticks, labels, grid**
* **Easy theming** (colors, fonts, spacing)
* **API-friendly**: call `SetData(...)` whenever new data arrives

---

## Requirements

* Unity **2021.3+**
* **TextMeshPro** (TMP Essentials imported)
* Unity UI (uGUI)

---

## Install

1. Copy `ResponsiveBarChart.cs` into your project (e.g., `Assets/UICharts/`).
2. In a Canvas, create an **Empty** GameObject → name it `BarChart`.
3. Add **Image** (optional—script will add one) and **ResponsiveBarChart** component.
4. Stretch the `BarChart` **RectTransform** to fill its parent (anchors full stretch, offsets 0).

---

## Quick Start

```csharp
public class Example : MonoBehaviour
{
    [SerializeField] private ResponsiveBarChart chart;

    void Start()
    {
        // Simplest: auto labels 1..N
        chart.SetDataAutoLabels(new float[] { 12, 34, 22, 50, 17 });

        // Or with custom labels (e.g., days)
        // chart.SetData(
        //     new float[] { 5, 15, 10, 25, 40, 12, 30 },
        //     new string[] { "Mon","Tue","Wed","Thu","Fri","Sat","Sun" }
        // );
    }
}
```

> Nothing is drawn until you call `SetData(...)`. After that, call it anytime to update the chart with fresh data from your API.

---

## Public API

```csharp
// Set values and optional labels
void SetData(IList<float> values, IList<string> labels = null);

// Auto-generate labels "1..N"
void SetDataAutoLabels(IList<float> values);

// Theme helpers
void SetTheme(
    Color? background = null,
    Color? bar = null,
    Color? axis = null,
    Color? tick = null,
    Color? label = null,
    Color? grid = null
);

// Y axis scaling
void SetYAxisMax(float max, bool auto = false);
```

---

## Inspector Options (highlights)

* **Layout**

  * `chartPadding` (left/bottom), `rightPadding`, `topPadding`
  * `barSpacing`, `axisThickness`, `tickLength`, `yTickCount`
  * `showYTicks`, `showXLabels`, `showGridLines`
* **Scaling**

  * `autoYMax` (on by default)
  * `yMax` (used if `autoYMax == false`)
  * `useNiceNumbersForYMax`
* **Style**

  * `backgroundColor`, `barColor`, `axisColor`, `tickColor`, `labelColor`, `gridColor`
  * `labelFont` (TMP Font Asset), `labelFontSize`
* **Animation**

  * `animateOnSet`, `animateDuration`

---

## Tips

* For **dates**, format them before passing to `SetData`:

  ```csharp
  var values = new float[] { 3, 6, 4, 9 };
  var labels = new [] { "2025-09-01", "2025-09-02", "2025-09-03", "2025-09-04" };
  chart.SetData(values, labels);
  ```
* If your parent uses `LayoutGroup`/`ContentSizeFitter`, the chart will still fit—
  it listens to size changes and reflows bars automatically.

---

## Roadmap (nice-to-haves)

* Rounded bars / per-bar colors
* Tooltips & selection callbacks
* Stacked / grouped bars
* Optional editor preview (separate, safe tool)

---

## Troubleshooting

* **Bars appear then “snap back” to 5**
  You’re likely using an older preview-enabled version. This repo uses **runtime-only** data—make sure your scene has the script from this project and you call `SetData(...)` at runtime.

* **Nothing shows**
  Ensure `SetData(...)` is called after the GameObject is active and there are values. Also confirm the chart GameObject is under a **Canvas**.

* **Text not visible**
  Assign a TMP **Font Asset** in the inspector and check label color/size.

---

## Folder Structure (suggested)

```
Assets/
  UICharts/
    ResponsiveBarChart.cs
    Examples/
      ExampleScene.unity
      ExampleController.cs
```

---

## Contributing

PRs welcome! Keep it dependency-light (uGUI + TMP) and editor-safe. Add GIFs/screens in your PR if possible.
