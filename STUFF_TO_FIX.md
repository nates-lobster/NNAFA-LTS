# Issues Fixed & Future Work

## Fixed in this release (V0.3.2)
- **LSL Stream fallback removal** – eliminated synthetic telemetry generation when the LSL stream drops frames.
- **Async loop responsiveness** – added `await asyncio.sleep(0.01); continue` when no data is received to prevent deadlock.
- **Documentation** – added this `STUFF_TO_FIX.md` file summarising fixes and pending items.
- **Zipping compatibility** – ensured `deploy.zip` contains `deploy/frontend` folder so `setup_env.bat` can unzip correctly.
- **BrainFlow LSL Bridge Pipeline** – Audited the BrainFlow LSL Bridge Pipeline.
- **C# WebSocket Stream** – Fixed C# WebSocket Stream issues.
- **Brainflow Data Stream** – Fixed Brainflow Data Stream connection indicator transitioning from "waiting" to streaming data upon connecting.

## Known issues / future improvements
- Validate large data handling on low‑memory devices.
- Add automated integration tests for the LSL connection manager.
- Explore optional compression for telemetry payloads.
- Review any remaining untracked JSON test output files.
