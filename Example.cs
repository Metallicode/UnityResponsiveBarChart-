using UnityEngine;


public class Example : MonoBehaviour
{
    [SerializeField] private ResponsiveBarChart chart;

    private void Start()
    {
        // Minimal: values only
        // chart.SetDataAutoLabels(new float[] { 12, 34, 22, 50, 17 });

        // With labels (numbers or dates)
        chart.SetData(
            new float[] { 5, 15, 10, 25, 40, 12, 30 , 18 },
            new string[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" , "Next Mon" }
        );

        // Or with DateTime
        // chart.SetDataWithDates(values, dateList, "MMM d");
    }

    public void OnNewApiData(float[] values, string[] labels)
    {
        chart.SetData(values, labels); // bars auto-resize to fit
    }
}
