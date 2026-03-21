# VSCode for Signals: 2-Week Build Plan

## Planning Frame
- Start date: 2026-03-23
- Duration: 2 weeks
- Goal: deliver a usable vertical slice of VSCode for Signals aligned with the PRD
- Build target at end of week 2: a user can import a sample signal, visualize it, run a reusable pipeline, inspect intermediate outputs, ask AI about a selected range, and export a summary

## Delivery Strategy
The first two weeks should not try to finish the whole product. They should establish the architecture and prove the core loop end to end:

1. Import signal
2. Visualize immediately
3. Apply transformation pipeline
4. Inspect intermediate states
5. Ask AI grounded in current context
6. Save and export the result

## Scope for the 2-Week Increment
### Must ship
- Monorepo or clearly separated frontend/backend repo structure
- .NET backend modular monolith with feature slices
- React frontend with Feature-Sliced Architecture
- CSV import flow
- Signal explorer and chart canvas
- Pipeline builder with a small transform set
- Pipeline execution trace
- AI assistant backed by external LLM adapter
- Workspace persistence for one saved session format
- Markdown report export

### Nice to have
- JSON time-series import
- Annotations and bookmarks
- Basic autosave

### Explicitly out of scope
- Real-time hardware streaming
- Multi-user collaboration
- Enterprise auth and RBAC
- Plugin marketplace

## Recommended Repository Shape
```text
/
  docs/
  backend/
    src/
    tests/
  frontend/
    src/
    tests/
```

## Architecture Decisions To Lock In During Week 1
### Backend
- ASP.NET Core Web API on .NET
- Feature folders such as `Importer`, `Signals`, `Pipelines`, `Workspaces`, `AiAssistant`, `Reports`
- Inside each feature: `Command`, `Endpoint`, `Handler`, `Response`
- Canonical signal model for imported datasets
- Background execution abstraction even if first implementation runs inline

### Frontend
- React + TypeScript
- FSA layers: `app`, `pages`, `widgets`, `features`, `entities`, `shared`
- Query layer for server state
- Shared shell layout for explorer, main canvas, inspector, bottom trace panel

### AI
- Server-side provider abstraction
- Prompt templates plus explicit context builders
- Persist prompt, response, provider metadata, and context snapshot reference

## Week 1
### Day 1: Foundation and repo bootstrap
- Create repository structure for frontend, backend, docs, and tests.
- Scaffold ASP.NET Core backend and React frontend.
- Add formatting, linting, testing, and editor config baselines.
- Define architecture decision record for FSA conventions on both sides.

Deliverables:
- Running frontend shell
- Running backend API host
- Shared README with local run instructions

### Day 2: Backend slice skeleton and core contracts
- Implement backend feature folders and composition root, starting with `Features/Importer/{Command,Endpoint,Handler,Response}`.
- Define core domain contracts: `Workspace`, `SignalAsset`, `SignalChannel`, `PipelineDefinition`, `PipelineRun`.
- Add API contract DTOs for signal import, workspace load, pipeline run, and AI message.
- Add persistence abstraction interfaces.

Deliverables:
- Compiling backend slice structure
- Initial domain and contract models
- Smoke tests for API startup

### Day 3: Frontend shell and workspace layout
- Implement app shell with IDE-like panel layout.
- Build stub widgets for explorer, chart panel, inspector, AI panel, and trace panel.
- Wire FSA boundaries for `signal`, `workspace`, `pipeline`, and `ai-session` entities.
- Add routing and base design tokens.

Deliverables:
- Navigable frontend shell
- Placeholder workspace screen with resizable panel structure

### Day 4: Signal import vertical slice
- Implement CSV upload endpoint and parser on backend.
- Normalize uploaded CSV into canonical signal/channel structure.
- Add frontend import flow and signal list rendering.
- Return parsed metadata and sample channel previews.

Deliverables:
- User can upload a CSV and see detected channels
- Import validation errors shown clearly

### Day 5: First visualization
- Implement chart widget for single and multi-channel rendering.
- Support zoom, pan, hover, and region selection.
- Add endpoint or contract for fetching plot-ready signal data.
- Connect selected range state to inspector.

Deliverables:
- Interactive chart view backed by imported data
- Selection model available in UI state

## Week 2
### Day 6: Pipeline domain and execution engine
- Implement `PipelineDefinition`, `PipelineStep`, `PipelineRun`, and `PipelineStepRun`.
- Add transformation step framework with typed parameters.
- Ship first steps: normalize, moving average, low-pass filter, resample, FFT.
- Add run API and basic execution trace model.

Deliverables:
- Backend can execute a pipeline against imported signal data
- Trace data returned per step

### Day 7: Pipeline builder UI
- Implement pipeline list, step editor, parameter forms, and run action.
- Show final output plus intermediate outputs in trace panel.
- Add rerun flow when parameters change.

Deliverables:
- User can define and run a pipeline from the frontend
- Step-by-step outputs are inspectable

### Day 8: AI assistant integration
- Implement provider abstraction and initial external LLM adapter.
- Build context assembler from active signal, selected range, pipeline definition, and trace summary.
- Add AI panel with message history and visible context preview.
- Add safeguards for prompt size and failed provider responses.

Deliverables:
- User can ask AI about current analysis context
- Prompt metadata and context reference stored server-side

### Day 9: Workspace persistence and export
- Implement save/load workspace endpoints and persistence model.
- Persist open signal references, pipeline definitions, active selection, and AI thread metadata.
- Add Markdown export for investigation summary and chart snapshot hooks.

Deliverables:
- Workspace can be saved and reloaded
- Basic Markdown export works

### Day 10: Hardening, tests, and demo prep
- Add integration tests across import, pipeline, and AI orchestration.
- Add frontend tests for import, chart rendering, and pipeline run flow.
- Improve errors, loading states, and empty states.
- Prepare seeded demo data and a scripted walkthrough.

Deliverables:
- Demo-ready vertical slice
- Known issues list and next-step backlog

## Parallel Workstreams
### Backend track
- Domain modeling
- Import and normalization
- Pipeline runtime
- AI orchestration
- Persistence and export

### Frontend track
- Workspace shell
- Explorer and chart canvas
- Pipeline builder
- AI panel
- Save/export interactions

### Cross-cutting track
- Contracts
- Observability
- Testing
- Sample datasets
- Developer experience

## Definition of Done for End of Week 2
- A user can import a CSV signal capture and see it rendered.
- A user can select a signal range and inspect metadata.
- A user can build and run a pipeline with at least 5 transformation steps.
- The system shows intermediate outputs and execution trace per step.
- A user can ask AI to explain the selected region or suggest the next step.
- The workspace can be saved and loaded.
- A report can be exported as Markdown.

## Primary Risks During the First 2 Weeks
- Charting performance may degrade if the first renderer is not designed for large datasets.
- CSV normalization may become messy if schemas vary too widely.
- AI context payloads can balloon unless selection summaries are constrained.
- Overengineering FSA can slow delivery if slice boundaries are debated too long.

## Mitigations
- Use a canonical signal model early and reject ambiguous imports with explicit errors.
- Keep the first charting path narrow and optimize only the hot path.
- Cap AI context to active selection plus concise pipeline summaries.
- Favor a pragmatic FSA rule: feature-first boundaries, shared only when reuse is real.

## Expected Outputs After 2 Weeks
- A runnable full-stack prototype
- A documented codebase structure that can scale without rewrite
- A validated product loop for signal analysis plus AI assistance
- Clear backlog for weeks 3 through 6

## Immediate Backlog for Weeks 3 Through 6
- JSON import and richer schema mapping
- Annotations and bookmarks
- Pipeline library and versioning improvements
- Better chart comparison modes
- AI prompt presets and report templates
- Authentication and multi-tenant groundwork if required
