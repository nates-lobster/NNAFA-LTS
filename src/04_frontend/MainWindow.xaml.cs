using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Nnafa.Telemetry.V1;
using ScottPlot;
using ScottPlot.WPF;
using Color = ScottPlot.Color;

namespace Frontend
{
    public partial class MainWindow : Window
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly CancellationTokenSource _cts = new();
        
        // Data buffers for ScottPlot
        private readonly double[] _tp9Data = new double[512];
        private readonly double[] _af7Data = new double[512];
        private readonly double[] _af8Data = new double[512];
        private readonly double[] _tp10Data = new double[512];
        private int _dataIndex = 0;
        
        // Data buffers for PSD
        private readonly double[] _psdFreqs = new double[101];
        private readonly double[] _psdPowers = new double[101];

        public MainWindow()
        {
            InitializeComponent();
            _telemetryClient = new TelemetryClient();
            _telemetryClient.OnTelemetryReceived += HandleTelemetry;
            
            SetupPlots();
        }

        private void SetupPlots()
        {
            var plots = new[] { WpfPlotTP9, WpfPlotAF7, WpfPlotAF8, WpfPlotTP10 };
            var titles = new[] { "TP9", "AF7", "AF8", "TP10" };
            var dataArrays = new[] { _tp9Data, _af7Data, _af8Data, _tp10Data };

            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].Plot.Add.Signal(dataArrays[i]);
                plots[i].Plot.Title(titles[i]);
                plots[i].Plot.Axes.SetLimitsY(-150, 150);
                
                // Styling for Dark Mode
                plots[i].Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
                plots[i].Plot.DataBackground.Color = Color.FromHex("#313244");
                plots[i].Plot.Axes.Color(Color.FromHex("#A6ADC8"));
                plots[i].Refresh();
            }
            
            SetupPsdPlot(WpfPlotPSD_Monitor);
            SetupPsdPlot(WpfPlotPSD_NFB);
        }

        private void SetupPsdPlot(WpfPlot plot)
        {
            plot.Plot.Title("Power Spectral Density");
            plot.Plot.XLabel("Frequency (Hz)");
            plot.Plot.YLabel("Power");
            plot.Plot.Axes.SetLimits(0, 100, 0, 100); // Default Magnitude limits
            
            // Background Shading for Brainwaves using reliable RGBA byte constructor (R, G, B, A)
            byte alpha = 60; // Semi-transparent
            plot.Plot.Add.HorizontalSpan(1, 4, new Color(100, 100, 100, alpha)); // Delta - Gray
            plot.Plot.Add.HorizontalSpan(4, 8, new Color(128, 0, 128, alpha));   // Theta - Purple
            plot.Plot.Add.HorizontalSpan(8, 12, new Color(0, 128, 0, alpha));    // Alpha - Green
            plot.Plot.Add.HorizontalSpan(12, 30, new Color(0, 0, 128, alpha));   // Beta - Blue
            plot.Plot.Add.HorizontalSpan(30, 40, new Color(128, 128, 0, alpha)); // Gamma - Yellow
            
            var scatter = plot.Plot.Add.Scatter(_psdFreqs, _psdPowers);
            scatter.Color = Color.FromHex("#89B4FA");
            scatter.LineWidth = 2;
            
            plot.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            plot.Plot.DataBackground.Color = Color.FromHex("#313244");
            plot.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            plot.Refresh();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                await _telemetryClient.ConnectAsync(_cts.Token);
                
                _ = Task.Run(async () =>
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        await Task.Delay(50); // 20 FPS refresh
                        await Dispatcher.InvokeAsync(() =>
                        {
                            WpfPlotTP9.Refresh();
                            WpfPlotAF7.Refresh();
                            WpfPlotAF8.Refresh();
                            WpfPlotTP10.Refresh();
                            
                            WpfPlotPSD_Monitor.Refresh();
                            WpfPlotPSD_NFB.Refresh();
                        });
                    }
                }, _cts.Token);
            }
        }

        private void HandleTelemetry(TelemetryPayload payload)
        {
            // Update Ring Buffers
            _tp9Data[_dataIndex] = payload.Channels.Tp9;
            _af7Data[_dataIndex] = payload.Channels.Af7;
            _af8Data[_dataIndex] = payload.Channels.Af8;
            _tp10Data[_dataIndex] = payload.Channels.Tp10;
            
            _dataIndex = (_dataIndex + 1) % 512;
            
            // Update PSD Data
            if (payload.PsdFreqs.Count == 101 && payload.PsdPowers.Count == 101)
            {
                Dispatcher.Invoke(() => 
                {
                    string scaleMode = "Magnitude";
                    if (RbLogarithmic.IsChecked == true)
                    {
                        scaleMode = "Logarithmic";
                    }
                    else if (RbStandard.IsChecked == true)
                    {
                        scaleMode = "Standard";
                    }

                    for (int i = 0; i < 101; i++)
                    {
                        _psdFreqs[i] = payload.PsdFreqs[i];
                        double power = payload.PsdPowers[i];
                        
                        if (scaleMode == "Logarithmic")
                            _psdPowers[i] = 10 * Math.Log10(Math.Max(power, 1e-10));
                        else if (scaleMode == "Standard")
                            _psdPowers[i] = Math.Sqrt(power); // Amplitude
                        else
                            _psdPowers[i] = power; // Magnitude
                    }
                });
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded) return;

                IntegrityLight.Fill = payload.Metrics.SignalIntegrity switch
                {
                    SignalIntegrity.Green => Brushes.LimeGreen,
                    SignalIntegrity.Yellow => Brushes.Yellow,
                    SignalIntegrity.Red => Brushes.Red,
                    _ => Brushes.Gray
                };

                AlphaRatioText.Text = payload.Metrics.AlphaRatio.ToString("F2");
                AlphaRatioBar.Value = payload.Metrics.AlphaRatio;
            });
        }

        private void RbScale_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyStandardPsdLimits();
        }
        
        private void ApplyStandardPsdLimits()
        {
            double maxY = 100;
            double minY = 0;
            if (RbLogarithmic.IsChecked == true)
            {
                minY = -15;
                maxY = 30;
            }
            else if (RbStandard.IsChecked == true)
            {
                maxY = 20;
            }
            
            WpfPlotPSD_Monitor.Plot.Axes.SetLimits(0, 100, minY, maxY);
            WpfPlotPSD_NFB.Plot.Axes.SetLimits(0, 100, minY, maxY);
            
            WpfPlotPSD_Monitor.Refresh();
            WpfPlotPSD_NFB.Refresh();
        }

        private void WpfPlotPSD_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                e.Handled = true; // Prevent ScottPlot default AutoScale
                ApplyStandardPsdLimits();
            }
        }

        private void KillPython_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'python.exe'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var cmdLine = obj["CommandLine"]?.ToString();
                    if (cmdLine != null && cmdLine.Contains("server.py"))
                    {
                        int pid = Convert.ToInt32(obj["ProcessId"]);
                        Process.GetProcessById(pid).Kill();
                    }
                }
                MessageBox.Show("Python server instances terminated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to kill Python processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            await _telemetryClient.DisconnectAsync();
        }
    }
}