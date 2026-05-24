import sys

file_path = "MainWindow.xaml.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add fields
old_fields = """        private int _memoryLevel = 3;
        private int _memoryScore = 0;
        private int _memoryStrikes = 0;
        private bool _isGameRunning = false;
        private bool _isDisplayingSequence = false;
        private bool _isControlTrial = false;
        private string _sequenceString = "";"""
new_fields = """        private int _memoryLevel = 3;
        private int _memoryScore = 0;
        private int _memoryStrikes = 0;
        private bool _isGameRunning = false;
        private bool _isDisplayingSequence = false;
        private bool _isControlTrial = false;
        private string _sequenceString = "";
        private bool _isLevelLocked = false;
        private int _lockedTrialsCount = 0;
        private int _totalCalibrationTrials = 0;"""
content = content.replace(old_fields, new_fields)

# 2. Add IsSetDiff to TrialData
old_trial_data = """        public class TrialData
        {
            public int Level { get; set; }
            public bool IsControl { get; set; }
            public double EncodingStart { get; set; }"""
new_trial_data = """        public class TrialData
        {
            public int Level { get; set; }
            public bool IsControl { get; set; }
            public bool IsSetDiff { get; set; }
            public double EncodingStart { get; set; }"""
content = content.replace(old_trial_data, new_trial_data)

# 3. Modify ParseTrials
old_parse = """                        currentTrial = new TrialData
                        {
                            Level = level,
                            IsControl = ev.Text.Contains("spatial control"),
                            EncodingStart = ev.TimeInSeconds
                        };"""
new_parse = """                        currentTrial = new TrialData
                        {
                            Level = level,
                            IsControl = ev.Text.Contains("spatial control"),
                            IsSetDiff = ev.Text.Contains("set_diff: True"),
                            EncodingStart = ev.TimeInSeconds
                        };"""
content = content.replace(old_parse, new_parse)

# 4. Modify BtnStartMemory_Click
old_start = """            _isGameRunning = true;
            _memoryLevel = 3;
            _memoryScore = 0;
            _memoryStrikes = 0;
            _isControlTrial = true;"""
new_start = """            _isGameRunning = true;
            _memoryLevel = _selectedTaskType == 0 ? 3 : 8; // spatial starts at 8
            _memoryScore = 0;
            _memoryStrikes = 0;
            _isControlTrial = true;
            _isLevelLocked = false;
            _lockedTrialsCount = 0;
            _totalCalibrationTrials = 0;"""
content = content.replace(old_start, new_start)

# 5. Modify StartNewSpatialRoundAsync
old_spatial_start1 = """            if (_isControlTrial)
            {
                RecordCognitiveEvent($"Started presentation of spatial control set {_memoryLevel}", false);"""
new_spatial_start1 = """            if (_isControlTrial)
            {
                RecordCognitiveEvent($"Started presentation of spatial control set {_memoryLevel} (set_diff: {_isLevelLocked})", false);"""
content = content.replace(old_spatial_start1, new_spatial_start1)

old_spatial_start2 = """            else
            {
                RecordCognitiveEvent($"Started presentation of spatial set {_memoryLevel}", false);"""
new_spatial_start2 = """            else
            {
                RecordCognitiveEvent($"Started presentation of spatial set {_memoryLevel} (set_diff: {_isLevelLocked})", false);"""
content = content.replace(old_spatial_start2, new_spatial_start2)

# 6. Modify SpatialSquare_MouseDown (Control Correct)
old_control_correct = """                    _isControlTrial = false; // Transition to Test trial at the same level

                    TxtSpatialStatus.Text = "CORRECT!";"""
new_control_correct = """                    _isControlTrial = false; // Transition to Test trial at the same level
                    
                    if (_isLevelLocked) {
                        _lockedTrialsCount++;
                        if (_lockedTrialsCount >= 10) {
                            TxtSpatialStatus.Text = "CORRECT!";
                            TxtSpatialStatus.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#A6E3A1")!;
                            _ = Task.Delay(1500).ContinueWith(t => Dispatcher.Invoke(() => EndMemoryGame("10 locked trials completed!")));
                            return;
                        }
                    }

                    TxtSpatialStatus.Text = "CORRECT!";"""
content = content.replace(old_control_correct, new_control_correct)

# 7. Modify SpatialSquare_MouseDown (Test Correct)
old_test_correct = """                        _memoryScore += _memoryLevel * 10;
                        _isControlTrial = true; // Transition to Control trial and increment level
                        _memoryLevel++;"""
new_test_correct = """                        _memoryScore += _memoryLevel * 10;
                        _isControlTrial = true; // Transition to Control trial
                        _totalCalibrationTrials++;
                        if (!_isLevelLocked) {
                            _memoryLevel += 2; // user right -> add 2
                            if (_totalCalibrationTrials >= 10) {
                                _isLevelLocked = true;
                                _lockedTrialsCount = 0;
                            }
                        }"""
content = content.replace(old_test_correct, new_test_correct)

# 8. Modify SpatialSquare_MouseDown (Test Incorrect)
old_test_incorrect = """                    if (_memoryLevel > 3)
                    {
                        _memoryLevel--;
                    }

                    // Reset sequence to Control trial at the adapted level
                    _isControlTrial = true;"""
new_test_incorrect = """                    _totalCalibrationTrials++;
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
                    _isControlTrial = true;"""
content = content.replace(old_test_incorrect, new_test_incorrect)

# 9. JSON Export Modifications
old_export = """            var exportData = new
            {
                timestamp = DateTime.Now.ToString("o"),
                duration_seconds = (DateTime.Now - _challengeStartTime).TotalSeconds,
                task_type = _selectedTaskType == 0 ? "DigitSpan" : "SpatialSequence",
                end_message = endMessage,
                final_score = _memoryScore,
                max_level = _memoryLevel,
                trials = _postTestTrials
            };"""
new_export = """            string participantId = "";
            Dispatcher.Invoke(() => {
                participantId = TxtParticipantId.Text.Trim();
            });

            var exportData = new
            {
                participant_id = participantId,
                timestamp = DateTime.Now.ToString("o"),
                duration_seconds = (DateTime.Now - _challengeStartTime).TotalSeconds,
                task_type = _selectedTaskType == 0 ? "DigitSpan" : "SpatialSequence",
                end_message = endMessage,
                final_score = _memoryScore,
                max_level = _memoryLevel,
                trials = _postTestTrials.Select(t => new {
                    level = t.Level,
                    is_control = t.IsControl,
                    set_diff = t.IsSetDiff,
                    encoding_start = t.EncodingStart,
                    encoding_end = t.EncodingEnd,
                    recall_start = t.RecallStart,
                    presentation_times = t.PresentationTimes,
                    clicks = t.Clicks,
                    round_end = t.RoundEnd
                }).ToList()
            };"""
content = content.replace(old_export, new_export)

# 10. Post-test Plot colors
old_plot_control = """                if (controlTrials.Count > 0)
                {
                    var scatterControl = WpfPlotPostTestAnalysis.Plot.Add.Scatter(xsControl.ToArray(), ysControl.ToArray());
                    scatterControl.Label = "Control Accuracy";
                    scatterControl.Color = Color.FromHex("#A6E3A1");"""
new_plot_control = """                if (controlTrials.Count > 0)
                {
                    var scatterControl = WpfPlotPostTestAnalysis.Plot.Add.Scatter(xsControl.ToArray(), ysControl.ToArray());
                    scatterControl.Label = "Control Accuracy";
                    scatterControl.Color = Color.FromHex("#94E2D5");"""
content = content.replace(old_plot_control, new_plot_control)

old_plot_test = """                if (testTrials.Count > 0)
                {
                    var scatterTest = WpfPlotPostTestAnalysis.Plot.Add.Scatter(xsTest.ToArray(), ysTest.ToArray());
                    scatterTest.Label = "Test Accuracy";
                    scatterTest.Color = Color.FromHex("#89B4FA");"""
new_plot_test = """                if (testTrials.Count > 0)
                {
                    var scatterTest = WpfPlotPostTestAnalysis.Plot.Add.Scatter(xsTest.ToArray(), ysTest.ToArray());
                    scatterTest.Label = "Test Accuracy";
                    scatterTest.Color = Color.FromHex("#F5A623");"""
content = content.replace(old_plot_test, new_plot_test)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("done")
