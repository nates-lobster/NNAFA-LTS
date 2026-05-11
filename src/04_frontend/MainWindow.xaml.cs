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

        // Historical Buffers (600 samples = 30s at 20Hz)
        private readonly double[] _historyDelta = new double[600];
        private readonly double[] _historyTheta = new double[600];
        private readonly double[] _historyAlpha = new double[600];
        private readonly double[] _historyBeta = new double[600];
        private readonly double[] _historyGamma = new double[600];
        private int _historyIndex = 0;

        // Debug Buffers (512 samples)
        private readonly double[] _debugRawData = new double[512];
        private readonly double[] _debugNotchedData = new double[512];
        private readonly double[] _debugFilteredData = new double[512];
        private int _debugDataIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            _telemetryClient = new TelemetryClient();
            _telemetryClient.OnTelemetryReceived += HandleTelemetry;
            
            SetupPlots();
        }

        private void SetupPlots()
        {
            var plots = new[] { WpfPlotAF7, WpfPlotAF8, WpfPlotTP9, WpfPlotTP10 };
            var titles = new[] { "AF7 (Left Forehead)", "AF8 (Right Forehead)", "TP9 (Left Ear)", "TP10 (Right Ear)" };
            var dataArrays = new[] { _af7Data, _af8Data, _tp9Data, _tp10Data };

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
            SetupHistoryPlot();
            SetupDebugPlots();
        }

        private void SetupHistoryPlot()
        {
            WpfPlotHistory.Plot.Title("Brainwave Power Trends (Last 30s)");
            WpfPlotHistory.Plot.XLabel("Time (30s Window)");
            WpfPlotHistory.Plot.YLabel("Power");

            var d = WpfPlotHistory.Plot.Add.Signal(_historyDelta); d.Label = "Delta"; d.Color = Color.FromHex("#666666");
            var t = WpfPlotHistory.Plot.Add.Signal(_historyTheta); t.Label = "Theta"; t.Color = Color.FromHex("#800080");
            var a = WpfPlotHistory.Plot.Add.Signal(_historyAlpha); a.Label = "Alpha"; a.Color = Color.FromHex("#008000");
            var b = WpfPlotHistory.Plot.Add.Signal(_historyBeta); b.Label = "Beta"; b.Color = Color.FromHex("#000080");
            var g = WpfPlotHistory.Plot.Add.Signal(_historyGamma); g.Label = "Gamma"; g.Color = Color.FromHex("#808000");

            WpfPlotHistory.Plot.ShowLegend(Alignment.UpperLeft);
            WpfPlotHistory.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotHistory.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotHistory.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            WpfPlotHistory.Plot.Axes.SetLimitsY(0, 100);
            WpfPlotHistory.Refresh();
        }

        private void SetupDebugPlots()
        {
            var debugPlots = new[] { WpfPlotDebugRaw, WpfPlotDebugNotched, WpfPlotDebugFiltered };
            var titles = new[] { "1. Raw Signal", "2. Post-Notch (60Hz)", "3. Post-Bandpass (1-100Hz)" };
            var arrays = new[] { _debugRawData, _debugNotchedData, _debugFilteredData };

            for (int i = 0; i < debugPlots.Length; i++)
            {
                debugPlots[i].Plot.Add.Signal(arrays[i]);
                debugPlots[i].Plot.Title(titles[i]);
                debugPlots[i].Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
                debugPlots[i].Plot.DataBackground.Color = Color.FromHex("#313244");
                debugPlots[i].Plot.Axes.Color(Color.FromHex("#A6ADC8"));
                debugPlots[i].Plot.Axes.SetLimitsY(-200, 200);
                debugPlots[i].Refresh();
            }

            SetupPsdPlot(WpfPlotDebugPSD);
            WpfPlotDebugPSD.Plot.Title("4. Final PSD (after FFT)");
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
            plot.Plot.Add.HorizontalSpan(40, 100, new Color(255, 69, 0, alpha)); // High Gamma - Orange-Red
            
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
                            WpfPlotHistory.Refresh();
                            WpfPlotDebugRaw.Refresh();
                            WpfPlotDebugNotched.Refresh();
                            WpfPlotDebugFiltered.Refresh();
                            WpfPlotDebugPSD.Refresh();
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
                    string scaleMode = "Standard";
                    if (RbCompensated.IsChecked == true)
                    {
                        scaleMode = "Compensated";
                    }

                    for (int i = 0; i < 101; i++)
                    {
                        double freq = payload.PsdFreqs[i];
                        _psdFreqs[i] = freq;
                        double power = payload.PsdPowers[i];
                        
                        if (scaleMode == "Compensated")
                            _psdPowers[i] = power * Math.Max(freq, 0.5); // Flattening: P * f
                        else
                            _psdPowers[i] = power; // Standard Magnitude
                    }
                });
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded) return;
                
                string statusText = payload.Metrics.SignalIntegrity.ToString().ToUpper();
                TxtStatus.Text = statusText;

                IntegrityLight.Fill = payload.Metrics.SignalIntegrity switch
                {
                    SignalIntegrity.Green => Brushes.LimeGreen,
                    SignalIntegrity.Yellow => Brushes.Yellow,
                    SignalIntegrity.Red => Brushes.Red,
                    _ => Brushes.Gray
                };
                
                TxtStatus.Foreground = IntegrityLight.Fill;

                TxtDelta.Text = payload.BandPower.Delta.ToString("F1");
                TxtTheta.Text = payload.BandPower.Theta.ToString("F1");
                TxtAlpha.Text = payload.BandPower.Alpha.ToString("F1");
                TxtBeta.Text = payload.BandPower.Beta.ToString("F1");
                TxtGamma.Text = payload.BandPower.Gamma.ToString("F1");

                AlphaRatioText.Text = payload.Metrics.AlphaRatio.ToString("F2");
                AlphaRatioBar.Value = payload.Metrics.AlphaRatio;

                // Update History Buffers (happens ~20 times/sec)
                _historyDelta[_historyIndex] = payload.BandPower.Delta;
                _historyTheta[_historyIndex] = payload.BandPower.Theta;
                _historyAlpha[_historyIndex] = payload.BandPower.Alpha;
                _historyBeta[_historyIndex] = payload.BandPower.Beta;
                _historyGamma[_historyIndex] = payload.BandPower.Gamma;
                _historyIndex = (_historyIndex + 1) % 600;

                // Update Debug Buffers based on selected RadioButton
                double raw = 0, notched = 0, filtered = 0;
                if (RbDebugTP9.IsChecked == true) { raw = payload.RawChannels.Tp9; notched = payload.NotchedChannels.Tp9; filtered = payload.Channels.Tp9; }
                else if (RbDebugAF7.IsChecked == true) { raw = payload.RawChannels.Af7; notched = payload.NotchedChannels.Af7; filtered = payload.Channels.Af7; }
                else if (RbDebugAF8.IsChecked == true) { raw = payload.RawChannels.Af8; notched = payload.NotchedChannels.Af8; filtered = payload.Channels.Af8; }
                else if (RbDebugTP10.IsChecked == true) { raw = payload.RawChannels.Tp10; notched = payload.NotchedChannels.Tp10; filtered = payload.Channels.Tp10; }
                else if (RbDebugAvg.IsChecked == true)
                {
                    raw = (payload.RawChannels.Tp9 + payload.RawChannels.Af7 + payload.RawChannels.Af8 + payload.RawChannels.Tp10) / 4.0;
                    notched = (payload.NotchedChannels.Tp9 + payload.NotchedChannels.Af7 + payload.NotchedChannels.Af8 + payload.NotchedChannels.Tp10) / 4.0;
                    filtered = (payload.Channels.Tp9 + payload.Channels.Af7 + payload.Channels.Af8 + payload.Channels.Tp10) / 4.0;
                }

                _debugRawData[_debugDataIndex] = raw;
                _debugNotchedData[_debugDataIndex] = notched;
                _debugFilteredData[_debugDataIndex] = filtered;
                _debugDataIndex = (_debugDataIndex + 1) % 512;
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
            
            if (RbCompensated.IsChecked == true)
            {
                maxY = 250; // Higher limits needed for P * f
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