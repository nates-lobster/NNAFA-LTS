import sys
import re

file_path = "MainWindow.xaml.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Add _current fields
fields_code = """
        // Working Memory Challenge State
        private double _currentDelta = 0;
        private double _currentTheta = 0;
        private double _currentAlpha = 0;
        private double _currentBeta = 0;
        private double _currentGamma = 0;
"""
content = content.replace("        // Working Memory Challenge State", fields_code + "        // Working Memory Challenge State")

# Add to CognitiveEvent
old_cog = """        public class CognitiveEvent
        {
            public double TimeInSeconds { get; set; }
            public string Text { get; set; }
            public bool IsUserEvent { get; set; }
        }"""
new_cog = """        public class CognitiveEvent
        {
            public double TimeInSeconds { get; set; }
            public string Text { get; set; }
            public bool IsUserEvent { get; set; }
            public double DeltaPower { get; set; }
            public double ThetaPower { get; set; }
            public double AlphaPower { get; set; }
            public double BetaPower { get; set; }
            public double GammaPower { get; set; }
        }"""
content = content.replace(old_cog, new_cog)

# Add to HandleTelemetryData
old_handle = """                TxtAlpha.Text = payload.BandPower.Alpha.ToString("F1");
                TxtBeta.Text = payload.BandPower.Beta.ToString("F1");
                TxtGamma.Text = payload.BandPower.Gamma.ToString("F1");"""
new_handle = """                TxtAlpha.Text = payload.BandPower.Alpha.ToString("F1");
                TxtBeta.Text = payload.BandPower.Beta.ToString("F1");
                TxtGamma.Text = payload.BandPower.Gamma.ToString("F1");
                _currentDelta = payload.BandPower.Delta;
                _currentTheta = payload.BandPower.Theta;
                _currentAlpha = payload.BandPower.Alpha;
                _currentBeta = payload.BandPower.Beta;
                _currentGamma = payload.BandPower.Gamma;"""
content = content.replace(old_handle, new_handle)

# Add to RecordCognitiveEvent
old_record = """                    TimeInSeconds = relativeSeconds,
                    Text = description,
                    IsUserEvent = isUserEvent
                });"""
new_record = """                    TimeInSeconds = relativeSeconds,
                    Text = description,
                    IsUserEvent = isUserEvent,
                    DeltaPower = _currentDelta,
                    ThetaPower = _currentTheta,
                    AlphaPower = _currentAlpha,
                    BetaPower = _currentBeta,
                    GammaPower = _currentGamma
                });"""
content = content.replace(old_record, new_record)

# Add to ExportChallengeResults
old_export = """                            timestamp_seconds = Math.Round(ev.TimeInSeconds, 2),
                            event_description = ev.Text,
                            type = ev.IsUserEvent ? "User" : "System"
                        });"""
new_export = """                            timestamp_seconds = Math.Round(ev.TimeInSeconds, 2),
                            event_description = ev.Text,
                            type = ev.IsUserEvent ? "User" : "System",
                            delta_power = Math.Round(ev.DeltaPower, 2),
                            theta_power = Math.Round(ev.ThetaPower, 2),
                            alpha_power = Math.Round(ev.AlphaPower, 2),
                            beta_power = Math.Round(ev.BetaPower, 2),
                            gamma_power = Math.Round(ev.GammaPower, 2)
                        });"""
content = content.replace(old_export, new_export)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("done brainwaves")
