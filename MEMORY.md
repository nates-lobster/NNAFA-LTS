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

## Short-Term Memory (Task State & Upgrades)
* **Status:** Upgraded backend to V0.2.5 with calibration fixes, stdout status flush configuration, and native Brainflow logging redirection.
* **Bug Fixes:**
  - Resolved `brainflow_lsl_bridge.py` zero-sample drop by replacing `data.any()` with `data.shape[1] > 0`.
  - Prevented Bluetooth connection locks by catching system signals (`SIGINT`, `SIGTERM`, `SIGBREAK`) for graceful session release.
  - Hardened LSL receiver stream resolution in `lsl_stream.py` to target named `"Muse"` streams, falling back to type `"EEG"`.
  - Preserved precise LSL hardware timestamps, eliminating processing jitter.
  - **Fixed UI Native BLE Error Bug:** Configured `BoardShim.set_log_file("brainflow_native.log")` in `brainflow_lsl_bridge.py` to redirect all native Brainflow C++ trace logs away from `sys.stderr`, preventing the C# frontend from incorrectly intercepting them as fatal connection errors.
  - **Fixed UI Status Buffer Bug:** Re-added line-buffering to Python stdout/stderr (`sys.stdout.reconfigure(line_buffering=True)`) and forced `flush=True` on all printed status lines to ensure they are received instantly by the C# UI pipe, resolving the frozen "Initializing" indicator.
  - **Fixed Neurofeedback Target Calibration Bug:** Fixed the issue where target ratio was constantly locked to `0.0` in the C# frontend by assigning the calculated `TARGET_RATIO` to `payload.target_ratio` in Python `server.py` before broadcasting the telemetry payload.
* **Developer Log Hygiene & Archiver:**
  - Configured `server.py` to launch with `mode='w'`, automatically truncating and clearing the log file on every fresh application startup to keep logs short and token-friendly.
  - Bridge uses `mode='a'` to append seamlessly without overwriting server logs.
  - Created `scratch/manage_logs.py` allowing developers to manually status-check, clear, or archive log files to a timestamped roll (`logs/archive/`) at any time.




