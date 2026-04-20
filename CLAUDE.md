# CLAUDE.md

## Purpose

This repository is a production-oriented **C# WPF vision application** built around a **.NET Generic Host + DI + BackgroundService + Channels** pipeline. The system orchestrates PLC triggers, HALCON image capture, preprocessing, inspection, UI display, image logging, CSV/report sinks, and PLC writeback.

When working in this repo, optimize for:

- correctness first
- production stability second
- deterministic pipeline behavior third
- performance and memory discipline always
- minimal architectural churn

Do not treat this as a generic CRUD desktop app. It is a latency-sensitive, image-heavy, hardware-integrated application.

---

## The developer this repo is for

Write like a strong senior C# engineer helping another engineer who wants to **understand the system deeply**, not just receive code.

Preferred collaboration style:

- explain the architecture and thought process first
- then propose file-by-file changes
- then provide copy-paste-ready code
- preserve existing naming and patterns unless there is a strong reason not to
- avoid unnecessary abstraction, pattern inflation, or framework churn
- be explicit about ownership, disposal, threading, DI, and runtime consequences
- when debugging, show where to put breakpoints and what to log
- when giving code, make it compile against the repo’s current shapes instead of inventing parallel types

If there is uncertainty about an existing type or method shape, inspect the actual code before changing it.

---

## Architecture and project boundaries

Keep the layers strict.

### `VisionApp.Core`
Contains domain models, interfaces, orchestration, engine state, and hosted pipeline services.

**Must not contain:**
- HALCON-specific logic
- PLC/libplctag implementation details
- WPF/UI-specific logic
- infrastructure concrete classes

Typical contents:
- domain records like `TriggerKey`, `TriggerEvent`, `CaptureRequest`, `RawFrameArrived`, `FrameArrived`, `InspectionResult`, `CycleCompleted`
- interfaces like `ITriggerSource`, `ICamera`, `IFramePreprocessor`, `IInspectionRunner`, `IImageLogger`, `IResultSink`, `IFrameObserver`
- orchestration/state machine logic
- hosted services that move data through channels

### `VisionApp.Infrastructure`
Contains hardware integration and concrete implementations.

Typical contents:
- PLC trigger source (`PlcTriggerSource`)
- HALCON camera implementations (`HalconGigECamera`)
- HDev procedure-based framegrabber setup
- preprocessing implementations
- image logging pipeline
- defect assignment implementations
- CSV / PLC / output sinks
- options classes and DI module wiring

### `VisionApp.Wpf`
Contains UI only.

Typical contents:
- views
- viewmodels
- stores
- observers that bridge pipeline output to UI state
- `HSmartWindowControlWPF` usage

Do not leak UI logic into Core or Infrastructure.

---

## Pipeline model

The app is a **channel-driven hosted pipeline**. Preserve that mental model.

Typical runtime flow:

1. `ITriggerSource` yields `TriggerEvent`
2. cycle orchestration / engine validates trigger progression and emits `CaptureRequest`
3. camera dispatcher captures raw images
4. preprocess stage transforms raw frames into processed frames
5. observers / logging consume processed frames
6. inspection runs on processed frames
7. results are published to sinks / observers / station-level post-processing
8. cycle completes only when end conditions and expected result counts are satisfied

### Current service pattern

Prefer the established service boundaries:

- `TriggerPumpService`
- `CycleEngineService`
- `CameraDispatcherService`
- `FramePreprocessService`
- `InspectionService`
- background services for image writing, heartbeat, and other integration concerns

If you add a new behavior, first ask:

- Is this orchestration?
- Is this per-frame transformation?
- Is this post-inspection business logic?
- Is this a sink?
- Is this UI projection?

Put it in the correct stage instead of stuffing logic into a nearby class.

### Channel shapes

The pipeline evolved from a simpler model into a split raw/processed model. Preserve that shape.

Common channels:
- `Channel<TriggerEvent>`
- `Channel<CaptureRequest>`
- `Channel<RawFrameArrived>`
- `Channel<FrameArrived>`
- `Channel<InspectionResult>`

Typical intent:
- `RawFrameArrived` carries raw frames from camera capture
- `FrameArrived` carries the post-preprocess frame that UI, inspection, and logging should use

### Service responsibilities

#### `TriggerPumpService`
- reads `ITriggerSource.ReadAllAsync(ct)`
- writes `TriggerEvent` into the trigger channel
- should behave like an ongoing async stream until cancellation

#### `CycleEngineService`
- is the single source of truth for trigger progression and cycle completion
- reads trigger events and inspection results
- serializes access to the cycle engine when required
- emits `CaptureRequest`
- fans completed cycle output to `IResultSink` implementations

#### `CameraDispatcherService`
- reads `CaptureRequest`
- resolves the target camera
- captures a **raw** frame
- writes `RawFrameArrived` into the raw frame channel

#### `FramePreprocessService`
- reads `RawFrameArrived`
- runs the configured preprocessor such as gamma correction
- publishes the processed `FrameArrived`
- ensures the raw frame is disposed when ownership has safely moved to a replacement frame
- UI and logging should consume the processed frame, not the pre-preprocess frame

#### `InspectionService`
- reads processed `FrameArrived`
- executes `IInspectionRunner`
- publishes `InspectionResult`
- disposes frame ownership in `finally` when it owns the frame lifetime

---

## Deterministic cycle engine rules

Respect the cycle engine. Do not bypass it with ad hoc state in UI or infrastructure.

Typical plan semantics:
- `OrderedTriggers` defines expected trigger sequence and expected count
- `StartTriggers` defines what can begin a cycle
- `EndTriggers` defines what must be seen before completion
- multi-end completion is supported
- start trigger may also be an end trigger for single-trigger cycle scenarios

Typical states:
- `Idle`
- `Running`
- `Completing`

Rules:
- only valid start triggers can start a cycle
- each trigger index for a product should only count once per cycle
- completion depends on both trigger/end-state logic and result-count readiness
- station-level completion may occur before full app cycle completion; preserve that distinction

---

## Configuration-first philosophy

**Rules, thresholds, and station-specific settings belong in configuration, not hardcoded constants.**

Use configuration when:
- a value may change post-launch
- field teams may need to tune it without redeploying
- station behavior differs per installation or product line

Examples:
- vertical band filter rules
- per-station mapping rules
- PLC routing and timeout settings
- image logging behavior
- model paths and per-station inspection settings

Do not force absurd config complexity for behavior that is clearly core code logic. Use judgment.

### Strongly typed options

Prefer strongly typed options bound from configuration.

Rules:
- option classes should usually be `sealed class`
- use `{ get; init; }` for config properties where practical
- each options class should expose `public const string SectionName`
- bind in DI with `services.AddOptions<T>().Bind(config.GetSection(T.SectionName))`
- inject via `IOptions<T>` and capture `.Value` in the constructor

Example:

```csharp
public sealed class MyService
{
    private readonly MyOptions _options;

    public MyService(IOptions<MyOptions> options)
    {
        _options = options.Value;
    }
}
```

### Config binding gotchas

- positional records cannot be directly bound well by `IConfiguration`; use a mutable DTO and convert to a domain record
- dictionary keys such as `Dictionary<int, T>` appear as strings in JSON but the binder can convert them
- production config may live outside the repo; do not assume `appsettings.json` is the only active source

---

## Code organization and naming

Prefer organization by domain rather than by technical pattern alone.

Typical namespaces/folders:
- `Inspection.Composition` — per-station pipeline builders
- `Inspection.Steps` — detectors, filters, post-processors
- `Inspection.DefectAssignment` — assignment and aggregation logic
- `Inspection.Assignment` — assignment options and rule DTOs
- `PlcOutbound` — write queue and heartbeat
- `Triggers` — PLC trigger handling
- `Sinks` — CSV, PLC, logging, or other output handlers

Naming guidance:
- station-specific types should be explicit: `Station4PipelineBuilder`, `Station5DefectAssignmentObserver`
- options names should match appsettings sections when possible: `Station4DefectAssignmentOptions`
- interfaces should reflect the actual architectural boundary: `IStationPipelineBuilder`, `IPlcWriteQueue`, `IInspectionStep`
- use `sealed` when inheritance is not a real requirement

---

## HALCON rules

HALCON objects are dangerous if treated casually.

### Ownership and disposal

HALCON images are heavy and must always have a clear owner.

Rules:
- uninitialized `HImage` objects must not be disposed blindly
- if an image is cloned, the clone owner must dispose it
- UI should own only UI clones, never pipeline-owned originals
- logging queues must dispose dropped clones
- preprocessing may dispose raw frames only when it has produced a safe replacement
- inspection should dispose frames in `finally`
- stores/viewmodels that replace images must dispose the previous image safely

### UI rule

Use `HSmartWindowControlWPF` rather than converting to `BitmapSource` unless there is a compelling reason.

### HALCON procedure rule

Camera parameter setup belongs in HDev procedures, not scattered through C#.

Keep settings such as:
- `TriggerMode`
- `StreamBufferHandlingMode`
- `AcquisitionMode`
- frame grabber startup / close sequences

inside the HALCON procedure path / procedure definitions when that is the established repo pattern.

### Package/version rule

Be careful with HALCON package version mismatches across projects. Do not casually change HALCON package versions in only one project.

---

## PLC and libplctag rules

This repo uses PLC-triggered capture and config-driven tag groups.

### Trigger handling expectations

Typical assumptions:
- polling via `PeriodicTimer`
- configurable `ReadDelayMs`
- configurable `MinLowMs`
- sync gating such as ignoring triggers `2..N` until trigger `1` has been seen
- fan-out from one base tag group to multiple camera IDs when needed

When debugging missing triggers, do not guess. Instrument first.

Check:
- actual poll duration vs configured `ReadDelayMs`
- tag read status codes
- re-arm transitions
- rising edge detection
- ignored edges due to sync gating
- pulse width assumptions
- whether the camera returned a buffered frame rather than the intended one

### PLC outbound write queue

The outbound PLC write path should remain ordered and deliberate.

Rules:
- use a single-reader ordered queue, typically `Channel<PlcWriteEntry>`
- bool writes use `ElementSize = 1` and write via bit methods
- DINT writes use `ElementSize = 4` and write via `SetInt32(0, value)`
- commit with `Write()` and inspect status after write
- on write/tag failure, evict cached tag state so the next attempt can recreate it

### Outbound configuration pattern

Typical structure:
- shared gateway, routing path, and timeout values in `PlcOutboundOptions`
- per-station output shape in a station results options type such as `PlcStationResultsOptions`
- separate heartbeat service toggling a bool tag at a configured interval

### Timestamp completion pattern

When implemented, timestamp writes should follow defect writes.

Pattern:
- after defect bool tags are written, write a DINT timestamp
- format is typically `HHmmss` encoded as an `int`
- tag shape is typically `{BaseTag}.{TimestampTagSuffix}`
- this signals to the PLC that the write cycle is complete and safe to consume

### libplctag caution

Do not assume constructor signatures or wrapper semantics. Inspect the actual wrapper/API version used in the repo before writing tag creation code.

---

## Channels, throughput, and memory discipline

This codebase handles large image objects. Backpressure decisions matter.

### General rules

- prefer bounded channels for high-volume image flow unless there is a strong reason not to
- choose a deliberate `FullMode`
- understand whether the producer should wait, drop oldest, or drop newest
- never allow silent leaks when items are dropped
- never enqueue large HALCON objects without a disposal plan

### Logging rule

Image logging must never destabilize the live pipeline.

If logging cannot keep up:
- real-time mode should prefer controlled dropping over app degradation
- completeness mode may allow backpressure, but only if that tradeoff is intentional

Preferred practical defaults when throughput matters:
- prefer JPEG over PNG
- use bounded writer queues
- increase queue capacity carefully rather than blindly
- avoid uncontrolled fire-and-forget writer explosions
- ensure dropped items are disposed correctly

### Known tradeoff

- `DropWhenBusy = false` preserves more completeness but can stall preprocess/UI flow through backpressure
- `DropWhenBusy = true` preserves responsiveness but may drop frames if the writer cannot keep up

Always make that tradeoff explicit.

### Performance mindset

Every new stage must be evaluated for:
- latency
- allocations
- queue growth
- UI responsiveness
- blocking behavior
- disposal behavior under load

---

## WPF and UI rules

The UI is a projection of pipeline state, not the system of record.

### Keep UI thin

Allowed:
- minimal view-specific code-behind
- `HSmartWindowControlWPF` hookup
- viewmodel/store updates
- observer code that projects processed pipeline output into UI state

Not allowed:
- core orchestration logic
- camera logic
- PLC logic
- inspection decision logic

### Frame display rule

The UI should display the same processed frame that the inspection pipeline uses when preprocessing is enabled.

### Store/viewmodel rule

Stores and tile viewmodels that hold HALCON images must dispose replaced or cleared images correctly.

---

## Inspection and post-processing rules

### Inspection runner

`IInspectionRunner` remains the entry point for **per-frame** inspection.

Prefer:
- ordered step execution
- explicit output keys
- predictable result contracts

### Filtered detection source

When filtered detections exist, downstream consumers should prefer them over raw detector output.

Typical pattern:
- raw detector output under one key
- filtered output under a key such as `YoloX_Filtered`
- overlays, defect assignment, and reporting consume the filtered result

### Post-inspection observers

Observers are the right place for **after inspection** actions such as:
- UI projection
- station-level defect assignment
- CSV reporting
- PLC bool decisions
- future outbound integrations

Do not bury station aggregation logic inside a YOLO step.

---

## Defect assignment conventions

Defect assignment is **station-level business logic**, not detector logic.

### Core pattern

Prefer this shape:
- config/options define station rules
- a locator or mapper resolves board/bearer/element assignment
- an observer processes `InspectionResult`
- an accumulator tracks counts per cycle/station
- a sink writes the final output

### What defect assignment is for

The goal is to turn per-frame detections into a station-level summary that:
- assigns each defect to a physical product element
- optionally assigns a bearer based on trigger index
- applies station-specific rename rules where needed
- produces a final dictionary of counts suitable for CSV, PLC, or downstream actions

### Keep per-frame and station-level logic separate

Per-frame work:
- run YOLOX or other detectors
- apply frame-local filters
- generate overlays and raw/filtered outputs

Station-level work:
- combine outputs across triggers and cameras
- assign defects to boards/bearers/elements
- aggregate counts
- decide final CSV / PLC output shapes

### Station differences

Assume different stations may require different strategies.

Examples:
- one station may segment by X-position or vertical band rules
- another may map more directly from trigger index
- one station may have rename rules such as PN → TN on certain triggers
- another may not

Favor config-driven or strategy-driven differences instead of copy-paste forks.

### Station completion behavior

Station completion may happen before overall application cycle completion. Preserve that distinction.

Station accumulators may flush when station end triggers are satisfied even if the overall cycle engine is still waiting on other stations.

### Known pipeline distinction

When these conventions apply:
- Station 4 may be closer to `YoloX -> Decide`
- Station 5 may be closer to `YoloX -> VerticalBandFilter -> ElementAssignment -> Decide`

Do not flatten those differences unless you are intentionally generalizing them.

---

## DI and module wiring rules

Use modular registration and keep startup understandable.

Typical module patterns:
- `AddOfflineIO(...)`
- `AddOnlineIO(...)`
- `AddImageLoggingModule(...)`
- `AddInspectionModule(...)`
- `AddSinksModule(...)`
- additional modules for outbound PLC, reporting, or station-specific composition

### DI rules

- register options in one clear place
- register concrete sinks if another class depends on the concrete type
- use fanout/composite sinks when one observer must write to multiple outputs
- avoid circular dependency traps
- if a service depends on an abstraction such as `IShiftResolver`, ensure a real implementation is registered
- if host startup fails, suspect DI misregistration or bad config binding first

All major infrastructure services, builders, sinks, and observers should be wired in the central infrastructure DI module pattern already used by the repo.

---

## Adding new features

Follow the established feature pattern.

### Config-driven feature workflow

1. Define the options class in the correct domain folder.
2. Add the config section to the active appsettings source.
3. Register the options in DI.
4. Inject via `IOptions<T>` into the consuming class.
5. Update both config schema and sample config when extending existing options.

### Example shape for extending PLC outbound

If adding a new PLC write type:
- add a new `PlcWriteEntry` subtype
- extend the queue service switch/dispatch logic
- add tag creation and write helpers for that concrete PLC type
- keep the queue ordered and explicit

Prefer extension over rewrite.

---

## Testing and validation

- prefer meaningful integration tests over fake in-memory tests when integration behavior is the real risk
- validate config at startup where practical so failures happen early
- pipeline steps should log enough rejected/filtered behavior to support observability
- test locally before pushing changes that affect trigger flow, image ownership, or PLC output

When a change affects runtime behavior, provide:
- where to breakpoint
- what logs to inspect
- what successful runtime behavior should look like

---

## Git and commit conventions

### Commit practice

- create a **new commit** for each feature/fix rather than amending published history
- commit messages should explain **why**, not just **what**
- include station, pipeline, or PLC context where relevant
- honor signing configuration if the repo uses signed commits

Example:

```text
Bad: Move VerticalBandRules to appsettings
Good: Extract vertical band rules to appsettings for field tuning
```

### Branch/PR guidance

- work on feature branches off the main development branch used by the repo
- prefer one coherent PR for a related set of changes rather than fragmented micro-PRs
- avoid force-pushing published shared branches unless the team explicitly expects it

---

## Debugging workflow

When something is broken, debug in this order.

### Trigger/cycle issues

Check:
- trigger source logs
- poll cadence
- sync gating
- re-arm behavior
- cycle engine transitions
- duplicate trigger handling
- end-trigger satisfaction

### Camera/frame issues

Check:
- camera connectivity / reconnect path
- queue draining behavior
- whether the intended frame or a buffered frame is returned
- uninitialized frame fallbacks

### Inspection/result issues

Check:
- runner output keys
- filtered vs raw detections
- observer execution
- sink registrations

### UI issues

Check:
- whether UI is receiving processed or raw frames
- whether UI owns a clone instead of a pipeline image
- tile/store disposal on replace/clear

### Memory/performance issues

Check:
- bounded vs unbounded channels
- channel capacities and full modes
- logger throughput
- clone disposal on drop
- any new allocation-heavy hot path

---

## Known constraints and gotchas

- positional records do not bind cleanly from `IConfiguration`
- dictionary keys may appear as strings in JSON
- single-reader PLC write queues are intentional to preserve order
- Core must not reference Infrastructure
- station-specific rules should not leak into generic shared mechanics
- UI must not become the owner of pipeline-owned images
- dropped channel items that own HALCON resources must be disposed
- `appsettings.json` may be only the dev-time config source; production deployments may rely on an external ProgramData config folder
- do not use outdated field/property names from old drafts when the repo has moved on

---

## Key files and locations

These are common anchor points and should be checked before making assumptions:

| Purpose | Location |
|---|---|
| Config schema (Station4) | `VisionApp.Infrastructure/Inspection/Assignment/Station4DefectAssignmentOptions.cs` |
| Config schema (Station5) | `VisionApp.Infrastructure/Inspection/Assignment/Station5DefectAssignmentOptions.cs` |
| PLC settings | `VisionApp.Infrastructure/PlcOutbound/PlcOutboundOptions.cs` |
| Station4 pipeline | `VisionApp.Infrastructure/Inspection/Composition/Station4PipelineBuilder.cs` |
| Station5 pipeline | `VisionApp.Infrastructure/Inspection/Composition/Station5PipelineBuilder.cs` |
| DI registration | `VisionApp.Infrastructure/DI/VisionInfrastructureModules.cs` |
| App config | `VisionApp.Wpf/appsettings.json` |
| External config | `C:\ProgramData\AMV\VisionApp\0.0.1\AppSettings` |

---

## Default coding style for this repo

- use clear, direct C#
- favor explicit names over cleverness
- keep async flows cancellation-aware
- use `finally` for cleanup when ownership is clear
- prefer options + DI over static/global state
- prefer immutable records for domain messages when practical
- keep infrastructure classes focused on one integration concern
- keep comments high-signal and engineering-oriented
- write comments for **why**, not **what**
- do not refactor broad surrounding code during a focused bug fix unless necessary
- simple is better than clever

---

## If you are an AI coding assistant editing this repo

Before changing anything, assume the following:

1. The existing architecture is deliberate.
2. Hardware integration and image ownership matter more than stylistic purity.
3. The developer wants both a working answer and a clear explanation.
4. Matching the current repo matters more than presenting a textbook-perfect rewrite.
5. Every change should respect Core/Infrastructure/Wpf boundaries.

When asked to implement something, follow this workflow:

1. inspect the actual current shapes first
2. explain briefly where the change belongs and why
3. implement the smallest viable change that fits the architecture
4. call out DI/config/runtime implications
5. provide verification steps

Good outputs usually include:
- a short architecture explanation first
- a file-by-file change list
- exact code that matches the repo’s current contracts
- notes on disposal and threading
- DI registration updates
- config updates when required
- verification steps

Bad outputs usually look like:
- inventing unnecessary new abstractions
- moving logic into the wrong layer
- assuming type shapes that were not checked
- ignoring HALCON disposal rules
- ignoring queue backpressure
- changing many files when one strategy/config extension would do
- hiding important tradeoffs

If you need to add a new capability, first try to express it as one of:
- a new options-backed strategy
- a new observer
- a new sink
- a new infrastructure implementation behind an existing Core contract
- a small extension to the existing channel pipeline

Do not casually replace the pipeline model, host model, or HALCON/UI integration approach.
