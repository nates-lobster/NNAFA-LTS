# GitHub Release Templates: NNAFA-LTS

Use these templates to create formal releases on GitHub under the **Releases** tab.

---

## 🏷️ Release v0.2.0
**Tag:** `v0.2.0`  
**Target:** `neurofeedback`  
**Release Title:** `v0.2.0: Brainflow LSL Bridge & Async Connection Manager`

### Description
This release introduces the **Brainflow LSL Ingestion Bridge** as a robust, native alternative to BlueMuse, alongside a completely rewritten asynchronous, non-blocking telemetry engine in Python to prevent event loop stalls.

### 🚀 Key Features & DSP Upgrades
*   **Brainflow LSL Bridge:** Added `brainflow_lsl_bridge.py` allowing native Bluetooth Low Energy (BLE) and Silicon Labs BLED112 virtual COM port connections.
*   **Asynchronous Connection Manager:** Ported Python server's LSL resolution to an asynchronous background worker thread. Resolving missing stream sources no longer blocks the `asyncio` event loop or disrupts live WebSockets.
*   **Continuous Telemetry Heartbeat:** Added constant 20Hz heartbeat packets carrying stream stalled/connected integrity metrics directly to the WPF UI.
*   **Multi-Model Hardware Support:** Implemented a new dropdown selector in WPF to choose Muse S, Muse 2, and Muse 2016 models, automatically mapping correct hardware Board IDs on initialization.
*   **Live Exception/Status Logs:** Redirected Python standard error logs directly to the WPF status bar to display tracebacks and connection warnings instead of silent failures.

---

## 🏷️ Release v0.2.1
**Tag:** `v0.2.1`  
**Target:** `neurofeedback`  
**Release Title:** `v0.2.1: Portable Environment Setup & Line-Buffered Logging`

### Description
This release makes the NNAFA-LTS developer and user pipeline **completely portable**. It introduces automated environment setups, eliminates all hardcoded folder structures in dynamic C# execution, and fixes buffered subprocess pipelining to allow immediate synthetic emulation.

### 🚀 Key Enhancements
*   **One-Click Setup (`setup_env.bat`):** Added a fully automated batch script that checks local Python installations, initializes the `venv` virtual environment, installs all python package manifests, and runs an initial C# `dotnet build`.
*   **Dynamic Directory Resolution:** Removed all brittle relative parent jumps (`..\\..\\..\\..\\..`) in C#. Replaced them with a recursive directory tree crawler `FindProjectRoot` that crawls parent directories dynamically.
*   **Synthetic Emulation Out-of-the-Box:** Fixed the emulator/synthetic stream mode (Board ID `-1`), letting developers test the entire ingestion, bridge, filtering, and C# visualization pipeline with zero physical hardware or Bluetooth dongles.
*   **Line-Buffered Real-time Output:** Enabled unbuffered logging in Python subprocesses (`-u` flag and `line_buffering` reconfigurations), solving pipe buffering lag so that connection status and errors are updated on the WPF interface in real-time.
