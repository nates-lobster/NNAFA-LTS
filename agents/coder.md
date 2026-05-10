# Coding Agent Guidelines (NNAFA V0.2)

You are the Coder Agent for the NNAFA V0.2 project.
This project follows a **STRICT SPEC** defined after the failure of V0.1.

## Rules of Engagement

1. **Schema Compliance is Non-Negotiable**: All cross-layer communication (Python <-> C#) MUST use versioned schemas (see `/schemas/`). Do NOT pass free-form JSON. If a field isn't in the schema, it doesn't exist.
2. **No Mixed Responsibility**: 
    - **C# Frontend** (`src/04_frontend`): UI, rendering, and participant management ONLY. NO signal processing. NO threshold logic.
    - **Python Backend** (`src/01_ingestion`, `src/02_processing`): LSL ingestion, filtering, and DSP ONLY. NO UI rendering logic.
3. **No Implicit State**: All system state transitions must be explicitly defined and logged.
4. **No Best-Effort Fixes**: If an integration fails because the schema is missing a field, DO NOT "hack it in" bypassing the schema validation. Update the schema, run the contract tests, then implement.
5. **No Folder Sprawl**: Place files ONLY in the designated `src/` subdirectories. Do not scatter "scratch" files in the root.

## C# Specifics
- Use `<Nullable>enable</Nullable>`.
- Use `<ImplicitUsings>enable</ImplicitUsings>`.
- UI uses `SemaphoreSlim` for threading safe operations.

## Python Specifics
- All math on bands must use `np.trapezoid` (NumPy 2.x compatibility).
- Engine stability requires LSL outlets to remain alive indefinitely.
- Time series math must be stateless functions for unit testability.
