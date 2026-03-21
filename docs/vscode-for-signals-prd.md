# VSCode for Signals PRD

## Document Control
- Product: VSCode for Signals
- Status: Draft v1
- Date: 2026-03-21
- Intended audience: Product, design, frontend, backend, AI platform, QA

## 1. Product Summary
VSCode for Signals is a modern, IDE-like workspace for exploring, debugging, and analyzing signals. It gives engineers and researchers a fast feedback loop for loading signal data, visualizing it instantly, building reusable processing pipelines, inspecting intermediate states, and using AI to reason about anomalies, patterns, and next steps.

The product is not an LLM itself. It is an orchestration layer on top of external LLM providers, grounding AI responses in the active workspace context: signal metadata, selected ranges, pipeline outputs, annotations, logs, and user intent.

## 2. Vision
Create the default workspace for signal intelligence work: a tool that feels as fluid for signal analysis as VS Code feels for software development.

## 3. Problem Statement
Signal analysis workflows are fragmented across notebooks, custom scripts, vendor tools, dashboards, and ad hoc chats with AI systems that lack context about the actual data under investigation.

Current pain points:
- Slow iteration between data loading, processing, and visualization.
- Poor visibility into how signal transformations change the result at each step.
- Low reuse of analysis logic across projects and teammates.
- Hard to debug pipeline errors or understand why a derived metric changed.
- AI tools can suggest ideas, but they are usually detached from the active workspace and cannot inspect the user's actual analysis context.

## 4. Product Goals
### Primary goals
- Make signal exploration feel immediate: load data and see it within seconds.
- Make analysis repeatable through reusable, composable pipelines.
- Make debugging first-class by exposing intermediate pipeline states and execution traces.
- Make AI useful by grounding it in workspace state and signal context.
- Support a clear, scalable implementation path using React on the frontend and C# .NET on the backend with feature-sliced organization on both sides.

### Business goals
- Reduce time-to-insight for common signal investigation tasks.
- Increase reuse of analysis pipelines instead of one-off scripts.
- Create a foundation for premium capabilities such as team collaboration, audit trails, domain packs, and provider-specific AI workflows.

## 5. Non-Goals
- Training or fine-tuning a proprietary foundation model.
- Replacing Python, MATLAB, or domain-specific toolchains for every advanced use case.
- Building a fully collaborative multi-user editing system in the MVP.
- Supporting every file format and every signal modality in v1.
- Shipping on-device, real-time hardware acquisition in the first release.

## 6. Target Users
### Primary users
- Signal processing engineers
- Embedded systems engineers
- Data scientists working with time-series data
- Research engineers investigating waveforms, telemetry, or sensor streams
- QA and support engineers debugging device behavior from recorded traces

### Secondary users
- Technical product teams reviewing customer signal captures
- Operations teams analyzing machine or system telemetry
- Educators and advanced students working with signal analysis workflows

## 7. Jobs To Be Done
- When I receive a new signal capture, I want to inspect and visualize it immediately so I can decide what matters.
- When I build a transformation pipeline, I want to save and reuse it so future investigations start faster.
- When a signal looks wrong, I want to step through the pipeline and inspect intermediate outputs so I can isolate the issue.
- When I ask AI for help, I want answers grounded in the current signal, selected time range, and analysis artifacts so the response is actionable.
- When I finish an investigation, I want to export findings, annotations, and pipeline context so the work is reproducible.

## 8. Product Principles
- Instant feedback over batch-first workflows.
- Visible state over hidden automation.
- Reusable analysis over throwaway scripting.
- AI as a copilot, not a black box.
- Modular architecture over framework sprawl.

## 9. MVP Scope
### In scope for MVP
- Local and cloud-ready web application with IDE-style layout
- Import of common signal data formats such as CSV and JSON time-series
- Signal explorer with metadata, channel listing, and preview
- Multi-panel visualization with zoom, pan, overlays, and selections
- Reusable pipeline builder for a small set of transformations
- Pipeline execution trace with intermediate outputs
- Annotation and bookmarks on signal regions
- AI chat panel grounded in selected signals, ranges, annotations, and pipeline state
- Saved workspaces and saved pipelines
- Export of charts, pipeline definitions, and investigation summaries

### Deferred after MVP
- Real-time acquisition from hardware devices
- Shared multi-user workspaces
- Plugin marketplace
- Fine-grained role-based access control
- Advanced domain modules such as RF-specific decoders, biomedical packs, or vibration diagnostics packs
- Native desktop wrapper

## 10. Core Product Experience
The product should feel like a signal-native IDE:
- Left rail: workspace explorer, files, saved pipelines, bookmarks
- Center: tabbed signal canvases, synchronized visualizations, inspectors
- Right rail: AI chat, metadata, parameter controls, annotations
- Bottom panel: pipeline trace, logs, execution console, diagnostics

Primary interaction loop:
1. Load a signal file or open a saved workspace.
2. Inspect channels and render the signal instantly.
3. Select a time range or channel subset.
4. Apply transformations through a reusable pipeline.
5. Inspect intermediate outputs and compare before/after views.
6. Ask AI to explain anomalies, propose next analysis steps, or summarize findings.
7. Save the workspace, export outputs, or persist the pipeline for reuse.

## 11. Key User Stories
### Signal ingestion
- As a user, I can import one or more signal files and see detected channels and metadata.
- As a user, I can reopen recently used files and workspaces.

### Visualization
- As a user, I can view one or more signals in synchronized charts.
- As a user, I can zoom, pan, brush-select a time region, and inspect exact values.
- As a user, I can overlay derived signals on original signals.

### Pipelines
- As a user, I can assemble transformations such as normalize, filter, resample, smooth, FFT, and segment.
- As a user, I can save a pipeline with parameters and rerun it on a new dataset.
- As a user, I can inspect each pipeline step's input, output, and execution metadata.

### Debugging
- As a user, I can identify which pipeline step changed the signal unexpectedly.
- As a user, I can view errors, warnings, and data-quality checks tied to specific steps.

### AI assistance
- As a user, I can ask questions about the active signal or selected range.
- As a user, I can ask AI to explain anomalies, suggest a next transformation, or summarize a finding.
- As a user, I can see what context was sent to the AI so the interaction stays inspectable and trustworthy.

### Persistence and reporting
- As a user, I can save a workspace session, including open tabs, selections, annotations, and pipelines.
- As a user, I can export a report that includes charts, findings, and the pipeline used.

## 12. Functional Requirements
### 12.1 Workspace Management
- Users can create, save, load, duplicate, and delete workspaces.
- A workspace stores:
  - Imported signal references
  - Open views and layout state
  - Active selections
  - Annotations and bookmarks
  - Saved pipelines and pipeline runs
  - AI conversation history linked to context snapshots

### 12.2 Signal Import and Normalization
- System supports CSV and JSON time-series in MVP.
- Import flow validates schema, timestamps or sample indices, numeric columns, and missing values.
- System maps imported data into a canonical signal model.
- System preserves source metadata and import warnings.

### 12.3 Visualization Engine
- First meaningful chart render should occur within 2 seconds for standard MVP datasets.
- Frontend visualization implementation uses Apache ECharts as the primary charting engine for MVP.
- Charts support:
  - Single and multi-channel views
  - Overlays
  - Derived series
  - Cursor and range selection
  - Annotations and bookmarks
  - Downsampled rendering for large data
- Selection state is shared across chart, inspector, pipeline, and AI panel.

### 12.4 Pipeline Builder and Runtime
- Users can build ordered pipelines from predefined transformations.
- Each transformation exposes typed parameters and validation.
- Supported MVP steps:
  - Normalize
  - Moving average / smoothing
  - High-pass or low-pass filter
  - Resample
  - Segment
  - FFT
- Pipeline runs produce:
  - Final outputs
  - Intermediate outputs
  - Step execution timing
  - Step logs, warnings, and failures
- Pipelines are versioned and reusable across workspaces.

### 12.5 Debugging and Inspection
- Users can inspect pipeline input/output for every step.
- Users can compare original vs transformed signals side by side.
- Users can view a structured execution trace with timing and errors.
- Users can replay a previous successful pipeline run.

### 12.6 AI-Assisted Reasoning
- AI is offered through a provider abstraction layer with support for external models.
- AI requests are grounded with:
  - Signal metadata
  - Selected range or channel
  - Pipeline definition
  - Relevant intermediate outputs
  - User annotations and bookmarks
- AI must not silently access unrelated workspace data.
- Users can trigger common prompts:
  - Explain anomaly
  - Suggest next analysis step
  - Summarize selected region
  - Generate report draft
- Every AI response stores:
  - Prompt template used
  - Context snapshot reference
  - Provider and model metadata
  - Token and latency metrics where available

### 12.7 Export and Reporting
- Users can export:
  - PNG/SVG chart snapshots
  - Pipeline JSON definitions
  - Markdown investigation summaries
- Exported summaries include traceable links to the underlying pipeline and data selection.

## 13. Non-Functional Requirements
### Performance
- Initial workspace shell load under 3 seconds on a standard developer laptop.
- Render interaction latency under 100 ms for common zoom and pan operations on MVP dataset sizes.
- Pipeline execution status updates streamed to the client.

### Reliability
- Failed pipeline steps must fail visibly and preserve prior successful outputs.
- Workspace autosave protects against accidental browser refresh or crash.

### Security
- API keys for LLM providers are stored server-side and never exposed directly to the browser.
- AI context payloads are explicitly assembled and auditable.
- File upload constraints and size limits are enforced server-side.

### Observability
- Backend emits structured logs, traces, and metrics for imports, pipeline runs, chart data requests, and AI invocations.
- Frontend captures UI errors and slow interaction telemetry.

### Maintainability
- Codebase follows feature-sliced organization on frontend and backend.
- Shared contracts are typed, versioned, and tested.
- New pipeline steps and AI actions can be added without cross-cutting rewrites.

## 14. Success Metrics
### Product metrics
- Median time from file import to first chart render
- Median time from opening a workspace to running first pipeline
- Reuse rate of saved pipelines
- Percentage of AI interactions rated useful by users
- Number of exports or investigation summaries created per active workspace

### Engineering metrics
- Import success rate
- Pipeline step failure rate
- AI request latency and error rate by provider
- Frontend error rate
- Test coverage of core ingestion, pipeline, and AI orchestration flows

## 15. MVP Release Criteria
The MVP is ready for internal use when:
- A user can import a sample CSV or JSON signal file.
- The system renders at least one signal view instantly enough to feel interactive.
- A user can build and rerun a pipeline with at least five transformation types.
- Intermediate outputs can be inspected per pipeline step.
- A user can ask AI about a selected range and get a grounded response.
- Workspace state, annotations, and pipelines persist across sessions.
- Basic export to Markdown summary and chart snapshot works.

## 16. Product Risks
- Large datasets may require aggressive downsampling or background processing to stay responsive.
- AI explanations may appear authoritative even when the underlying context is incomplete.
- Too much flexibility in the pipeline builder could increase complexity before core workflows are proven.
- File-format fragmentation can expand scope quickly.
- Feature-Sliced Architecture can become ceremonial if slice boundaries are not enforced pragmatically.

## 17. Assumptions
- The first release targets browser-based usage rather than native desktop.
- Early adopters are comfortable working with structured data files instead of live hardware streams.
- External LLM providers are acceptable for the first release from a latency and cost perspective.
- The initial product can be delivered as a modular monolith rather than distributed microservices.

## 18. Technical Architecture Direction
### 18.1 Frontend
- Framework: React with TypeScript
- UI component foundation: shadcn/ui, customized to support a dense IDE-like workspace
- State: query/cache for server state, localized UI state per slice, shared app shell state
- Rendering: Apache ECharts for high-performance interactive charting with support for large time-series datasets
- Architecture: Feature-Sliced Architecture adapted for product scale

Suggested frontend slice layout:

```text
src/
  app/
  pages/
  widgets/
  features/
    import-signal/
    build-pipeline/
    inspect-selection/
    ask-ai/
    export-report/
  entities/
    signal/
    pipeline/
    workspace/
    annotation/
    ai-session/
  shared/
    api/
    config/
    lib/
    ui/
```

Frontend responsibilities:
- Rich IDE-style layout and navigation
- Signal rendering and interactions
- Workspace composition and local state orchestration
- AI interaction UI with transparent context preview

### 18.2 Backend
- Platform: ASP.NET Core on .NET
- Endpoint framework: FastEndpoints
- Shape: modular monolith with feature folders under `Features/`
- Slice convention: each feature groups its own `Command`, `Endpoint`, `Handler`, and `Response` types together
- Serialization strategy: JSON by default with MessagePack used for compact binary contracts and payloads where appropriate
- Persistence: relational database for workspace metadata and pipeline definitions, object/file storage strategy for uploaded signal files and derived artifacts
- Processing: background job execution for expensive pipeline runs when needed
- AI: provider adapter layer with prompt templates and context assembly services

Pragmatic backend slice layout:

```text
src/
  App/
    Host/
    Composition/
  Features/
    Importer/
      Command/
      Endpoint/
      Handler/
      Response/
    Signals/
      Command/
      Endpoint/
      Handler/
      Response/
    Pipelines/
      Command/
      Endpoint/
      Handler/
      Response/
    Workspaces/
      Command/
      Endpoint/
      Handler/
      Response/
    AiAssistant/
      Command/
      Endpoint/
      Handler/
      Response/
    Reports/
      Command/
      Endpoint/
      Handler/
      Response/
  Shared/
    Kernel/
    Contracts/
    Observability/
    Persistence/
```

Backend responsibilities:
- File ingestion and normalization
- Canonical signal storage and retrieval
- Pipeline execution and execution tracing
- Workspace persistence
- AI provider orchestration and prompt grounding
- Export generation

### 18.3 Why this architecture
- React FSA keeps UI complexity modular and keeps entities/features explicit.
- .NET feature slices reduce coupling better than layered-by-technical-concern folders.
- Keeping `Command`, `Endpoint`, `Handler`, and `Response` together inside each feature keeps behavior local and makes vertical slices easier to ship and maintain.
- A modular monolith is sufficient for MVP and avoids premature service decomposition.

## 19. Domain Model
Core entities:
- Workspace
- SignalAsset
- SignalChannel
- SignalSelection
- PipelineDefinition
- PipelineStep
- PipelineRun
- PipelineStepRun
- Annotation
- Bookmark
- AiSession
- AiMessage
- Report

Key relationships:
- A workspace contains many signals, annotations, and pipeline runs.
- A pipeline definition contains ordered pipeline steps.
- A pipeline run references one pipeline definition and one or more input signals.
- An AI message references a context snapshot derived from the workspace state.

## 20. API and Integration Requirements
### Core API capabilities
- `POST /signals/import`
- `GET /signals/{id}`
- `GET /signals/{id}/channels`
- `POST /pipelines`
- `POST /pipelines/{id}/run`
- `GET /pipeline-runs/{id}`
- `POST /workspaces`
- `GET /workspaces/{id}`
- `POST /ai/sessions/{id}/messages`
- `POST /reports/export`

### External integrations
- LLM provider adapters such as OpenAI or Anthropic through server-side abstractions
- File/object storage abstraction for raw uploads and generated artifacts
- Optional telemetry stack for logging, traces, and metrics

## 21. UX Requirements
- The UI must feel tool-like, not dashboard-like.
- shadcn/ui should be used as the base component system, but styling and composition must be customized so the product does not feel like a generic admin template.
- Default workflow should require minimal setup after import.
- Users must always know:
  - what data is loaded
  - what selection is active
  - what pipeline ran
  - what context AI received
- Keyboard shortcuts should be introduced early for common actions such as search, run pipeline, focus AI, and bookmark selection.

## 22. Accessibility Requirements
- Keyboard navigation across workspace panels
- Sufficient contrast for data overlays and annotations
- Screen-reader labels for controls, inspector values, and AI actions
- Non-color indicators for warnings and anomalies

## 23. Testing Strategy
- Backend unit tests for domain logic and transformation steps
- Backend integration tests for import, pipeline runs, and AI orchestration boundaries
- Frontend component tests for key widgets and feature flows
- End-to-end tests for import, visualize, run pipeline, ask AI, and export report

## 24. Phased Delivery
### Phase 1: MVP
- Import, visualize, transform, inspect, ask AI, save workspace

### Phase 2
- More file formats, collaboration primitives, pipeline library improvements, richer exports

### Phase 3
- Domain-specific packs, plugin ecosystem, live sources, enterprise governance

## 25. Open Questions
- Which signal domains matter most for the first user group: embedded telemetry, audio, industrial sensor data, or another segment?
- What are the target upper bounds for MVP dataset size and sampling rate?
- Should saved pipelines be portable as plain JSON only, or also as shareable templates with version constraints?
- Which external LLM providers should be first-class at launch?
- Does the first release require authentication, or can it begin as a local/single-tenant workspace experience?

## 26. Recommendation for the First Build
Build a narrow but real vertical slice:
- CSV/JSON import
- Multi-channel waveform view
- Pipeline builder with 5 to 6 transformations
- Step-by-step pipeline trace
- AI chat grounded in active selection and pipeline context
- Saved workspaces and Markdown report export

This path validates the product thesis without overcommitting to niche formats or collaboration infrastructure too early.
