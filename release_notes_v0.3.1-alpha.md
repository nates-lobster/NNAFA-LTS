# Release Notes: v0.3.1-alpha

## Overview
This release finalizes the V0.3 core rebuild, introducing strict schema enforcement, new cognitive testing capabilities, and streamlined startup scripts. 

## New Features & Enhancements
- **Chimp Test Control Phase**: Added complete cognitive testing control phase with full DSP and UI integration. 
- **Telemetry & Validation**: Updated Protobuf schemas (`state_v1.proto`) for enhanced data validation between the Python Bridge and C# Frontend.
- **Simulation Scripts**: Added utility scripts (`add_brainwaves.py`, `modify.py`, `modify_lsl.py`) for simulating and modifying brainwave data and LSL streams during development.
- **Strict Spec Enforcement**: Updated system memory and architecture to enforce stateless processing, config-driven logic, and strict schema validation.
- **Run Script**: Renamed the versioned `run_v[version].bat` file to a stable `run.bat` to avoid naming issues in future updates. 

## Fixes & Refactoring
- Updated C# MainWindow and DSP logic to support new telemetry formats.
- Generated new data records (`cognitive_results_*.json`, `post_test_data_*.json`) for better debugging and analysis tracking.
