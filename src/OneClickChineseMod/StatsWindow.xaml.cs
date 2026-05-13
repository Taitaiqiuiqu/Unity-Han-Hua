using System.Windows;
using System.Windows.Input;

namespace OneClickChineseMod;

public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
    }

    public void SetStats(int totalRequests, int successCount, int failCount, int cacheHits, double avgLatencyMs)
    {
        StatTotalRequests.Text = totalRequests.ToString();
        StatSuccessCount.Text = successCount.ToString();
        StatFailCount.Text = failCount.ToString();
        StatCacheHits.Text = cacheHits.ToString();
        StatAvgLatency.Text = $"{avgLatencyMs:F0} ms";

        var total = successCount + failCount;
        var successRate = total > 0 ? (double)successCount / total * 100 : 0;
        StatSuccessRate.Text = $"{successRate:F1}%";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
