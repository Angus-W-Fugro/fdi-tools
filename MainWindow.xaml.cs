using System.Drawing;

namespace FDITools;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private MainModel _Model;

    public MainWindow()
    {
        _Model = new MainModel();
        DataContext = _Model;
        InitializeComponent();

        _Model.CreatePlotAction = CreatePlot;
    }

    public void CreatePlot(double[] xs, double[] ys, Color color)
    {
        WpfPlot.Plot.AddScatterPoints(xs, ys, color);
        WpfPlot.Plot.AxisAuto();
        WpfPlot.Refresh();
    }
}