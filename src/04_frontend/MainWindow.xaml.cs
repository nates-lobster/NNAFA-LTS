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
using Google.Protobuf;
using System.Windows.Controls;
using System.Windows.Input;

namespace Frontend
{
    public partial class MainWindow : Window
    {
        // LSL Message Queue
        private System.Collections.Generic.Queue<string> _lslMessageQueue = new();
        private bool _isProcessingLslMessages = false;

        private async void QueueLslMessage(string message)
        {
            _lslMessageQueue.Enqueue(message);
            if (!_isProcessingLslMessages)
            {
                _isProcessingLslMessages = true;
                while (_lslMessageQueue.Count > 0)
                {
                    string msg = _lslMessageQueue.Dequeue();
                    if (TxtLslStatus != null) TxtLslStatus.Text = msg;
                    await Task.Delay(5000);
                }
                if (_lslProcess == null && TxtLslStatus != null) TxtLslStatus.Text = "Status: Idle";
                _isProcessingLslMessages = false;
            }
        }

        private readonly TelemetryClient _telemetryClient;
        private readonly CancellationTokenSource _cts = new();
        private Process? _lslProcess;
        
        // Data buffers for ScottPlot
        private readonly double[] _tp9Data = new double[512];
        private readonly double[] _af7Data = new double[512];
        private readonly double[] _af8Data = new double[512];
        private readonly double[] _tp10Data = new double[512];
        private int _dataIndex = 0;
        
        // Data buffers for PSD (401 points from 0 to 100 Hz in 0.25 Hz steps)
        private readonly double[] _psdFreqs = new double[401];
        private readonly double[] _psdPowers = new double[401];
        private readonly double[] _psdNfbFreqs = new double[401];
        private readonly double[] _psdNfbPowers = new double[401];
        private readonly double[] _psdMemoryTaskFreqs = new double[401];
        private readonly double[] _psdMemoryTaskPowers = new double[401];

        // Spectrogram Data: 161 frequency rows (0-40 Hz in 0.25 Hz steps) x 200 time history columns
        private readonly double[,] _spectrogramData = new double[161, 200];
        private readonly double[] _spectrogramFreqs = new double[161];
        private readonly double[] _spectrogramLivePowers = new double[161];
        private readonly double[] _spectrogramAvgPowers = new double[161];
        private ScottPlot.Plottables.Scatter? _spectrogramLiveScatter;
        private ScottPlot.Plottables.Scatter? _spectrogramAvgScatter;


        // Working Memory Challenge State
        private double _currentDelta = 0;
        private double _currentTheta = 0;
        private double _currentAlpha = 0;
        private double _currentBeta = 0;
        private double _currentGamma = 0;
        // Working Memory Challenge State
        private int _memoryLevel = 3;
        private int _memoryScore = 0;
        private int _memoryStrikes = 0;
        private string _sequenceString = "";
        private bool _isDisplayingSequence = false;
        private bool _isGameRunning = false;
        private bool _isLevelLocked = false;
        private int _lockedTrialsCount = 0;
        private int _totalCalibrationTrials = 0;
        private readonly Random _random = new();

        // Spatial Sequence Challenge (Chimpanzee Test) State
        private int _selectedTaskType = 0; // 0 = Digit Span, 1 = Spatial Sequence
        private int _spatialNextExpectedValue = 1;
        private bool _isSpatialClickActive = false;
        private System.Windows.Threading.DispatcherTimer? _spatialMaskTimer;
        private readonly System.Collections.Generic.List<Border> _spatialSquares = new();
        private readonly System.Collections.Generic.List<int> _spatialSquareValues = new();
        private bool _isControlTrial = true;

        // Brainwave Power Trend Plot State (Memory Task Tab)
        private readonly System.Collections.Generic.List<double> _trendDelta = new();
        private readonly System.Collections.Generic.List<double> _trendTheta = new();
        private readonly System.Collections.Generic.List<double> _trendAlpha = new();
        private readonly System.Collections.Generic.List<double> _trendBeta = new();
        private readonly System.Collections.Generic.List<double> _trendGamma = new();

        // Event Timeline tracking
        private DateTime _challengeStartTime = DateTime.MinValue;
        private readonly System.Collections.Generic.List<CognitiveEvent> _cognitiveEvents = new();
        private readonly System.Collections.Generic.List<CognitiveInterval> _cognitiveIntervals = new();

        // Post-Test Analysis state
        private System.Collections.Generic.List<TrialData> _postTestTrials = new();
        private System.Collections.Generic.List<bool> _postTestTrialVisibility = new();

        public class CognitiveEvent
        {
            public double TimeInSeconds { get; set; }
            public string Text { get; set; }
            public bool IsUserEvent { get; set; }
            public double DeltaPower { get; set; }
            public double ThetaPower { get; set; }
            public double AlphaPower { get; set; }
            public double BetaPower { get; set; }
            public double GammaPower { get; set; }
        }

        public class CognitiveInterval
        {
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public bool IsEncoding { get; set; }
            public int Level { get; set; }
            public bool IsControl { get; set; }
        }

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
        private readonly double[] _debugFirData = new double[512];
        private readonly double[] _debugFilteredData = new double[512];
        private readonly double[] _debugPsdPowers = new double[401];
        private int _debugDataIndex = 0;

        // Overlay plot handles
        private ScottPlot.Plottables.Signal? _sigRaw;
        private ScottPlot.Plottables.Signal? _sigNotch;
        private ScottPlot.Plottables.Signal? _sigFir;
        private ScottPlot.Plottables.Signal? _sigIir;

        // Neurofeedback Audio
        private readonly MediaPlayer _mediaPlayer = new();
        private double _targetVolume = 0;
        private double _currentVolume = 0;

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
            SetupMemoryTaskPsdPlot();
            SetupMemoryTaskGammaTrendPlot();
            SetupSpectrogramPlot();
            SetupHistoryPlot();
            SetupDebugPlots();
            SetupOverlayPlot();
            SetupPostTestPlot();
        }

        private void SetupOverlayPlot()
        {
            WpfPlotOverlay.Plot.Title("Debug Pipeline Overlay");
            WpfPlotOverlay.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotOverlay.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotOverlay.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            WpfPlotOverlay.Plot.Axes.SetLimitsY(-200, 200);

            _sigRaw = WpfPlotOverlay.Plot.Add.Signal(_debugRawData); _sigRaw.LegendText = "Raw"; _sigRaw.Color = Color.FromHex("#666666");
            _sigNotch = WpfPlotOverlay.Plot.Add.Signal(_debugNotchedData); _sigNotch.LegendText = "Notch"; _sigNotch.Color = Color.FromHex("#F38BA8");
            _sigFir = WpfPlotOverlay.Plot.Add.Signal(_debugFirData); _sigFir.LegendText = "FIR"; _sigFir.Color = Color.FromHex("#FAB387");
            _sigIir = WpfPlotOverlay.Plot.Add.Signal(_debugFilteredData); _sigIir.LegendText = "IIR"; _sigIir.Color = Color.FromHex("#A6E3A1");

            WpfPlotOverlay.Plot.ShowLegend(Alignment.UpperRight);
            WpfPlotOverlay.Refresh();
        }

        private void SetupHistoryPlot()
        {
            WpfPlotHistory.Plot.Title("Brainwave Power Trends (Last 30s)");
            WpfPlotHistory.Plot.XLabel("Time (30s Window)");
            WpfPlotHistory.Plot.YLabel("Power");

            var d = WpfPlotHistory.Plot.Add.Signal(_historyDelta); d.LegendText = "Delta"; d.Color = Color.FromHex("#666666");
            var t = WpfPlotHistory.Plot.Add.Signal(_historyTheta); t.LegendText = "Theta"; t.Color = Color.FromHex("#800080");
            var a = WpfPlotHistory.Plot.Add.Signal(_historyAlpha); a.LegendText = "Alpha"; a.Color = Color.FromHex("#008000");
            var b = WpfPlotHistory.Plot.Add.Signal(_historyBeta); b.LegendText = "Beta"; b.Color = Color.FromHex("#000080");
            var g = WpfPlotHistory.Plot.Add.Signal(_historyGamma); g.LegendText = "Gamma"; g.Color = Color.FromHex("#808000");

            WpfPlotHistory.Plot.ShowLegend(Alignment.UpperLeft);
            WpfPlotHistory.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotHistory.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotHistory.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            WpfPlotHistory.Plot.Axes.SetLimitsY(0, 100);
            WpfPlotHistory.Refresh();
        }

        private void SetupDebugPlots()
        {
            var debugPlots = new[] { WpfPlotDebugRaw, WpfPlotDebugNotched, WpfPlotDebugFir, WpfPlotDebugFiltered };
            var titles = new[] { "1. Raw Signal", "2. Post-Notch (60Hz)", "3. FIR Denoised", "4. Post-Bandpass (IIR)" };
            var arrays = new[] { _debugRawData, _debugNotchedData, _debugFirData, _debugFilteredData };

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
            // Overwrite PSD data source for debug to use independent buffer
            WpfPlotDebugPSD.Plot.Clear();
            var scatter = WpfPlotDebugPSD.Plot.Add.Scatter(_psdFreqs, _debugPsdPowers);
            scatter.Color = Color.FromHex("#89B4FA");
            scatter.LineWidth = 2;
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
            
            double[] freqArray = _psdFreqs;
            double[] powerArray = _psdPowers;
            if (plot == WpfPlotPSD_NFB)
            {
                freqArray = _psdNfbFreqs;
                powerArray = _psdNfbPowers;
            }
            
            var scatter = plot.Plot.Add.Scatter(freqArray, powerArray);
            scatter.Color = Color.FromHex("#89B4FA");
            scatter.LineWidth = 2;
            
            plot.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            plot.Plot.DataBackground.Color = Color.FromHex("#313244");
            plot.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            plot.Refresh();
        }

        private void SetupMemoryTaskPsdPlot()
        {
            var plot = WpfPlotPSD_MemoryTask;
            plot.Plot.Title("Memory Task Frontal PSD (AF7/AF8)");
            plot.Plot.XLabel("Frequency (Hz)");
            plot.Plot.YLabel("Power");
            plot.Plot.Axes.SetLimits(0, 100, 0, 100);

            byte alpha = 60;
            plot.Plot.Add.HorizontalSpan(1, 4, new Color(100, 100, 100, alpha)); // Delta
            plot.Plot.Add.HorizontalSpan(4, 8, new Color(128, 0, 128, alpha));   // Theta
            plot.Plot.Add.HorizontalSpan(8, 12, new Color(0, 128, 0, alpha));    // Alpha
            plot.Plot.Add.HorizontalSpan(12, 30, new Color(0, 0, 128, alpha));   // Beta
            plot.Plot.Add.HorizontalSpan(30, 40, new Color(128, 128, 0, alpha)); // Gamma
            plot.Plot.Add.HorizontalSpan(40, 100, new Color(255, 69, 0, alpha)); // High Gamma

            var scatter = plot.Plot.Add.Scatter(_psdMemoryTaskFreqs, _psdMemoryTaskPowers);
            scatter.Color = Color.FromHex("#A6E3A1"); // Green scatter for task waves
            scatter.LineWidth = 2;

            plot.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            plot.Plot.DataBackground.Color = Color.FromHex("#313244");
            plot.Plot.Axes.Color(Color.FromHex("#A6ADC8"));
            plot.Refresh();
        }

        private void SetupMemoryTaskGammaTrendPlot()
        {
            UpdateTrendPlot();
        }

        private void CbTrend_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTrendPlot();
        }

        private void WpfPlotGammaTrend_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsLoaded || WpfPlotGammaTrend_MemoryTask == null || _cognitiveEvents.Count == 0) return;

            var pos = e.GetPosition(WpfPlotGammaTrend_MemoryTask);
            var coord = WpfPlotGammaTrend_MemoryTask.Plot.GetCoordinates(new Pixel((float)pos.X, (float)pos.Y));

            // Find closest event along the X-axis (time) within a comfortable tolerance of 0.8 seconds
            CognitiveEvent? closest = null;
            double minDistance = 0.8; 
            
            lock (_cognitiveEvents)
            {
                foreach (var ev in _cognitiveEvents)
                {
                    double dist = Math.Abs(ev.TimeInSeconds - coord.X);
                    if (dist < minDistance)
                    {
                        closest = ev;
                        minDistance = dist;
                    }
                }
            }

            if (closest != null)
            {
                string type = closest.IsUserEvent ? "User Click" : "System Transition";
                string colorHex = closest.IsUserEvent ? "#F38BA8" : "#A6ADC8";
                
                TxtEventDetails.Text = $"[{type}] at {closest.TimeInSeconds:F2}s ➔ {closest.Text}";
                TxtEventDetails.Foreground = (Brush)new BrushConverter().ConvertFrom(colorHex)!;
            }
            else
            {
                TxtEventDetails.Text = "Hover over dotted vertical event lines to inspect user clicks and game system transitions.";
                TxtEventDetails.Foreground = (Brush)new BrushConverter().ConvertFrom("#A6ADC8")!;
            }
        }

        private void UpdateTrendPlot()
        {
            if (!IsLoaded || WpfPlotGammaTrend_MemoryTask == null) return;

            var plot = WpfPlotGammaTrend_MemoryTask;
            plot.Plot.Clear();

            // Re-apply Styling for Dark Mode
            plot.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            plot.Plot.DataBackground.Color = Color.FromHex("#313244");
            plot.Plot.Axes.Color(Color.FromHex("#A6ADC8"));

            plot.Plot.Title("Cognitive Challenge Overlaid Power Trend");
            plot.Plot.XLabel("Time (Seconds)");
            plot.Plot.YLabel("Power Magnitude");

            bool hasData = false;
            double maxTime = 5.0;

            lock (_trendGamma)
            {
                int len = _trendGamma.Count;
                if (len > 0)
                {
                    hasData = true;
                    double[] xValues = System.Linq.Enumerable.Range(0, len)
                                                         .Select(i => (double)i / 20.0)
                                                         .ToArray();
                    maxTime = xValues[^1];

                    // Draw Delta if checked
                    if (CbTrendDelta != null && CbTrendDelta.IsChecked == true)
                    {
                        var sc = plot.Plot.Add.Scatter(xValues, _trendDelta.ToArray());
                        sc.Color = Color.FromHex("#89B4FA");
                        sc.LineWidth = 2;
                        sc.LegendText = "Delta (δ)";
                    }
                    // Draw Theta if checked
                    if (CbTrendTheta != null && CbTrendTheta.IsChecked == true)
                    {
                        var sc = plot.Plot.Add.Scatter(xValues, _trendTheta.ToArray());
                        sc.Color = Color.FromHex("#CBA6F7");
                        sc.LineWidth = 2;
                        sc.LegendText = "Theta (θ)";
                    }
                    // Draw Alpha if checked
                    if (CbTrendAlpha != null && CbTrendAlpha.IsChecked == true)
                    {
                        var sc = plot.Plot.Add.Scatter(xValues, _trendAlpha.ToArray());
                        sc.Color = Color.FromHex("#A6E3A1");
                        sc.LineWidth = 2;
                        sc.LegendText = "Alpha (α)";
                    }
                    // Draw Beta if checked
                    if (CbTrendBeta != null && CbTrendBeta.IsChecked == true)
                    {
                        var sc = plot.Plot.Add.Scatter(xValues, _trendBeta.ToArray());
                        sc.Color = Color.FromHex("#74C7EC");
                        sc.LineWidth = 2;
                        sc.LegendText = "Beta (β)";
                    }
                    // Draw Gamma if checked
                    if (CbTrendGamma != null && CbTrendGamma.IsChecked == true)
                    {
                        var sc = plot.Plot.Add.Scatter(xValues, _trendGamma.ToArray());
                        sc.Color = Color.FromHex("#F9E2AF");
                        sc.LineWidth = 2;
                        sc.LegendText = "Gamma (γ)";
                    }
                }
            }

            // Draw Cognitive Intervals (Encoding = Green area, Recall = Red area)
            lock (_cognitiveIntervals)
            {
                foreach (var interval in _cognitiveIntervals)
                {
                    double start = interval.StartTime;
                    double end = interval.EndTime < 0 ? maxTime : interval.EndTime;

                    if (start < end)
                    {
                        var color = interval.IsEncoding 
                            ? new Color(166, 227, 161, 30)  // Soft green (opacity 30/255)
                            : new Color(243, 139, 168, 30); // Soft red (opacity 30/255);
                        plot.Plot.Add.HorizontalSpan(start, end, color);
                    }
                }
            }

            // Draw Cognitive Event Click markers (dots on the trend lines, no vertical lines)
            lock (_cognitiveEvents)
            {
                foreach (var ev in _cognitiveEvents)
                {
                    if (ev.IsUserEvent)
                    {
                        double clickTime = ev.TimeInSeconds;
                        int idx = (int)(clickTime * 20.0);

                        lock (_trendGamma)
                        {
                            int count = _trendGamma.Count;
                            if (count > 0)
                            {
                                int clampIdx = Math.Clamp(idx, 0, count - 1);

                                if (CbTrendDelta != null && CbTrendDelta.IsChecked == true)
                                {
                                    var marker = plot.Plot.Add.Marker(clickTime, _trendDelta[clampIdx]);
                                    marker.Color = Color.FromHex("#89B4FA");
                                    marker.Shape = MarkerShape.FilledCircle;
                                    marker.Size = 10;
                                }
                                if (CbTrendTheta != null && CbTrendTheta.IsChecked == true)
                                {
                                    var marker = plot.Plot.Add.Marker(clickTime, _trendTheta[clampIdx]);
                                    marker.Color = Color.FromHex("#CBA6F7");
                                    marker.Shape = MarkerShape.FilledCircle;
                                    marker.Size = 10;
                                }
                                if (CbTrendAlpha != null && CbTrendAlpha.IsChecked == true)
                                {
                                    var marker = plot.Plot.Add.Marker(clickTime, _trendAlpha[clampIdx]);
                                    marker.Color = Color.FromHex("#A6E3A1");
                                    marker.Shape = MarkerShape.FilledCircle;
                                    marker.Size = 10;
                                }
                                if (CbTrendBeta != null && CbTrendBeta.IsChecked == true)
                                {
                                    var marker = plot.Plot.Add.Marker(clickTime, _trendBeta[clampIdx]);
                                    marker.Color = Color.FromHex("#74C7EC");
                                    marker.Shape = MarkerShape.FilledCircle;
                                    marker.Size = 10;
                                }
                                if (CbTrendGamma != null && CbTrendGamma.IsChecked == true)
                                {
                                    var marker = plot.Plot.Add.Marker(clickTime, _trendGamma[clampIdx]);
                                    marker.Color = Color.FromHex("#F9E2AF");
                                    marker.Shape = MarkerShape.FilledCircle;
                                    marker.Size = 10;
                                }
                            }
                        }
                    }
                }
            }

            // Show Legend with premium styling
            plot.Plot.ShowLegend();
            plot.Plot.Legend.IsVisible = true;
            plot.Plot.Legend.BackgroundColor = Color.FromHex("#1E1E2E");
            plot.Plot.Legend.FontColor = Color.FromHex("#CDD6F4");
            plot.Plot.Legend.OutlineColor = Color.FromHex("#45475A");

            // Compress time axis
            double maxX = maxTime > 5 ? maxTime : 5;
            plot.Plot.Axes.SetLimits(0, maxX, 0, 50);

            plot.Refresh();
        }

        private void RecordCognitiveEvent(string description, bool isUserEvent)
        {
            if (_challengeStartTime == DateTime.MinValue) return;
            
            double relativeSeconds = (DateTime.Now - _challengeStartTime).TotalSeconds;
            lock (_cognitiveEvents)
            {
                _cognitiveEvents.Add(new CognitiveEvent
                {
                    TimeInSeconds = relativeSeconds,
                    Text = description,
                    IsUserEvent = isUserEvent,
                    DeltaPower = _currentDelta,
                    ThetaPower = _currentTheta,
                    AlphaPower = _currentAlpha,
                    BetaPower = _currentBeta,
                    GammaPower = _currentGamma
                });
            }
            UpdateTrendPlot();
        }

        private void StartCognitiveInterval(bool isEncoding, int level, bool isControl = false)
        {
            if (_challengeStartTime == DateTime.MinValue) return;
            double nowSec = (DateTime.Now - _challengeStartTime).TotalSeconds;
            
            lock (_cognitiveIntervals)
            {
                // Finalize the last interval if it exists and hasn't been finalized
                if (_cognitiveIntervals.Count > 0)
                {
                    var last = _cognitiveIntervals[^1];
                    if (last.EndTime < 0)
                    {
                        last.EndTime = nowSec;
                    }
                }
                _cognitiveIntervals.Add(new CognitiveInterval
                {
                    StartTime = nowSec,
                    EndTime = -1, // Open-ended
                    IsEncoding = isEncoding,
                    Level = level,
                    IsControl = isControl
                });
            }
        }

        private void FinalizeActiveInterval()
        {
            if (_challengeStartTime == DateTime.MinValue) return;
            double nowSec = (DateTime.Now - _challengeStartTime).TotalSeconds;
            lock (_cognitiveIntervals)
            {
                if (_cognitiveIntervals.Count > 0)
                {
                    var last = _cognitiveIntervals[^1];
                    if (last.EndTime < 0)
                    {
                        last.EndTime = nowSec;
                    }
                }
            }
        }

        private void ExportChallengeResults(string endMessage)
        {
            try
            {
                if (_challengeStartTime == DateTime.MinValue) return;

                double totalDuration = (DateTime.Now - _challengeStartTime).TotalSeconds;
                
                // Build intervals data with average brainwaves
                var intervalsList = new System.Collections.Generic.List<object>();
                lock (_cognitiveIntervals)
                {
                    foreach (var interval in _cognitiveIntervals)
                    {
                        double start = interval.StartTime;
                        double end = interval.EndTime < 0 ? totalDuration : interval.EndTime;
                        double duration = end - start;

                        // Compute average powers during this interval
                        double sumDelta = 0, sumTheta = 0, sumAlpha = 0, sumBeta = 0, sumGamma = 0;
                        int count = 0;
                        
                        lock (_trendGamma)
                        {
                            int startIndex = (int)(start * 20.0);
                            int endIndex = (int)(end * 20.0);
                            
                            startIndex = Math.Clamp(startIndex, 0, _trendGamma.Count);
                            endIndex = Math.Clamp(endIndex, 0, _trendGamma.Count);

                            for (int i = startIndex; i < endIndex; i++)
                            {
                                sumDelta += _trendDelta[i];
                                sumTheta += _trendTheta[i];
                                sumAlpha += _trendAlpha[i];
                                sumBeta += _trendBeta[i];
                                sumGamma += _trendGamma[i];
                                count++;
                            }
                        }

                        intervalsList.Add(new
                        {
                            level = interval.Level,
                            phase = interval.IsEncoding ? "Encoding" : "Recall",
                            is_control = interval.IsControl,
                            start_seconds = Math.Round(start, 2),
                            end_seconds = Math.Round(end, 2),
                            duration_seconds = Math.Round(duration, 2),
                            metrics = count > 0 ? new {
                                avg_delta = Math.Round(sumDelta / count, 2),
                                avg_theta = Math.Round(sumTheta / count, 2),
                                avg_alpha = Math.Round(sumAlpha / count, 2),
                                avg_beta = Math.Round(sumBeta / count, 2),
                                avg_gamma = Math.Round(sumGamma / count, 2)
                            } : null
                        });
                    }
                }

                var eventsList = new System.Collections.Generic.List<object>();
                lock (_cognitiveEvents)
                {
                    foreach (var ev in _cognitiveEvents)
                    {
                        eventsList.Add(new
                        {
                            timestamp_seconds = Math.Round(ev.TimeInSeconds, 2),
                            event_description = ev.Text,
                            type = ev.IsUserEvent ? "User" : "System",
                            delta_power = Math.Round(ev.DeltaPower, 2),
                            theta_power = Math.Round(ev.ThetaPower, 2),
                            alpha_power = Math.Round(ev.AlphaPower, 2),
                            beta_power = Math.Round(ev.BetaPower, 2),
                            gamma_power = Math.Round(ev.GammaPower, 2)
                        });
                    }
                }

                var resultData = new
                {
                    test_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    task_type = _selectedTaskType == 0 ? "Digit Recall" : "Spatial Sequence",
                    final_score = _memoryScore,
                    max_level = _memoryLevel,
                    end_reason = endMessage,
                    total_duration_seconds = Math.Round(totalDuration, 2),
                    intervals = intervalsList,
                    events = eventsList,
                    trials = ParseTrials()
                };

                // Serialize to JSON
                string json = System.Text.Json.JsonSerializer.Serialize(resultData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                string filename = $"post_test_data_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string rootDir = FindProjectRoot(AppDomain.CurrentDomain.BaseDirectory) ?? AppDomain.CurrentDomain.BaseDirectory;
                
                string fullPath = System.IO.Path.Combine(rootDir, filename);
                string latestPath = System.IO.Path.Combine(rootDir, "post_test_data_latest.json");

                System.IO.File.WriteAllText(fullPath, json);
                System.IO.File.WriteAllText(latestPath, json);

                // Also save in scratch of conversation dir if accessible
                string scratchDir = @"C:\Users\Nate\.gemini\antigravity\brain\6f955eca-6cd3-468b-8607-6eef63ba3bf7\scratch";
                if (System.IO.Directory.Exists(scratchDir))
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(scratchDir, filename), json);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(scratchDir, "cognitive_results_latest.json"), json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            }
        }

        private void SetupPostTestPlot()
        {
            WpfPlotPostTestAnalysis.Plot.Title("Post-Test Focus Analysis");
            WpfPlotPostTestAnalysis.Plot.XLabel("Discrete Phase / Click Number");
            WpfPlotPostTestAnalysis.Plot.YLabel("Average Power (μV²)");

            WpfPlotPostTestAnalysis.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotPostTestAnalysis.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotPostTestAnalysis.Plot.Axes.Color(Color.FromHex("#A6ADC8"));

            WpfPlotPostTestAnalysis.Refresh();
        }

        public class TrialClick
        {
            public double TimeInSeconds { get; set; }
            public bool IsWrong { get; set; }
        }

        public class TrialData
        {
            public int Level { get; set; }
            public bool IsControl { get; set; }
            public bool IsSetDiff { get; set; }
            public double EncodingStart { get; set; }
            public double EncodingEnd { get; set; }
            public double RecallStart { get; set; }
            public System.Collections.Generic.List<double> PresentationTimes { get; set; } = new();
            public System.Collections.Generic.List<TrialClick> Clicks { get; set; } = new();
            public double RoundEnd { get; set; }
        }

        private System.Collections.Generic.List<TrialData> ParseTrials()
        {
            var trials = new System.Collections.Generic.List<TrialData>();
            TrialData? currentTrial = null;

            lock (_cognitiveEvents)
            {
                foreach (var ev in _cognitiveEvents)
                {
                    if (ev.Text.StartsWith("Started presentation of set ") || 
                        ev.Text.StartsWith("Started presentation of spatial set ") ||
                        ev.Text.StartsWith("Started presentation of spatial control set "))
                    {
                        if (currentTrial != null)
                        {
                            if (currentTrial.RoundEnd <= 0) currentTrial.RoundEnd = ev.TimeInSeconds;
                            trials.Add(currentTrial);
                        }

                        int level = 3;
                        string[] parts = ev.Text.Split(' ');
                        if (parts.Length > 0 && int.TryParse(parts[^1], out int lvl))
                        {
                            level = lvl;
                        }

                        currentTrial = new TrialData
                        {
                            Level = level,
                            IsControl = ev.Text.Contains("spatial control"),
                            IsSetDiff = ev.Text.Contains("set_diff: True"),
                            EncodingStart = ev.TimeInSeconds
                        };
                    }
                    else if (ev.Text.StartsWith("Started recall of set ") || 
                             ev.Text.StartsWith("Started recall of spatial set ") ||
                             ev.Text.StartsWith("Started recall of spatial control set "))
                    {
                        if (currentTrial != null)
                        {
                            currentTrial.EncodingEnd = ev.TimeInSeconds;
                            currentTrial.RecallStart = ev.TimeInSeconds;
                        }
                    }
                    else if (ev.Text.StartsWith("Presented digit:"))
                    {
                        if (currentTrial != null)
                        {
                            currentTrial.PresentationTimes.Add(ev.TimeInSeconds);
                        }
                    }
                    else if (ev.IsUserEvent)
                    {
                        if (currentTrial != null)
                        {
                            bool isWrong = ev.Text.Contains("(Incorrect)");
                            currentTrial.Clicks.Add(new TrialClick { TimeInSeconds = ev.TimeInSeconds, IsWrong = isWrong });
                        }
                    }
                    else if (ev.Text.Contains("recall correct") || ev.Text.Contains("recall incorrect"))
                    {
                        if (currentTrial != null)
                        {
                            currentTrial.RoundEnd = ev.TimeInSeconds;
                            trials.Add(currentTrial);
                            currentTrial = null;
                        }
                    }
                }

                if (currentTrial != null)
                {
                    if (currentTrial.RoundEnd <= 0) currentTrial.RoundEnd = (DateTime.Now - _challengeStartTime).TotalSeconds;
                    trials.Add(currentTrial);
                }
            }

            return trials;
        }

        private double GetAverageBandPower(string bandName, double startSec, double endSec)
        {
            System.Collections.Generic.List<double> sourceList;
            lock (_trendGamma)
            {
                sourceList = bandName.ToLower() switch
                {
                    "delta" => _trendDelta.ToList(),
                    "theta" => _trendTheta.ToList(),
                    "alpha" => _trendAlpha.ToList(),
                    "beta" => _trendBeta.ToList(),
                    _ => _trendGamma.ToList()
                };
            }

            int count = sourceList.Count;
            if (count == 0) return 0.0;

            int startIndex = (int)(startSec * 20.0);
            int endIndex = (int)(endSec * 20.0);

            startIndex = Math.Clamp(startIndex, 0, count - 1);
            endIndex = Math.Clamp(endIndex, 0, count - 1);

            if (startIndex >= endIndex)
            {
                return sourceList[startIndex];
            }

            double sum = 0.0;
            int n = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                sum += sourceList[i];
                n++;
            }
            return n > 0 ? sum / n : 0.0;
        }

        private void GetBandTimeSeries(string band, double startSec, double endSec, out double[] xs, out double[] ys, out double average)
        {
            System.Collections.Generic.List<double> sourceList;
            lock (_trendGamma)
            {
                sourceList = band.ToLower() switch
                {
                    "delta" => _trendDelta.ToList(),
                    "theta" => _trendTheta.ToList(),
                    "alpha" => _trendAlpha.ToList(),
                    "beta" => _trendBeta.ToList(),
                    _ => _trendGamma.ToList()
                };
            }

            int count = sourceList.Count;
            if (count == 0)
            {
                xs = Array.Empty<double>();
                ys = Array.Empty<double>();
                average = 0.0;
                return;
            }

            int startIndex = (int)(startSec * 20.0);
            int endIndex = (int)(endSec * 20.0);

            startIndex = Math.Clamp(startIndex, 0, count - 1);
            endIndex = Math.Clamp(endIndex, 0, count - 1);

            var xList = new System.Collections.Generic.List<double>();
            var yList = new System.Collections.Generic.List<double>();
            double sum = 0.0;
            int n = 0;

            for (int i = startIndex; i <= endIndex; i++)
            {
                double val = sourceList[i];
                double relativeTime = ((double)i / 20.0) - startSec; // relative to start of encoding (ranges from 0 to duration)
                xList.Add(relativeTime);
                yList.Add(val);
                sum += val;
                n++;
            }

            if (n == 0)
            {
                xList.Add(0.0);
                yList.Add(sourceList[startIndex]);
                sum = sourceList[startIndex];
                n = 1;
            }

            xs = xList.ToArray();
            ys = yList.ToArray();
            average = sum / n;
        }

        private void ParseAndPopulatePostTestState()
        {
            _postTestTrials = ParseTrials();
            _postTestTrialVisibility = System.Linq.Enumerable.Repeat(true, _postTestTrials.Count).ToList();

            if (PanelPostTestTrialCheckboxes == null) return;

            PanelPostTestTrialCheckboxes.Children.Clear();
            for (int i = 0; i < _postTestTrials.Count; i++)
            {
                var trial = _postTestTrials[i];
                string suffix = _selectedTaskType == 1 ? (trial.IsControl ? " Control" : " Test") : "";
                var cb = new CheckBox
                {
                    Content = $"Trial {i + 1}{suffix} (Lvl {trial.Level})",
                    Foreground = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!,
                    Margin = new Thickness(0, 0, 15, 0),
                    IsChecked = true,
                    Tag = i,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                cb.Checked += (s, e) =>
                {
                    if (s is CheckBox box && box.Tag is int idx)
                    {
                        if (idx >= 0 && idx < _postTestTrialVisibility.Count)
                        {
                            _postTestTrialVisibility[idx] = true;
                            RenderPostTestPlot();
                        }
                    }
                };

                cb.Unchecked += (s, e) =>
                {
                    if (s is CheckBox box && box.Tag is int idx)
                    {
                        if (idx >= 0 && idx < _postTestTrialVisibility.Count)
                        {
                            _postTestTrialVisibility[idx] = false;
                            RenderPostTestPlot();
                        }
                    }
                };

                PanelPostTestTrialCheckboxes.Children.Add(cb);
            }

            RenderPostTestPlot();
        }

        private void CbPostBand_Changed(object sender, RoutedEventArgs e)
        {
            RenderPostTestPlot();
        }

        private void RenderPostTestPlot()
        {
            if (WpfPlotPostTestAnalysis == null) return;

            WpfPlotPostTestAnalysis.Plot.Clear();

            // Style Dark Mode
            WpfPlotPostTestAnalysis.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotPostTestAnalysis.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotPostTestAnalysis.Plot.Axes.Color(Color.FromHex("#A6ADC8"));

            WpfPlotPostTestAnalysis.Plot.Title("Post-Test Brainwave Analysis: Encoding Time + Click Averages");
            WpfPlotPostTestAnalysis.Plot.XLabel("Encoding Time (seconds from Start) / Click Steps");
            WpfPlotPostTestAnalysis.Plot.YLabel("Average Power (μV²)");

            var activeBands = new System.Collections.Generic.List<(string Name, string Label, string HexColor)>();
            // Dummy legend entries for control and test
            var ctrlLegend = WpfPlotPostTestAnalysis.Plot.Add.Scatter(new double[]{0}, new double[]{0});
            ctrlLegend.Color = Color.FromHex("#94E2D5");
            ctrlLegend.LegendText = "Control";
            ctrlLegend.IsVisible = false;
            var testLegend = WpfPlotPostTestAnalysis.Plot.Add.Scatter(new double[]{0}, new double[]{0});
            testLegend.Color = Color.FromHex("#F5A623");
            testLegend.LegendText = "Test";
            testLegend.IsVisible = false;
            if (CbPostDelta?.IsChecked == true) activeBands.Add(("delta", "Delta (δ)", "#89B4FA"));
            if (CbPostTheta?.IsChecked == true) activeBands.Add(("theta", "Theta (θ)", "#CBA6F7"));
            if (CbPostAlpha?.IsChecked == true) activeBands.Add(("alpha", "Alpha (α)", "#A6E3A1"));
            if (CbPostBeta?.IsChecked == true) activeBands.Add(("beta", "Beta (β)", "#74C7EC"));
            if (CbPostGamma?.IsChecked == true) activeBands.Add(("gamma", "Gamma (γ)", "#F9E2AF"));

            if (_postTestTrials.Count == 0 || activeBands.Count == 0)
            {
                WpfPlotPostTestAnalysis.Refresh();
                return;
            }

            bool singleBandMode = activeBands.Count == 1;
            double maxVal = 10.0;
            int maxClicksObserved = 0;
            double maxEncodingDuration = 1.0;

            // Pre-calculate maximums
            for (int i = 0; i < _postTestTrials.Count; i++)
            {
                if (i >= _postTestTrialVisibility.Count || !_postTestTrialVisibility[i])
                    continue;
                var trial = _postTestTrials[i];
                double encDuration = trial.EncodingEnd - trial.EncodingStart;
                if (encDuration > maxEncodingDuration) maxEncodingDuration = encDuration;
                if (trial.Clicks.Count > maxClicksObserved) maxClicksObserved = trial.Clicks.Count;
            }

            int maxEncSec = (int)Math.Ceiling(maxEncodingDuration);

            for (int i = 0; i < _postTestTrials.Count; i++)
            {
                if (i >= _postTestTrialVisibility.Count || !_postTestTrialVisibility[i])
                    continue;

                var trial = _postTestTrials[i];
                
                // Trial Color – teal for control, orange for test
                Color trialColor = trial.IsControl ? Color.FromHex("#94E2D5") : Color.FromHex("#F5A623");

                foreach (var band in activeBands)
                {
                    // Encoding time series
                    GetBandTimeSeries(band.Name, trial.EncodingStart, trial.EncodingEnd, out double[] encXs, out double[] encYs, out double encAvg);

                    // Click averages
                    var clickYs = new System.Collections.Generic.List<double>();
                    double lastTime = trial.RecallStart;
                    for (int c = 0; c < trial.Clicks.Count; c++)
                    {
                        double clickTime = trial.Clicks[c].TimeInSeconds;
                        double avgPower = GetAverageBandPower(band.Name, lastTime, clickTime);
                        clickYs.Add(avgPower);
                        lastTime = clickTime;
                    }

                    // Combined coordinates
                    int totalPoints = encXs.Length + 1 + clickYs.Count; // extra point for NaN gap
                    double[] xs = new double[totalPoints];
                    double[] ys = new double[totalPoints];

                    // Copy encoding series
                    Array.Copy(encXs, 0, xs, 0, encXs.Length);
                    Array.Copy(encYs, 0, ys, 0, encYs.Length);

                    // Insert NaN gap to break line between encoding and clicks
                    int gapIdx = encXs.Length;
                    xs[gapIdx] = double.NaN;
                    ys[gapIdx] = double.NaN;

                    // Insert click series after gap
                    for (int c = 0; c < clickYs.Count; c++)
                    {
                        xs[gapIdx + 1 + c] = maxEncSec + (c + 1.0);
                        ys[gapIdx + 1 + c] = clickYs[c];
                    }

                    foreach (var val in ys)
                    {
                        if (val > maxVal) maxVal = val;
                    }

                    Color seriesColor = singleBandMode ? trialColor : Color.FromHex(band.HexColor);
                    string suffix = _selectedTaskType == 1 ? (trial.IsControl ? "C" : "T") : "";
                    string legendText = singleBandMode 
                        ? $"Trial {i + 1}{(suffix == "" ? "" : " " + (trial.IsControl ? "Control" : "Test"))} (Lvl {trial.Level})" 
                        : $"T{i + 1}{suffix} - {band.Label}";

                    var sc = WpfPlotPostTestAnalysis.Plot.Add.Scatter(xs, ys);
                    sc.Color = seriesColor;
                    sc.LineWidth = 2.5f;
                    sc.LegendText = legendText;
                    sc.MarkerSize = 0; // custom markers rendered below

                    // Draw presentation vertical lines
                    foreach (var pTime in trial.PresentationTimes)
                    {
                        double pRelative = pTime - trial.EncodingStart;
                        var vline = WpfPlotPostTestAnalysis.Plot.Add.VerticalLine(pRelative);
                        vline.Color = new Color(seriesColor.R, seriesColor.G, seriesColor.B, 100);
                        vline.LinePattern = LinePattern.Dashed;
                        vline.LineWidth = 1.5f;
                    }

                    // Draw numbered bubbles:
                    // 1. Transition Point (end of encoding, X = 0)
                    int transitionIdx = encXs.Length - 1;
                    if (transitionIdx >= 0 && transitionIdx < xs.Length)
                    {
                        AddBubbleMarker(xs[transitionIdx], ys[transitionIdx], seriesColor, i + 1, false);
                    }

                    // 2. Click Points (X = 1, 2, 3...)
                    for (int c = 0; c < clickYs.Count; c++)
                    {
                        int ptIdx = encXs.Length + c;
                        if (ptIdx < xs.Length)
                        {
                            bool isWrong = trial.Clicks[c].IsWrong;
                            AddBubbleMarker(xs[ptIdx], ys[ptIdx], seriesColor, i + 1, isWrong);
                        }
                    }
                }
            }

            // Set custom X-axis ticks (negative values = encoding time series, positive = click averages)
            var positionsList = new System.Collections.Generic.List<double>();
            var labelsList = new System.Collections.Generic.List<string>();

            int step = maxEncSec > 6 ? 2 : 1;
            for (int t = 0; t <= maxEncSec; t += step)
            {
                positionsList.Add(t);
                labelsList.Add($"{t}s");
            }

            for (int k = 1; k <= maxClicksObserved; k++)
            {
                positionsList.Add(maxEncSec + k);
                labelsList.Add($"Click {k}");
            }
            WpfPlotPostTestAnalysis.Plot.Axes.Bottom.SetTicks(positionsList.ToArray(), labelsList.ToArray());

            WpfPlotPostTestAnalysis.Plot.ShowLegend(Alignment.UpperRight);
            WpfPlotPostTestAnalysis.Plot.Legend.BackgroundColor = Color.FromHex("#1E1E2E");
            WpfPlotPostTestAnalysis.Plot.Legend.FontColor = Color.FromHex("#CDD6F4");
            WpfPlotPostTestAnalysis.Plot.Legend.OutlineColor = Color.FromHex("#45475A");

            WpfPlotPostTestAnalysis.Plot.Axes.SetLimits(-0.5, maxEncodingDuration + maxClicksObserved + 0.5, 0, maxVal * 1.25);
            WpfPlotPostTestAnalysis.Refresh();
        }

        private void AddBubbleMarker(double px, double py, Color color, int trialNum, bool isWrong)
        {
            // Outer white ring
            var borderMarker = WpfPlotPostTestAnalysis.Plot.Add.Marker(px, py);
            borderMarker.Shape = MarkerShape.FilledCircle;
            borderMarker.Color = Color.FromHex("#FFFFFF");
            borderMarker.Size = 18;

            // Inner colored bubble
            var fillMarker = WpfPlotPostTestAnalysis.Plot.Add.Marker(px, py);
            fillMarker.Shape = MarkerShape.FilledCircle;
            fillMarker.Color = isWrong ? Color.FromHex("#F38BA8") : color;
            fillMarker.Size = 14;

            // Trial index number inside
            var txt = WpfPlotPostTestAnalysis.Plot.Add.Text(trialNum.ToString(), px, py);
            txt.LabelText = trialNum.ToString();
            txt.LabelFontColor = Color.FromHex("#11111B");
            txt.LabelFontSize = 9.5f;
            txt.LabelBold = true;
            txt.LabelAlignment = Alignment.MiddleCenter;
        }

        private void SetupSpectrogramPlot()
        {
            WpfPlotSpectrogram.Plot.Title("Prefrontal Dynamic PSD (AF7/AF8 Average, 0-40 Hz)");
            WpfPlotSpectrogram.Plot.XLabel("Frequency (Hz)");
            WpfPlotSpectrogram.Plot.YLabel("Power (dB)");

            WpfPlotSpectrogram.Plot.FigureBackground.Color = Color.FromHex("#1E1E2E");
            WpfPlotSpectrogram.Plot.DataBackground.Color = Color.FromHex("#313244");
            WpfPlotSpectrogram.Plot.Axes.Color(Color.FromHex("#A6ADC8"));

            // Band Shading up to 40 Hz using reliable RGBA byte constructor
            byte alpha = 60; // Semi-transparent
            WpfPlotSpectrogram.Plot.Add.HorizontalSpan(1, 4, new Color(100, 100, 100, alpha)); // Delta
            WpfPlotSpectrogram.Plot.Add.HorizontalSpan(4, 8, new Color(128, 0, 128, alpha));   // Theta
            WpfPlotSpectrogram.Plot.Add.HorizontalSpan(8, 12, new Color(0, 128, 0, alpha));    // Alpha
            WpfPlotSpectrogram.Plot.Add.HorizontalSpan(12, 30, new Color(0, 0, 128, alpha));   // Beta
            WpfPlotSpectrogram.Plot.Add.HorizontalSpan(30, 40, new Color(128, 128, 0, alpha)); // Gamma

            // Initialize frequency array 0 to 40 Hz
            for (int r = 0; r < 161; r++)
            {
                _spectrogramFreqs[r] = r * 0.25;
                _spectrogramLivePowers[r] = 0;
                _spectrogramAvgPowers[r] = 0;
            }

            // Initialize rolling history array
            for (int r = 0; r < 161; r++)
                for (int c = 0; c < 200; c++)
                    _spectrogramData[r, c] = 0.0;

            // Live scatter: faded blue
            _spectrogramLiveScatter = WpfPlotSpectrogram.Plot.Add.Scatter(_spectrogramFreqs, _spectrogramLivePowers);
            _spectrogramLiveScatter.Color = new Color(137, 180, 250, 100); // Faded blue (#89B4FA with alpha)
            _spectrogramLiveScatter.LineWidth = 1;
            _spectrogramLiveScatter.LegendText = "Live Slice";

            // Average scatter: solid bright blue
            _spectrogramAvgScatter = WpfPlotSpectrogram.Plot.Add.Scatter(_spectrogramFreqs, _spectrogramAvgPowers);
            _spectrogramAvgScatter.Color = Color.FromHex("#89B4FA");
            _spectrogramAvgScatter.LineWidth = 2.5f;
            _spectrogramAvgScatter.LegendText = "2s Moving Avg";

            WpfPlotSpectrogram.Plot.ShowLegend(Alignment.UpperRight);

            // Default Axis Limits (Log mode): X: 0 to 40 Hz, Y: -20 to 40 dB
            WpfPlotSpectrogram.Plot.Axes.SetLimits(0, 40, -20, 40);

            WpfPlotSpectrogram.Refresh();
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
                            WpfPlotDebugFir.Refresh();
                            WpfPlotDebugFiltered.Refresh();
                            WpfPlotDebugPSD.Refresh();
                            WpfPlotOverlay.Refresh();
                            WpfPlotPSD_MemoryTask.Refresh();
                            WpfPlotSpectrogram.Refresh();
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
            if (payload.PsdFreqs.Count == 401 && payload.PsdPowers.Count == 401)
            {
                Dispatcher.Invoke(() => 
                {
                    // Compute total power in the 1-40 Hz range for Relative (%) calculations
                    double totalPower = 0;
                    for (int i = 0; i < 401; i++)
                    {
                        double freq = payload.PsdFreqs[i];
                        if (freq >= 1.0 && freq <= 40.0)
                        {
                            totalPower += payload.PsdPowers[i];
                        }
                    }
                    if (totalPower <= 0) totalPower = 1e-9;

                    // 1. Process Monitor/Debug PSD
                    bool monitorRelative = ComboPsdMode?.SelectedIndex == 1;
                    for (int i = 0; i < 401; i++)
                    {
                        double freq = payload.PsdFreqs[i];
                        _psdFreqs[i] = freq;
                        double power = payload.PsdPowers[i];
                        _psdPowers[i] = monitorRelative ? (power / totalPower) * 100.0 : 10.0 * Math.Log10(Math.Max(power, 1e-9));
                    }

                    // 2. Process NFB PSD
                    bool nfbRelative = ComboPsdModeNfb?.SelectedIndex == 1;
                    for (int i = 0; i < 401; i++)
                    {
                        double freq = payload.PsdFreqs[i];
                        _psdNfbFreqs[i] = freq;
                        double power = payload.PsdPowers[i];
                        _psdNfbPowers[i] = nfbRelative ? (power / totalPower) * 100.0 : 10.0 * Math.Log10(Math.Max(power, 1e-9));
                    }

                    // 3. Process Memory Task PSD
                    bool taskRelative = ComboPsdModeTask?.SelectedIndex == 1;
                    for (int i = 0; i < 401; i++)
                    {
                        double freq = payload.PsdFreqs[i];
                        _psdMemoryTaskFreqs[i] = freq;
                        double power = payload.PsdPowers[i];
                        _psdMemoryTaskPowers[i] = taskRelative ? (power / totalPower) * 100.0 : 10.0 * Math.Log10(Math.Max(power, 1e-9));
                    }

                    // 4. Update Spectrogram Rolling Data (0-40 Hz covers indices 0 to 160 at 0.25 Hz steps)
                    if (payload.PsdPowers.Count >= 161)
                    {
                        // Shift rolling history left by 1 column
                        for (int col = 0; col < 199; col++)
                        {
                            for (int row = 0; row < 161; row++)
                            {
                                _spectrogramData[row, col] = _spectrogramData[row, col + 1];
                            }
                        }

                        // Write new slice to rightmost column
                        bool spectroRelative = ComboSpectroMode?.SelectedIndex == 1;
                        for (int row = 0; row < 161; row++)
                        {
                            double p = payload.PsdPowers[row];
                            _spectrogramData[row, 199] = spectroRelative ? (p / totalPower) * 100.0 : 10.0 * Math.Log10(Math.Max(p, 1e-9));
                        }

                        // Update Spectrogram Line arrays
                        for (int row = 0; row < 161; row++)
                        {
                            _spectrogramLivePowers[row] = _spectrogramData[row, 199];

                            // Calculate 2-second moving average (40 slices at ~20 FPS)
                            double sum = 0;
                            int count = 40;
                            for (int c = 200 - count; c < 200; c++)
                            {
                                sum += _spectrogramData[row, c];
                            }
                            _spectrogramAvgPowers[row] = sum / count;
                        }
                    }
                });
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded) return;
                
                string statusText = payload.Metrics.SignalIntegrity.ToString().ToUpper();
                TxtStatus.Text = statusText;

                // Update Stalled indicator
                TxtStalled.Visibility = payload.IsStalled ? Visibility.Visible : Visibility.Collapsed;
                if (payload.IsStalled) return; // Don't process garbage if stalled

                // Update Integrity Light
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
                _currentDelta = payload.BandPower.Delta;
                _currentTheta = payload.BandPower.Theta;
                _currentAlpha = payload.BandPower.Alpha;
                _currentBeta = payload.BandPower.Beta;
                _currentGamma = payload.BandPower.Gamma;

                // Live Memory Task Gamma display
                double gammaPower = payload.BandPower.Gamma;
                PbMemoryGamma.Value = Math.Clamp(gammaPower, 0.0, 50.0);
                TxtMemoryGamma.Text = $"Power: {gammaPower:F1}";

                // Update Brainwave Trend Plot lists when challenge is active
                if (_isGameRunning)
                {
                    lock (_trendGamma)
                    {
                        _trendDelta.Add(payload.BandPower.Delta);
                        _trendTheta.Add(payload.BandPower.Theta);
                        _trendAlpha.Add(payload.BandPower.Alpha);
                        _trendBeta.Add(payload.BandPower.Beta);
                        _trendGamma.Add(payload.BandPower.Gamma);
                    }
                    UpdateTrendPlot();
                }

                // Neurofeedback UI
                double ratio = payload.SmoothedAlphaRatio;
                AlphaRatioText.Text = ratio.ToString("F2");
                AlphaRatioBar.Value = ratio;

                // Manual Difficulty Adjustment
                double difficultyOffset = SliderDifficulty.Value;
                TxtDifficulty.Text = $"Offset: {(difficultyOffset >= 0 ? "+" : "")}{difficultyOffset:F2}";
                double effectiveTarget = payload.TargetRatio + difficultyOffset;
                
                TargetRatioText.Text = $"Target: {effectiveTarget:F2}";
                PbCalibration.Value = payload.CalibrationProgress * 100;
                
                if (payload.CalibrationProgress > 0 && payload.CalibrationProgress < 1)
                {
                    BtnCalibrate.Content = "Calibrating...";
                    BtnCalibrate.IsEnabled = false;
                }
                else
                {
                    BtnCalibrate.Content = "Start 30s Baseline";
                    BtnCalibrate.IsEnabled = true;
                }

                // Audio Volume Logic
                // If ratio >= target, volume = 0 (silent)
                // If ratio < target, volume is proportional
                if (ratio >= effectiveTarget)
                {
                    _targetVolume = 0;
                }
                else
                {
                    // Volume = Difference (clamped 0-1)
                    _targetVolume = Math.Clamp(effectiveTarget - ratio, 0, 1);
                }

                // Smooth volume "slope"
                _currentVolume = _currentVolume * 0.9 + _targetVolume * 0.1;
                _mediaPlayer.Volume = _currentVolume;
                TxtVolume.Text = $"Volume: {(_currentVolume * 100):F0}%";

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
                double raw = 0, notched = 0, fir = 0, filtered = 0;
                if (RbDebugTP9.IsChecked == true) { raw = payload.RawChannels.Tp9; notched = payload.NotchedChannels.Tp9; fir = payload.FirChannels.Tp9; filtered = payload.Channels.Tp9; }
                else if (RbDebugAF7.IsChecked == true) { raw = payload.RawChannels.Af7; notched = payload.NotchedChannels.Af7; fir = payload.FirChannels.Af7; filtered = payload.Channels.Af7; }
                else if (RbDebugAF8.IsChecked == true) { raw = payload.RawChannels.Af8; notched = payload.NotchedChannels.Af8; fir = payload.FirChannels.Af8; filtered = payload.Channels.Af8; }
                else if (RbDebugTP10.IsChecked == true) { raw = payload.RawChannels.Tp10; notched = payload.NotchedChannels.Tp10; fir = payload.FirChannels.Tp10; filtered = payload.Channels.Tp10; }
                else if (RbDebugAvg.IsChecked == true)
                {
                    raw = (payload.RawChannels.Tp9 + payload.RawChannels.Af7 + payload.RawChannels.Af8 + payload.RawChannels.Tp10) / 4.0;
                    notched = (payload.NotchedChannels.Tp9 + payload.NotchedChannels.Af7 + payload.NotchedChannels.Af8 + payload.NotchedChannels.Tp10) / 4.0;
                    fir = (payload.FirChannels.Tp9 + payload.FirChannels.Af7 + payload.FirChannels.Af8 + payload.FirChannels.Tp10) / 4.0;
                    filtered = (payload.Channels.Tp9 + payload.Channels.Af7 + payload.Channels.Af8 + payload.Channels.Tp10) / 4.0;
                }

                _debugRawData[_debugDataIndex] = raw;
                _debugNotchedData[_debugDataIndex] = notched;
                _debugFirData[_debugDataIndex] = fir;
                _debugFilteredData[_debugDataIndex] = filtered;
                
                // Update Debug PSD from selected channel
                var psdSource = payload.PsdPowers;
                if (RbDebugTP9.IsChecked == true) psdSource = payload.PsdTp9;
                else if (RbDebugAF7.IsChecked == true) psdSource = payload.PsdAf7;
                else if (RbDebugAF8.IsChecked == true) psdSource = payload.PsdAf8;
                else if (RbDebugTP10.IsChecked == true) psdSource = payload.PsdTp10;

                if (psdSource.Count == 401)
                {
                    for (int i = 0; i < 401; i++) _debugPsdPowers[i] = psdSource[i];
                }

                _debugDataIndex = (_debugDataIndex + 1) % 512;
            });
        }

        private void ComboPsdMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyStandardPsdLimits();
        }
        
        private void ApplyStandardPsdLimits()
        {
            if (ComboPsdMode == null || ComboPsdModeNfb == null || ComboPsdModeTask == null || ComboSpectroMode == null) return;

            // Monitor PSD Limits
            bool monitorRelative = ComboPsdMode.SelectedIndex == 1;
            WpfPlotPSD_Monitor.Plot.Axes.SetLimits(0, 100, monitorRelative ? 0 : -20, monitorRelative ? 10 : 40);
            WpfPlotPSD_Monitor.Plot.YLabel(monitorRelative ? "Relative Power (%)" : "Power (dB)");

            // NFB PSD Limits
            bool nfbRelative = ComboPsdModeNfb.SelectedIndex == 1;
            WpfPlotPSD_NFB.Plot.Axes.SetLimits(0, 100, nfbRelative ? 0 : -20, nfbRelative ? 10 : 40);
            WpfPlotPSD_NFB.Plot.YLabel(nfbRelative ? "Relative Power (%)" : "Power (dB)");

            // Memory Task PSD Limits
            bool taskRelative = ComboPsdModeTask.SelectedIndex == 1;
            WpfPlotPSD_MemoryTask.Plot.Axes.SetLimits(0, 100, taskRelative ? 0 : -20, taskRelative ? 10 : 40);
            WpfPlotPSD_MemoryTask.Plot.YLabel(taskRelative ? "Relative Power (%)" : "Power (dB)");

            // Spectrogram PSD Limits
            bool spectroRelative = ComboSpectroMode.SelectedIndex == 1;
            WpfPlotSpectrogram.Plot.Axes.SetLimits(0, 40, spectroRelative ? 0 : -20, spectroRelative ? 10 : 40);
            WpfPlotSpectrogram.Plot.YLabel(spectroRelative ? "Relative Power (%)" : "Power (dB)");

            WpfPlotPSD_Monitor.Refresh();
            WpfPlotPSD_NFB.Refresh();
            WpfPlotPSD_MemoryTask.Refresh();
            WpfPlotSpectrogram.Refresh();
        }

        private void WpfPlotPSD_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
            {
                e.Handled = true; // Prevent ScottPlot default AutoScale
                ApplyStandardPsdLimits();
            }
        }

        private void RbDebug_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            Array.Clear(_debugRawData, 0, 512);
            Array.Clear(_debugNotchedData, 0, 512);
            Array.Clear(_debugFirData, 0, 512);
            Array.Clear(_debugFilteredData, 0, 512);
            Array.Clear(_debugPsdPowers, 0, 101);
            _debugDataIndex = 0;
            
            WpfPlotDebugRaw?.Refresh();
            WpfPlotDebugNotched?.Refresh();
            WpfPlotDebugFir?.Refresh();
            WpfPlotDebugFiltered?.Refresh();
            WpfPlotDebugPSD?.Refresh();
            WpfPlotOverlay?.Refresh();
        }

        private void CbLayer_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _sigRaw == null) return;
            
            _sigRaw.IsVisible = CbShowRaw.IsChecked == true;
            if (_sigNotch != null) _sigNotch.IsVisible = CbShowNotched.IsChecked == true;
            if (_sigFir != null) _sigFir.IsVisible = CbShowFir.IsChecked == true;
            if (_sigIir != null) _sigIir.IsVisible = CbShowIir.IsChecked == true;
            
            WpfPlotOverlay.Refresh();
        }

        private async void RbFilterMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            var mode = Nnafa.State.V1.FilterMode.FilterBoth;
            if (RbModeIirOnly.IsChecked == true) mode = Nnafa.State.V1.FilterMode.FilterOnlyIir;
            if (RbModeFirOnly.IsChecked == true) mode = Nnafa.State.V1.FilterMode.FilterOnlyFir;

            var request = new Nnafa.State.V1.StateRequest
            {
                Settings = new Nnafa.State.V1.SessionSettings { FilterMode = mode }
            };

            await _telemetryClient.SendAsync(request.ToByteArray(), _cts.Token);
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

        private async void BtnCalibrate_Click(object sender, RoutedEventArgs e)
        {
            if (_telemetryClient == null) return;
            var request = new Nnafa.State.V1.StateRequest
            {
                TargetState = Nnafa.State.V1.SystemState.StateCalibrating
            };
            await _telemetryClient.SendAsync(request.ToByteArray(), _cts.Token);
        }

        private void BtnSelectAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.m4a|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAudioFile.Text = System.IO.Path.GetFileName(dialog.FileName);
                _mediaPlayer.Open(new Uri(dialog.FileName));
                _mediaPlayer.MediaEnded += (s, ev) => { _mediaPlayer.Position = TimeSpan.Zero; _mediaPlayer.Play(); }; // Loop
                _mediaPlayer.Play();
                _mediaPlayer.Volume = 0;
            }
        }

        private async void SliderSmoothing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            TxtSmoothing.Text = $"Window: {SliderSmoothing.Value:F1}s";
            
            var request = new Nnafa.State.V1.StateRequest
            {
                Settings = new Nnafa.State.V1.SessionSettings { SmoothingWindowS = (float)SliderSmoothing.Value }
            };
            await _telemetryClient.SendAsync(request.ToByteArray(), _cts.Token);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            var request = new Nnafa.State.V1.StateRequest
            {
                Settings = new Nnafa.State.V1.SessionSettings { ResetBuffers = true }
            };
            await _telemetryClient.SendAsync(request.ToByteArray(), _cts.Token);
            MessageBox.Show("Historical data cleared.", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            await _telemetryClient.DisconnectAsync();
            StopLslStream();
        }

        private void BtnToggleLsl_Click(object sender, RoutedEventArgs e)
        {
            if (_lslProcess != null && !_lslProcess.HasExited)
            {
                StopLslStream();
            }
            else
            {
                StartLslStream();
            }
        }

        private void StartLslStream()
        {
            try
            {
                string provider = (ComboProvider.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string model = (ComboDeviceModel.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
                string? projectRoot = FindProjectRoot(AppDomain.CurrentDomain.BaseDirectory);
                if (projectRoot == null)
                {
                    projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
                }
                
                string venvPath = System.IO.Path.Combine(projectRoot, "venv", "Scripts", "python.exe");
                string scriptPath = System.IO.Path.Combine(projectRoot, "src", "01_ingestion", "brainflow_lsl_bridge.py");
                
                // Normalize paths
                venvPath = System.IO.Path.GetFullPath(venvPath);
                scriptPath = System.IO.Path.GetFullPath(scriptPath);

                string args = "";

                if (provider.Contains("BlueMuse"))
                {
                    // For BlueMuse, we just try to launch the app if it's installed.
                    Process.Start(new ProcessStartInfo("bluemuse://") { UseShellExecute = true });
                    QueueLslMessage("Status: Launched BlueMuse Protocol");
                    return;
                }
                else if (provider.Contains("Synthetic"))
                {
                    args = $"\"{scriptPath}\" --board-id -1"; // SYNTHETIC_BOARD
                }
                else
                {
                    int boardId = 38; // Default: MUSE_2_BOARD
                    if (provider.Contains("Native"))
                    {
                        if (model.Contains("Muse S")) boardId = 39; // MUSE_S_BOARD
                        else if (model.Contains("Muse 2016")) boardId = 41; // MUSE_2016_BOARD
                        else boardId = 38; // MUSE_2_BOARD
                        
                        args = $"\"{scriptPath}\" --board-id {boardId}";
                        if (!string.IsNullOrEmpty(TxtMacAddr.Text)) args += $" --mac-address {TxtMacAddr.Text}";
                    }
                    else if (provider.Contains("BLED112"))
                    {
                        if (model.Contains("Muse S")) boardId = 21; // MUSE_S_BLED_BOARD
                        else if (model.Contains("Muse 2016")) boardId = 42; // MUSE_2016_BLED_BOARD
                        else boardId = 22; // MUSE_2_BLED_BOARD

                        args = $"\"{scriptPath}\" --board-id {boardId} --serial-port {TxtComPort.Text}";
                        if (!string.IsNullOrEmpty(TxtMacAddr.Text)) args += $" --mac-address {TxtMacAddr.Text}";
                    }
                }

                ProcessStartInfo psi = new()
                {
                    FileName = venvPath,
                    Arguments = $"-u {args}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _lslProcess = new Process();
                _lslProcess.StartInfo = psi;
                _lslProcess.EnableRaisingEvents = true;

                _lslProcess.OutputDataReceived += (s, ev) => 
                {
                    if (!string.IsNullOrEmpty(ev.Data) && ev.Data.Contains("[STATUS]"))
                    {
                        string status = ev.Data.Replace("[STATUS]", "").Trim();
                        Dispatcher.Invoke(() => 
                        {
                            if (TxtLslStatus != null) QueueLslMessage($"Status: {status}");
                            if (status == "CONNECTED")
                            {
                                BtnToggleLsl.Background = Brushes.LimeGreen;
                            }
                            else if (status.Contains("ERROR"))
                            {
                                BtnToggleLsl.Background = Brushes.Red;
                            }
                        });
                    }
                };

                _lslProcess.ErrorDataReceived += (s, ev) => 
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                    {
                        // Ignore non-error strings that liblsl/native DLLs print to stderr
                        if (ev.Data.Contains("INFO|") || ev.Data.Contains("DEBUG|")) return;

                        Dispatcher.Invoke(() => 
                        {
                            if (TxtLslStatus != null) 
                            {
                                QueueLslMessage($"Error: {ev.Data}");
                            }
                            BtnToggleLsl.Background = Brushes.Red;
                        });
                    }
                };

                _lslProcess.Start();
                _lslProcess.BeginOutputReadLine();
                _lslProcess.BeginErrorReadLine();

                BtnToggleLsl.Content = "Stop LSL Stream";
                BtnToggleLsl.Background = Brushes.Orange;
                if (TxtLslStatus != null) QueueLslMessage("Status: Initializing...");
                
                // Monitor for exit
                _lslProcess.Exited += (s, ev) => Dispatcher.Invoke(() => ResetLslUi());
                _lslProcess.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start LSL Provider: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopLslStream()
        {
            try
            {
                if (_lslProcess != null && !_lslProcess.HasExited)
                {
                    _lslProcess.Kill(true);
                }
            }
            catch { }
            ResetLslUi();
        }

        private void ResetLslUi()
        {
            BtnToggleLsl.Content = "Start LSL Stream";
            BtnToggleLsl.Background = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;
            TxtLslStatus.Text = "Status: Idle";
            _lslProcess = null;
        }

        private static string? FindProjectRoot(string startDir)
        {
            string? currentDir = startDir;
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (System.IO.Directory.Exists(System.IO.Path.Combine(currentDir, "venv")) &&
                    System.IO.Directory.Exists(System.IO.Path.Combine(currentDir, "src")))
                {
                    return currentDir;
                }
                currentDir = System.IO.Path.GetDirectoryName(currentDir);
            }
            return null;
        }

        // ==========================================
        // Working Memory Challenge (Digit Span / Spatial Sequence Tasks)
        // ==========================================

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag != null)
            {
                if (int.TryParse(btn.Tag.ToString(), out int index))
                {
                    MainTabControl.SelectedIndex = index;
                }
            }
        }

        private void ComboMemoryTaskType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _selectedTaskType = ComboMemoryTaskType.SelectedIndex;

            if (_isGameRunning)
            {
                EndMemoryGame("Task type changed. Challenge stopped.");
            }

            if (_selectedTaskType == 0) // Digit Span
            {
                BorderDigitSpanView.Visibility = Visibility.Visible;
                BorderSpatialView.Visibility = Visibility.Collapsed;
                PanelDigitSpanInput.Visibility = Visibility.Visible;
                PanelSpatialTimeAdjuster.Visibility = Visibility.Collapsed;
            }
            else // Spatial Sequence
            {
                BorderDigitSpanView.Visibility = Visibility.Collapsed;
                BorderSpatialView.Visibility = Visibility.Visible;
                PanelDigitSpanInput.Visibility = Visibility.Collapsed;
                PanelSpatialTimeAdjuster.Visibility = Visibility.Visible;
                CanvasSpatialMemory.Children.Clear();
            }
        }

        private async void BtnStartMemory_Click(object sender, RoutedEventArgs e)
        {
            if (_isGameRunning)
            {
                EndMemoryGame("Challenge stopped.");
                return;
            }

            lock (_trendGamma)
            {
                _trendDelta.Clear();
                _trendTheta.Clear();
                _trendAlpha.Clear();
                _trendBeta.Clear();
                _trendGamma.Clear();
            }
            _challengeStartTime = DateTime.Now;
            lock (_cognitiveEvents)
            {
                _cognitiveEvents.Clear();
                _cognitiveEvents.Add(new CognitiveEvent { TimeInSeconds = 0, Text = "Challenge started", IsUserEvent = false });
            }
            lock (_cognitiveIntervals)
            {
                _cognitiveIntervals.Clear();
            }
            UpdateTrendPlot();

            _isGameRunning = true;
            _memoryLevel = _selectedTaskType == 0 ? 3 : 8; // spatial starts at 8
            _memoryScore = 0;
            _memoryStrikes = 0;
            _isControlTrial = true;
            _isLevelLocked = false;
            _lockedTrialsCount = 0;
            _totalCalibrationTrials = 0;
            BtnStartMemory.Content = "Stop Challenge";
            BtnStartMemory.Background = (Brush)new BrushConverter().ConvertFrom("#F38BA8")!; // Red stop button
            BtnStartMemory.Foreground = (Brush)new BrushConverter().ConvertFrom("#11111B")!;

            UpdateMemoryGameStats();
            
            if (_selectedTaskType == 0)
            {
                await StartNewMemoryRoundAsync();
            }
            else
            {
                await StartNewSpatialRoundAsync();
            }
        }

        private async Task StartNewMemoryRoundAsync()
        {
            if (!_isGameRunning) return;

            _sequenceString = "";
            for (int i = 0; i < _memoryLevel; i++)
            {
                _sequenceString += _random.Next(0, 10).ToString();
            }

            StartCognitiveInterval(isEncoding: true, level: _memoryLevel);
            RecordCognitiveEvent($"Started presentation of set {_memoryLevel}", false);

            MemoryGameStatusText.Text = "Get Ready...";
            MemoryGameStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom("#89B4FA")!;
            await Task.Delay(1500);

            await DisplaySequenceAsync(_sequenceString);
        }

        private async Task DisplaySequenceAsync(string sequence)
        {
            _isDisplayingSequence = true;
            BtnStartMemory.IsEnabled = false;
            TxtMemoryInput.IsEnabled = false;
            BtnSubmitMemory.IsEnabled = false;

            for (int i = 0; i < sequence.Length; i++)
            {
                if (!_isGameRunning) return;

                char digit = sequence[i];
                MemoryGameStatusText.Text = digit.ToString();
                MemoryGameStatusText.Foreground = Brushes.Yellow;

                RecordCognitiveEvent($"Presented digit: {digit}", false);

                await Task.Delay(800);

                if (!_isGameRunning) return;

                MemoryGameStatusText.Text = "-";
                MemoryGameStatusText.Foreground = Brushes.Gray;
                await Task.Delay(200);
            }

            if (!_isGameRunning) return;

            StartCognitiveInterval(isEncoding: false, level: _memoryLevel);
            RecordCognitiveEvent($"Started recall of set {_memoryLevel}", false);

            MemoryGameStatusText.Text = "Type the sequence!";
            MemoryGameStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!;
            _isDisplayingSequence = false;

            _digitRecallIndex = 0;
            TxtMemoryInput.IsEnabled = true;
            BtnSubmitMemory.IsEnabled = true;
            TxtMemoryInput.Text = "";
            TxtMemoryInput.Focus();
        }

        private int _digitRecallIndex = 0;

        private void TxtMemoryInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isGameRunning || _isDisplayingSequence) return;
            string text = TxtMemoryInput.Text.Trim();
            if (text.Length > _digitRecallIndex)
            {
                char typedDigit = text[text.Length - 1];
                bool isCorrect = false;
                if (_digitRecallIndex < _sequenceString.Length)
                {
                    isCorrect = (typedDigit == _sequenceString[_digitRecallIndex]);
                }
                
                RecordCognitiveEvent($"User typed digit: {typedDigit} {(isCorrect ? "" : "(Incorrect)")}", true);
                _digitRecallIndex = text.Length;
            }
            else if (text.Length < _digitRecallIndex)
            {
                _digitRecallIndex = text.Length;
            }
        }

        private void BtnSubmitMemory_Click(object sender, RoutedEventArgs e)
        {
            SubmitMemoryAnswer();
        }

        private void TxtMemoryInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SubmitMemoryAnswer();
            }
        }

        private async void SubmitMemoryAnswer()
        {
            if (!_isGameRunning || _isDisplayingSequence) return;

            string answer = TxtMemoryInput.Text.Trim();
            RecordCognitiveEvent($"User submitted digit recall answer: \"{answer}\"", true);

            if (answer == _sequenceString)
            {
                FinalizeActiveInterval();
                RecordCognitiveEvent($"Set {_memoryLevel} recall correct", false);

                _memoryScore += _memoryLevel * 10;
                _memoryLevel++; // Increase difficulty
                MemoryGameStatusText.Text = "CORRECT!";
                MemoryGameStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;

                UpdateMemoryGameStats();

                TxtMemoryInput.IsEnabled = false;
                BtnSubmitMemory.IsEnabled = false;

                await Task.Delay(1500);
                await StartNewMemoryRoundAsync();
            }
            else
            {
                FinalizeActiveInterval();
                _memoryStrikes++;
                RecordCognitiveEvent($"Set {_memoryLevel} recall incorrect (Strike {_memoryStrikes}/3)", false);

                if (_memoryLevel > 3)
                {
                    _memoryLevel--; // Adaptive: drops difficulty when user makes a mistake
                }

                UpdateMemoryGameStats();

                if (_memoryStrikes >= 3)
                {
                    EndMemoryGame("Incorrect! Strike 3/3.");
                }
                else
                {
                    MemoryGameStatusText.Text = $"INCORRECT!\nStrike {_memoryStrikes}/3\nDifficulty adapted to Level {_memoryLevel}";
                    MemoryGameStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom("#F9E2AF")!;

                    TxtMemoryInput.IsEnabled = false;
                    BtnSubmitMemory.IsEnabled = false;

                    await Task.Delay(2000);
                    await StartNewMemoryRoundAsync();
                }
            }
        }

        private void UpdateMemoryGameStats()
        {
            string typeLabel = _selectedTaskType == 0 ? "Digits" : "Squares";
            string phaseLabel = "";
            if (_selectedTaskType == 1)
            {
                phaseLabel = _isControlTrial ? " (Control)" : " (Test)";
            }
            TxtMemoryLevel.Text = $"Level: {_memoryLevel} ({typeLabel}){phaseLabel}";
            TxtMemoryScore.Text = $"Score: {_memoryScore}";
            TxtMemoryStrikes.Text = $"Strikes: {_memoryStrikes}/3";
        }

        private void EndMemoryGame(string message)
        {
            _isGameRunning = false;
            _isDisplayingSequence = false;

            RecordCognitiveEvent($"Challenge ended: {message}", false);

            if (_spatialMaskTimer != null)
            {
                _spatialMaskTimer.Stop();
                _spatialMaskTimer = null;
            }

            CanvasSpatialMemory.Children.Clear();

            BtnStartMemory.Content = "Start Challenge";
            BtnStartMemory.Background = (Brush)new BrushConverter().ConvertFrom("#89B4FA")!;
            BtnStartMemory.Foreground = (Brush)new BrushConverter().ConvertFrom("#11111B")!;
            BtnStartMemory.IsEnabled = true;

            TxtMemoryInput.IsEnabled = false;
            TxtMemoryInput.Text = "";
            BtnSubmitMemory.IsEnabled = false;

            if (_selectedTaskType == 0)
            {
                MemoryGameStatusText.Text = $"{message}\nFinal Score: {_memoryScore}\nMax Level Achieved: {_memoryLevel}";
                MemoryGameStatusText.Foreground = (Brush)new BrushConverter().ConvertFrom("#F38BA8")!;
            }
            else
            {
                TxtSpatialStatus.Text = $"{message} Score: {_memoryScore} | Level: {_memoryLevel}";
                TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#F38BA8")!;
                
                // Add a text block on canvas to show final score
                var text = new TextBlock
                {
                    Text = $"Game Over!\nFinal Score: {_memoryScore}\nMax Level: {_memoryLevel}",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                
                var container = new Border
                {
                    Background = (Brush)new BrushConverter().ConvertFrom("#1E1E2E")!,
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(8),
                    Child = text,
                    Width = 260,
                    Height = 120
                };
                
                Canvas.SetLeft(container, 150);
                Canvas.SetTop(container, 80);
                CanvasSpatialMemory.Children.Add(container);
            }

            FinalizeActiveInterval();
            ExportChallengeResults(message);
            ParseAndPopulatePostTestState();
        }

        // ==========================================
        // Spatial Sequence (Chimpanzee Test) Logic
        // ==========================================

        private async Task StartNewSpatialRoundAsync()
        {
            if (!_isGameRunning) return;

            CanvasSpatialMemory.Children.Clear();
            _spatialSquares.Clear();
            _spatialSquareValues.Clear();
            _spatialNextExpectedValue = 1;
            _isSpatialClickActive = false;

            if (_spatialMaskTimer != null)
            {
                _spatialMaskTimer.Stop();
                _spatialMaskTimer = null;
            }

            StartCognitiveInterval(isEncoding: true, level: _memoryLevel, isControl: _isControlTrial);
            if (_isControlTrial)
            {
                RecordCognitiveEvent($"Started presentation of spatial control set {_memoryLevel} (set_diff: {_isLevelLocked})", false);
                TxtSpatialStatus.Text = "Get Ready (Control Phase)...";
            }
            else
            {
                RecordCognitiveEvent($"Started presentation of spatial set {_memoryLevel} (set_diff: {_isLevelLocked})", false);
                TxtSpatialStatus.Text = "Get Ready...";
            }
            TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#89B4FA")!;
            
            await Task.Delay(1500);

            if (!_isGameRunning) return;

            int count = _memoryLevel;
            int cols = 6;
            int rows = 4;
            int totalCells = cols * rows;
            
            var cellIndices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < totalCells; i++) cellIndices.Add(i);
            
            // Shuffle cell slots
            for (int i = cellIndices.Count - 1; i > 0; i--)
            {
                int k = _random.Next(i + 1);
                int temp = cellIndices[i];
                cellIndices[i] = cellIndices[k];
                cellIndices[k] = temp;
            }

            double canvasWidth = CanvasSpatialMemory.ActualWidth > 0 ? CanvasSpatialMemory.ActualWidth : 560;
            double canvasHeight = CanvasSpatialMemory.ActualHeight > 0 ? CanvasSpatialMemory.ActualHeight : 280;

            double cellW = canvasWidth / cols;
            double cellH = canvasHeight / rows;
            double squareSize = 42;

            for (int i = 0; i < count; i++)
            {
                int cellIdx = cellIndices[i % totalCells];
                int col = cellIdx % cols;
                int row = cellIdx / cols;

                double centerX = col * cellW + cellW / 2;
                double centerY = row * cellH + cellH / 2;

                double maxJitterX = (cellW - squareSize - 8) / 2;
                double maxJitterY = (cellH - squareSize - 8) / 2;
                
                double jitterX = (maxJitterX > 0) ? (_random.NextDouble() * 2 - 1) * maxJitterX : 0;
                double jitterY = (maxJitterY > 0) ? (_random.NextDouble() * 2 - 1) * maxJitterY : 0;

                double left = centerX - squareSize / 2 + jitterX;
                double top = centerY - squareSize / 2 + jitterY;

                left = Math.Clamp(left, 5, canvasWidth - squareSize - 5);
                top = Math.Clamp(top, 5, canvasHeight - squareSize - 5);

                int val = i + 1;

                var square = new Border
                {
                    Width = squareSize,
                    Height = squareSize,
                    Background = (Brush)new BrushConverter().ConvertFrom("#89B4FA")!,
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(2),
                    BorderBrush = (Brush)new BrushConverter().ConvertFrom("#45475A")!,
                    Tag = val
                };

                var textBlock = new TextBlock
                {
                    Text = _isControlTrial ? "" : val.ToString(),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)new BrushConverter().ConvertFrom("#11111B")!,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                square.Child = textBlock;
                square.MouseDown += SpatialSquare_MouseDown;

                Canvas.SetLeft(square, left);
                Canvas.SetTop(square, top);

                CanvasSpatialMemory.Children.Add(square);
                _spatialSquares.Add(square);
                _spatialSquareValues.Add(val);
            }

            if (_isControlTrial)
            {
                TxtSpatialStatus.Text = "Just look at the squares...";
            }
            else
            {
                TxtSpatialStatus.Text = "Memorize the numbers!";
            }
            TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#F9E2AF")!;

            // Base time per item + slider offset
            double baseTimePerItemMs = 1200; // Base 1.2s per item
            double userOffsetMs = SliderSpatialTimeAdjuster.Value * 1000.0;
            double msPerItem = baseTimePerItemMs + userOffsetMs;
            if (msPerItem < 200) msPerItem = 200; // minimum clamp
            
            double presentationTimeMs = msPerItem * _memoryLevel;

            _spatialMaskTimer = new System.Windows.Threading.DispatcherTimer();
            _spatialMaskTimer.Interval = TimeSpan.FromMilliseconds(presentationTimeMs);
            _spatialMaskTimer.Tick += (s, e) =>
            {
                _spatialMaskTimer.Stop();
                MaskSpatialSquares();
            };
            _spatialMaskTimer.Start();
        }

        private void MaskSpatialSquares()
        {
            if (!_isGameRunning) return;

            StartCognitiveInterval(isEncoding: false, level: _memoryLevel, isControl: _isControlTrial);
            if (_isControlTrial)
            {
                RecordCognitiveEvent($"Started recall of spatial control set {_memoryLevel}", false);
                TxtSpatialStatus.Text = "Click all squares in any order!";
            }
            else
            {
                RecordCognitiveEvent($"Started recall of spatial set {_memoryLevel}", false);
                TxtSpatialStatus.Text = "Select squares in ascending order!";
            }
            TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!;

            foreach (var square in _spatialSquares)
            {
                if (square.Child is TextBlock tb)
                {
                    tb.Text = "";
                    square.Background = (Brush)new BrushConverter().ConvertFrom("#313244")!;
                    square.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#89B4FA")!;
                }
            }

            _isSpatialClickActive = true;
        }

        private async void SpatialSquare_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isGameRunning || !_isSpatialClickActive) return;

            var clickedBorder = sender as Border;
            if (clickedBorder == null) return;

            // Immediately disable event to prevent double-click spam
            clickedBorder.MouseDown -= SpatialSquare_MouseDown;

            int value = (int)clickedBorder.Tag;

            if (_isControlTrial)
            {
                RecordCognitiveEvent($"User clicked control square #{value}/{_memoryLevel}", true);

                // For control trial, any clicked square is correct!
                clickedBorder.Background = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;
                clickedBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!;

                _spatialNextExpectedValue++;

                if (_spatialNextExpectedValue > _memoryLevel)
                {
                    _isSpatialClickActive = false;
                    FinalizeActiveInterval();
                    RecordCognitiveEvent($"Spatial control set {_memoryLevel} recall correct", false);

                    _memoryScore += _memoryLevel * 10;
                    _isControlTrial = false; // Transition to Test trial at the same level
                    
                    if (_isLevelLocked) {
                        _lockedTrialsCount++;
                        if (_lockedTrialsCount >= 10) {
                            TxtSpatialStatus.Text = "CORRECT!";
                            TxtSpatialStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!;
                            _ = Task.Delay(1500).ContinueWith(t => Dispatcher.Invoke(() => EndMemoryGame("10 locked trials completed!")));
                            return;
                        }
                    }

                    TxtSpatialStatus.Text = "CORRECT!";
                    TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;

                    UpdateMemoryGameStats();
                    await Task.Delay(1500);
                    await StartNewSpatialRoundAsync();
                }
            }
            else
            {
                RecordCognitiveEvent($"User clicked square #{value}/{_memoryLevel}", true);

                if (value == _spatialNextExpectedValue)
                {
                    _spatialNextExpectedValue++;

                    if (clickedBorder.Child is TextBlock tb)
                    {
                        tb.Text = value.ToString();
                        tb.Foreground = (Brush)new BrushConverter().ConvertFrom("#11111B")!;
                    }
                    clickedBorder.Background = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;
                    clickedBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#CDD6F4")!;

                    if (_spatialNextExpectedValue > _memoryLevel)
                    {
                        _isSpatialClickActive = false;
                        FinalizeActiveInterval();
                        RecordCognitiveEvent($"Spatial set {_memoryLevel} recall correct", false);

                        _memoryScore += _memoryLevel * 10;
                        _isControlTrial = true; // Transition to Control trial
                        _totalCalibrationTrials++;
                        if (!_isLevelLocked) {
                            _memoryLevel += 2; // user right -> add 2
                            if (_totalCalibrationTrials >= 10) {
                                _isLevelLocked = true;
                                _lockedTrialsCount = 0;
                            }
                        }

                        TxtSpatialStatus.Text = "CORRECT!";
                        TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#A6E3A1")!;

                        UpdateMemoryGameStats();
                        await Task.Delay(1500);
                        await StartNewSpatialRoundAsync();
                    }
                }
                else
                {
                    _isSpatialClickActive = false;
                    _memoryStrikes++;
                    FinalizeActiveInterval();
                    RecordCognitiveEvent($"Spatial set {_memoryLevel} recall incorrect (Strike {_memoryStrikes}/3)", false);

                    _totalCalibrationTrials++;
                    if (!_isLevelLocked)
                    {
                        if (_memoryLevel > 3) {
                            _memoryLevel--; // user wrong -> subtract 1
                        }
                        if (_totalCalibrationTrials >= 10) {
                            _isLevelLocked = true;
                            _lockedTrialsCount = 0;
                        }
                    }

                    // Reset sequence to Control trial at the adapted level
                    _isControlTrial = true;

                    TxtSpatialStatus.Text = "INCORRECT!";
                    TxtSpatialStatus.Foreground = (Brush)new BrushConverter().ConvertFrom("#F38BA8")!;

                    foreach (var square in _spatialSquares)
                    {
                        int val = (int)square.Tag;
                        if (square.Child is TextBlock tb)
                        {
                            tb.Text = val.ToString();
                            tb.Foreground = (Brush)new BrushConverter().ConvertFrom("#11111B")!;
                        }
                        if (val == value)
                        {
                            square.Background = (Brush)new BrushConverter().ConvertFrom("#F38BA8")!;
                        }
                        else if (val < _spatialNextExpectedValue)
                        {
                            // Already correct
                        }
                        else
                        {
                            square.Background = (Brush)new BrushConverter().ConvertFrom("#F9E2AF")!;
                        }
                    }

                    UpdateMemoryGameStats();

                    if (_memoryStrikes >= 3)
                    {
                        await Task.Delay(2000);
                        EndMemoryGame("Strikes 3/3. Game Over!");
                    }
                    else
                    {
                        await Task.Delay(2500);
                        await StartNewSpatialRoundAsync();
                    }
                }
            }
        }

        // ==========================================
        // DSP Settings Tab Event Handlers
        // ==========================================

        private void SliderLowPass_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || TxtLowPassSetting == null) return;
            TxtLowPassSetting.Text = $"{SliderLowPass.Value:F0} Hz";
        }

        private void SliderHighPass_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || TxtHighPassSetting == null) return;
            TxtHighPassSetting.Text = $"{SliderHighPass.Value:F1} Hz";
        }

        private async void BtnApplyFilterSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_telemetryClient == null) return;

            float lowPass = (float)SliderLowPass.Value;
            float highPass = (float)SliderHighPass.Value;
            float notch = 60.0f;
            if (RbNotch50Setting.IsChecked == true) notch = 50.0f;
            else if (RbNotchOffSetting.IsChecked == true) notch = 0.0f;

            var request = new Nnafa.State.V1.StateRequest
            {
                Settings = new Nnafa.State.V1.SessionSettings
                {
                    LowPassHz = lowPass,
                    HighPassHz = highPass,
                    NotchHz = notch
                }
            };

            try
            {
                await _telemetryClient.SendAsync(request.ToByteArray(), _cts.Token);
                MessageBox.Show($"Filter parameters successfully updated:\n• Low-Pass Limit: {lowPass:F0} Hz\n• High-Pass Limit: {highPass:F1} Hz\n• Notch Filter: {(notch > 0 ? $"{notch:F0} Hz" : "Disabled")}", "Settings Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send configuration: {ex.Message}", "Communication Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}