# Project Memory: NeuroMemoryStudy

## Long-Term Memory (Persistent Rules & Setup)
* **Hardware:** Muse 2/S Headset via BlueMuse (LSL). 256Hz sampling. 4 Channels (TP9, AF7, AF8, TP10).
* **Frequencies:** Bandpass 1Hz - 40Hz. 60Hz Notch integrated into IIR/FIR.
* **DSP Pipeline:** Stage-based processing (Raw -> Notch -> FIR -> IIR). FIR is 129-tap Hamming window.
* **IPC:** WebSocket on Port 8765. Protobuf v2 expanded for per-channel PSDs and intermediate debug signals.
* **UI Improvements:** 
  - Expanded Debug Pipeline (3x2 grid) showing all DSP stages.
  - Added "Debug Overlay" tab for multi-stage signal comparison with layer toggles.
  - Fixed ScottPlot 5 obsolescence warnings (LegendText migration).
  - Added dynamic Alpha/Beta smoothing slider (0.5s to 10s).
  - Implemented C# MediaPlayer-based audio feedback loop.
* **Reliability:** Added 2-second connection watchdog to detect LSL/BlueMuse freezes. Increased artifact threshold to 150uV.
* **Calibration:** Added 30-second resting baseline calibration to calculate target ratios.
* **Software Setup:** Installed R, RStudio, and Rtools to `M:\Muse Project\Software\` for research-grade data analysis.
* **Data Flow:** Verified scrolling EEG waves and live FFT updates are functioning at ~10Hz refresh.
* **Brainflow Integration:** Added `brainflow_lsl_bridge.py` as a robust alternative to BlueMuse. Supports Native BLE, BLED112, and Synthetic Emulator across Muse 2, S, and 2016.
* **Asynchronous Connection Manager:** Ported Python server's LSL resolution to an asynchronous background worker thread, eliminating Event Loop blockages and preventing WebSocket command latencies.

## Short-Term Memory (Current Task State)
* **Status:** System upgraded to V0.2.2 with non-blocking connectivity, live status logs, and robust multi-device support.
* **Non-blocking LSL:** Complete asynchronous self-healing loop resolves LSL streams in a background thread, preventing thread-blocking freezes.
* **Multi-Device Support:** UI now offers full Board ID mappings for Muse 2, S, and 2016 (Native BLE and BLED112).
* **Synthetic Emulator:** Added a hardware-free "Synthetic (Emulator)" provider that tests the entire ingestion, bridge, DSP, and C# visualization stack out of the box.
* **Process Redirection:** WPF now captures and displays standard error and stdout of the Python bridge process directly in the UI status log, preventing silent startup failures.
* **Replay & Test Hygiene:** Reinstalled and validated clean `brainflow` packages. Verified 0 compilation errors.


