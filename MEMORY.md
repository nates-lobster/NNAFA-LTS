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

## Short-Term Memory (Current Task State)
* **Status:** System upgraded to V0.2.1 with FIR and Neurofeedback features on `neurofeedback` branch.
* **FIR Denoise:** 129-tap linear phase filter successfully integrated into the bridge.
* **Neurofeedback:** Audio volume loop and calibration system functional.
* **Stall Detection:** Real-time "STALLED" indicator for Bluetooth/LSL drops.
* **Known Issues:** 
    *   BlueMuse/LSL can freeze; watchdog implemented to detect and alert.
    *   WPF SkiaSharp warnings (NU1701) persist but are non-breaking.
* **Planned:** Merge `neurofeedback` into `main` tomorrow.

