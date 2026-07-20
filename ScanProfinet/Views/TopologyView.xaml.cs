using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScanProfinet.Models;
using ScanProfinet.ViewModels;

namespace ScanProfinet.Views;

public partial class TopologyView : UserControl
{
    public TopologyView() => InitializeComponent();

    private void NodeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.DataContext is TopoNode node)
        {
            double zoom = (DataContext as TopologyViewModel)?.Zoom ?? 1.0;
            if (zoom <= 0) zoom = 1.0;
            node.X += e.HorizontalChange / zoom;
            node.Y += e.VerticalChange / zoom;
        }
    }
}
