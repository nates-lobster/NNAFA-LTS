# Project Context: NNAFA-LTS (NeuroAnalysis & Feedback App)

**CRITICAL STARTUP RULE:** Always read [MEMORY.md](./MEMORY.md) at the start of every chat to load Long-Term and Short-Term persistent project facts.

**AGENT ORCHESTRATION:**
- **Before IMPLEMENTING:** Read [coder.md](./agents/coder.md)
- **Before FIXING:** Read [debugger.md](./agents/debugger.md)
- **Before COMMITTING/WRAPPING:** Read [auditor.md](./agents/auditor.md)

**DOCUMENTATION RULE:** Upon request, find `../Documentation/` and create a subfolder for the current day formatted as `MM-DD`. Inside, create/edit `CLI.md` summarizing the session's work.

## 1. System Architecture
* **Frontend (src/04_frontend/):** C# WPF (.NET 10.0) - "The General". Primary UI and participant management.
* **Backend (src/01-03/):** Python 3.11+ - "The Worker". Modularized: Ingestion, Processing, Bridge.
* **IPC Bridge:** WebSockets / **Protobuf** (Port 8765). Python broadcasts telemetry; C# subscribes.
* **Hardware:** Muse 2/S Headset via BlueMuse (LSL).

## 2. Technical Constraints (STRICT V0.2)
* **Sampling Rate:** 256Hz.
* **Filtering:** 60Hz Notch and 1Hz–40Hz Bandpass applied in `src/02_processing/`.
* **FFT Method:** `scipy.signal.welch` (2-second rolling ring buffer).
* **Artifact Rejection:** Peak-to-peak amplitude gating (>100uV) on AF7/AF8.
* **C# Conventions:** <Nullable>enable</Nullable> and <ImplicitUsings>enable</ImplicitUsings>.
* **Python Conventions:** Use `np.trapezoid` (NumPy 2.x) for band power.

## 3. Core Modules
1. **01_ingestion:** pylsl Stream Listener -> 2s Ring Buffer.
2. **02_processing:** Stateless DSP (FFT -> Band Power -> Ratio Logic).
3. **03_bridge:** WebSocket Server / Protobuf Serialization.
4. **04_frontend:** Visualization (Raw EEG + Integrity Lights + NFB Dashboard).

## 4. Operational Commands
* **Build C#:** `dotnet build src/04_frontend/Frontend.csproj`.
* **Run App (Full Stack):** `.\run_v0.2.bat`.
* **Run Backend Only:** `python src/03_bridge/server.py`.
* **Git Sync:** `git push origin main`.