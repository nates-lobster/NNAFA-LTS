# V0.2 Progress Tracker (vs V0.1)

This document tracks the re-implementation of V0.1 features in the strict architecture of V0.2.

## Milestone 1: The Foundation
- [x] Establish Strict Directory Structure
- [x] Define Agent Roles (`coder.md`, `debugger.md`, `auditor.md`)
- [x] Create initial IPC Schemas (Protobuf)
- [x] Initialize C# WPF Frontend (.NET 10.0)
- [x] Initialize Git Repository and link to GitHub (NNAFA-LTS)

## Milestone 2: Python Backend (The Worker)
- [x] Port LSL Ingestion (256Hz, 4 Channels) to `src/01_ingestion/`
- [x] Port DSP Pipeline (60Hz Notch, 1-40Hz Bandpass, 2s Ring Buffer, `scipy.signal.welch`) to `src/02_processing/`
- [x] Port `np.trapezoid` Band Power Extraction to `src/02_processing/`
- [x] Set up WebSocket Server (Port 8765) in `src/03_bridge/` matching Schema
- [x] Port audio engine (Rain/Bird chirps) based on Pygame mixer (stubbed)
- [x] Fix: "Orphaned Python process" issue during shutdown

## Milestone 3: C# Frontend (The General)
- [x] Subscribe to WebSocket telemetry (Port 8765)
- [x] Implement `SemaphoreSlim` for threading safe operations
- [x] Rebuild "Monitoring" Tab (Raw EEG + Integrity Lights R/Y/G)
- [x] Rebuild "Neurofeedback" Tab (Live Alpha/Beta ratio indicator)
- [x] Rebuild "Research" Tab (Participant Metadata + LabRecorder TCP Control Port 22345)
- [x] Add "Kill All Python" Button
- [x] Fix: "Disappearing plot" issue by locking axes to -150/+150 uV
- [x] Fix: Startup crashes by using `IsLoaded` checks
