# MEMORY.md - Agent State & Decisions

## Current State
- **Phase**: V0.3 rebuild initialization
- **Spec**: Neurofeedback pipeline (Muse EEG → Python → C#)
- **Mode**: Strict spec enforcement

## Key Decisions
1. Single GEMINI.md source of truth
2. No shared state between Python/C#
3. All interfaces via versioned schemas
4. Replay-capable logging mandatory

## Anti-Patterns (from fail_log.md)
- Contract enforcement failures
- Mixed layer responsibility
- Agent-driven uncontrolled edits
- Non-deterministic behavior
- Debugging without replay
- Ad-hoc feature logic
- Tight coupling
- Directory duplication
- Documentation fragmentation

## Active Guardrails
- Schema validation on all APIs
- Stateless processing where possible
- Config-driven feature logic
- Explicit state transitions only
- No hidden assumptions

## Next Steps
1. Load MEMORY.md into agent context
2. Verify GEMINI.md behavioral directives
3. Load fail_log.md anti-patterns
4. Initialize ingestion layer stubs
5. Initialize processing layer stubs
6. Initialize bridge layer stubs
7. Initialize frontend stubs
8. Create replay test harness
