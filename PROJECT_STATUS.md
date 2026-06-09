# TopBar Project Status

## Latest

Latest commits:

27e1b71 feat: add hover preview fade-out animation
29f728d diagnostics: add TB-007 window enumeration tracing
4df9b8e docs: update work-pc trial status
677480c docs: add work-pc portable release workflow
deaa288 style: polish hover preview glass appearance
fa51923 feat: keep grouped hover preview open after close

Latest commit: 27e1b71 feat: add hover preview fade-out animation.

Build44A work-PC readiness investigation completed.

Build44B portable release workflow completed. No runtime code changes were made.

Build44C work-PC trial checklist prepared. No runtime code changes were made.

Build45B TB-007 Diagnostics First completed and runtime-validated.

Build46A Hover Preview Fade Animation completed and manually verified.

Work-PC trial package:

* path: `artifacts\publish\workpc\TopBarPoC-workpc-win-x64.zip`
* size: 69,842,835 bytes
* SHA-256: `A17AADFB17A6AE7A20B144AC71129B1A8731B42CCB3C3FA2BBB130EAC87E552B`

Repository state:

* `master` and `origin/master` are synced at 27e1b71
* tracked working tree contains only the pending `PROJECT_STATUS.md` documentation update
* `.claude/` is untracked

Current focus:

* completed: Build46A - Hover Preview Fade Animation
* next: Build46B - Hover Preview Glass Polish
* after: Build46C - Grouped Preview Close Button

## Build44 - Work-PC Trial Preparation

### Completed - Build44A Work-PC Readiness Investigation

* Reviewed portable deployment constraints for a corporate Windows PC.
* Identified SmartScreen, Defender, EDR, AppLocker, and WDAC as potential blockers.
* Confirmed the trial should use a self-contained `win-x64` portable package.
* No runtime changes were required.

### Completed - Build44B Portable Release Workflow

* Added the documented work-PC portable publish and rollback workflow.
* Produced the self-contained package at
  `artifacts\publish\workpc\TopBarPoC-workpc-win-x64.zip`.
* Package size: 69,842,835 bytes.
* Package SHA-256: `A17AADFB17A6AE7A20B144AC71129B1A8731B42CCB3C3FA2BBB130EAC87E552B`.
* Manual development-PC smoke test passed for launch, AppBar reservation, exit,
  reservation release, and relaunch.
* No runtime changes were made.

### Prepared - Build44C Work-PC Trial Checklist

The trial checklist covers:

* first launch and corporate security controls
* AppBar reservation, exit, release, and relaunch
* Explorer restart and sleep/resume
* monitor hot-plug and DisplaySwitch topology changes
* display scaling and taskbar auto-hide when policy permits
* Teams, Outlook, browser, and VS Code daily workflow
* evidence collection from screenshots, security prompts, process state, and
  `%APPDATA%\TopBarPoC\logs\`

Findings will be categorized as blocker, daily-use risk, minor issue, or passed.
Code changes require a specific reproduced failure and approval.

## Build45 - TB-007 Non-Taskbar Surface Diagnostics

### Completed - Build45B Diagnostics First

* Added Release-visible `[WindowEnum]` diagnostics for included and excluded windows.
* Diagnostics record the final decision and reason together with HWND, title, class name,
  process name and ID, visibility, owner, DWM cloaked state, and extended window styles.
* Added state-change deduplication and removal of vanished HWNDs from the diagnostic cache.
* Existing window eligibility behavior and filter ordering remain unchanged.
* No new deny lists or process-name filters were added.
* `dotnet build` passed with 0 warnings and 0 errors.
* `git diff --check` passed.
* Runtime validation confirmed that known Steam and Discord helper surfaces were excluded.
* The previously observed unexpected helper chip was not reproduced during validation.
* TB-007 remains open as a watch item during the Build44C work-PC trial and daily use.

## Build46 - Hover Preview Polish

### Completed - Build46A Hover Preview Fade Animation

* Added a 150ms hover preview fade-out animation for ordinary hover departure.
* Preserved the existing fade-in behavior.
* Added a generation/version guard so stale animation completion cannot hide a newly
  shown or rebuilt preview.
* Added fade-out cancellation when the pointer re-enters the preview.
* Kept DWM thumbnails registered throughout fade-out and delayed teardown until the
  animation completes.
* Preserved immediate teardown for drag operations, activation paths, display refresh,
  shutdown, and final-card close.
* Singleton and grouped previews continue to share the existing card and window lifecycle.
* `dotnet build` passed with 0 warnings and 0 errors.
* `git diff --check` passed.
* Manual verification passed.
* Commit: 27e1b71 feat: add hover preview fade-out animation
* Commit pushed; `master` and `origin/master` are synced.

### Next

* Build46B - Hover Preview Glass Polish
* Build46C - Grouped Preview Close Button

## Build37 — Glass Minimal Refresh

### Current State

Build37B Glass Minimal refresh completed.
Build37C Balanced Density completed.

Status:

* Build37 completed
* commit: 0700358
* pushed to origin/master
* dotnet build: 0 warnings / 0 errors
* real-device/manual verification passed

Implemented:

* Glass Minimal visual refresh
* dark teal glass background
* subtle border / top highlight
* mint active chips
* softer inactive glass chips
* bottom-only rounded bar
* spacing / separator / clock polish

Preserved:

* AppBar behavior
* grouped chips
* ctrl-click cycle
* manual reorder
* drag/drop
* right-click menus
* overflow behavior
* shell logic

Design source of truth:

* design/GlassMinimal.png
* design/GlassMinimal.html

Priority:

1. PNG = final appearance
2. HTML = spacing/layout reference
3. Glass Minimal written spec = behavior / constraints

---

### Completed — Build37C (Balanced Density)

Adopted direction:

UW monitor considerations mean full icon-only mode is NOT the default.

Balanced Mode becomes the intended standard mode.

Default chips:

* icon + short label
* slightly compact presentation
* practical information density
* good fit for ultrawide monitors

Active chip:

* mint glass pill
* expanded label
* MaxWidth 120
* ellipsis

Important constraint:

Do NOT remove adaptive width infrastructure yet.

Keep:

* Width binding
* RecalcChipWidth()
* WindowChipVm width plumbing

Build37C implemented:

* MainWindow.xaml focused
* short label tuning
* active chip expansion
* preserve Build37B visuals
* preserve AppBar safety

---

### Completed — Build37D (Auto Density)

Adaptive density switching implemented.

Modes:

* Expanded
* Balanced
* Compact

Commit:

55b0892 feat: add adaptive spacing density for window chips

---

### Design Gaps / Not Yet Matching Mock

Still behind the Glass Minimal target:

1. True glass material

* no Acrylic/Mica/backdrop blur yet
* current implementation uses fake glass

2. Typography

* spec: Manrope / JetBrains Mono
* current: Segoe UI

3. Status cluster completeness

* CPU/MEM / Wi-Fi / Battery design not fully matched

4. Launcher / pinned app treatment

* not fully aligned with mock

5. Overflow visual design

* compact collapse behavior not finalized

6. Adaptive contrast

* wallpaper brightness adaptation not implemented

7. DPI/material scaling polish

* blur/radius/shadow scaling future work

## Build40 — DWM Hover Preview

### Status

COMPLETE

Commits:

41578a0 feat: add DWM hover preview for single window chips
20f0724 fix: resolve chip drag/drop hit-test dead zone in gap column

### Delivered

- DWM live thumbnail hover preview for single-window chips
- 450ms hover delay reused from Build39 groundwork
- 200ms chip→preview grace timer (prevents flicker on chip→preview mouse transition)
- Title strip at bottom of preview window
- No focus stealing (ShowActivated=False)
- Drag-start hides preview immediately (no delay)
- Elevated and cloaked target safe handling (HRESULT check, title-only dark fallback)
- Grouped chip preview: delivered in Build41A

### Additional Fix — Drag/Drop Gap Hit-Test Dead Zone

Discovered during Build40 manual testing.

Root cause: Build39's DataTemplate Grid wrapper (chip + gap columns) had no Background,
making the gap column a WPF hit-test vacuum. DragOver stopped firing when the cursor
was in the gap, causing the drop cue to flicker and drops to land at the wrong position
(appended to end instead of inserted at the indicated gap).

Fix:

* Background="Transparent" on DataTemplate Grid — gap column becomes hit-testable
* FindChipButton / FindDescendantButton fallback — resolves chip button via descendant
  search when the gap Grid is the hit target, restoring correct drop position

Attribution: Build39-exposed latent issue. Pre-existing with margin-based gaps; wider
Expanded gaps and the visual separator made it reliably reproducible.

### Deferred

* Animated fade in / out
* Glass and transparency treatment (AllowsTransparency + DWM thumbnail validation)
* Dynamic grouped preview removal after WM_CLOSE

---

## Completed

### Build20

- adaptive horizontal overflow
- >10 chip support

### Build21

- manual chip reorder
- drag/drop ordering
- multi-monitor sync

### Build22

- overflow directional fades
- passive fade overlays

### Build23

- density presets
- compact=32
- comfortable=36
- large=40

### Build25

- multi-monitor refresh hardening
- runtime display refresh diagnostics
- chip overflow fade stability
- 10+ chip robustness validation support

### Build35

- runtime taskbar auto-hide robustness
- WM_SETTINGCHANGE handling
- shell settle refresh
- live runtime taskbar state handling

### Build37D

- adaptive window chip density
- Expanded / Balanced / Compact modes
- layout-pressure hysteresis
- commit: 55b0892 feat: add adaptive spacing density for window chips

### Build38A

- PostMessage SetLastError=true
- TryCloseWindow helper
- singleton / grouped close routing
- TryGetContextWindow diagnostics
- WS_EX_NOACTIVATE window filter hardening
- post-fetch empty-title validation
- [WindowEnum] include diagnostics
- temporary diagnostics cleanup
- commit: 9ef3386 fix: harden close diagnostics and window enumeration

Observed:

- Some helper / transient windows, such as Steam and Discord helper surfaces, may appear in TopBar while not appearing in the Windows taskbar.
- This motivated the Build38B investigation.

### Build40

- DWM live thumbnail hover preview for single-window chips
- 450ms hover delay / 200ms chip→preview grace timer
- Title strip / no focus steal / drag immediate hide
- Elevated and cloaked target safe handling
- Drag/drop gap hit-test dead zone fix (Build39-exposed latent issue)
- commits: 41578a0, 20f0724

### Build41A

- grouped horizontal hover previews
- reusable PreviewCardVm
- shared ShowForCards pipeline
- singleton compatibility preserved
- MaxVisiblePreviewCards = 4
- activate-on-card-click
- 10 DIP card spacing
- commit: 56e3325 feat: add grouped hover preview cards for grouped window chips

### Build41B

- WS_EX_NOACTIVATE hover preview hardening
- WM_MOUSEACTIVATE no-activate handling
- aspect-ratio-preserving DWM thumbnails
- grouped overflow +N indicator
- singleton preview compatibility preserved
- grouped preview compatibility preserved
- MaxVisiblePreviewCards = 4 preserved
- activate-on-card-click behavior preserved
- hover grace hide behavior preserved
- drag immediate hide behavior preserved
- commit: 0b67ea3 feat: polish hover previews

### Build42A

- diagnostics-only file-backed Debug logging for live AppBar / DPI validation
- process / thread DPI awareness diagnostics
- GetDpiForMonitor HRESULT and raw DPI diagnostics
- GetDpiForWindow and WPF transform diagnostics
- expanded DisplayRefresh and AppBarDpi logs
- TB-001 initial validation (full validation completed separately — see TB-001 in Active Backlog):
  - multi-monitor baseline passed
  - negative-origin baseline passed
  - runtime scaling manual UX passed
  - AppBar remained stable
  - no progressive downward drift observed
- observed system-DPI-aware mode:
  - processAwareness=1
  - no WM_DPICHANGED observed
  - hwndDpi remained 96
  - WPF transforms remained 1.0
- no runtime visual regression reproduced

### Build42B

- P2 runtime taskbar auto-hide overlap hardening
- investigation: ABN_POSCHANGED was universally skipped; ABN_STATECHANGE + 500ms settle
  was the sole recovery path, subject to shell-state race after auto-hide toggle
- root cause: shell broadcasts ABN_POSCHANGED when a taskbar edge reservation changes
  (authoritative "settled" signal); skipping it entirely forced reliance on the earlier,
  less stable ABN_STATECHANGE notification
- fix: ABN_POSCHANGED now handled with self-feedback guard (_inhibitAbnPosChanged flag)
  - flag set before SetWindowPos in QueryAndApplyPosition
  - flag cleared via BeginInvoke(Normal) one pump cycle later
  - self-generated ABN_POSCHANGED (from our own SetWindowPos) → inhibited
  - external ABN_POSCHANGED (from Windows taskbar reservation change) → QueueShellStateRefresh()
- settle timer preserved as safety net
- WM_WINDOWPOSCHANGING obstacle correction preserved and unchanged
- build: 0 warnings / 0 errors
- runtime validation:
  - startup burst: 6 external ABN_POSCHANGED processed (deduplicated to 1 refresh/monitor);
    2 self-generated correctly inhibited
  - steady state: zero AppBar activity across 15K+ log lines — no notification storm
  - both monitor positions stable; rc.Top = screen.Bounds.Y throughout — no downward drift
- limitation: live top-edge taskbar auto-hide toggle not directly reproducible on current
  machine (taskbar at bottom); ABN_POSCHANGED code path confirmed active via startup burst

### Build41C

- hover preview close button (TB-008 partial)
- `×` button added to each preview card title row; `PreviewCardCloseButtonStyle` matches
  existing grouped-selector close aesthetics
- hide-before-close: `HidePreview()` (DWM thumbnail teardown + window hide) fires before
  `TryCloseWindow` — no stale thumbnail after close action
- `WM_CLOSE` sent via existing `TryCloseWindow` (PostMessage, SetLastError, diagnostic log)
- grouped preview: each visible card gets its own `×`; closing any card hides the whole preview;
  no dynamic card list mutation — 500ms poll removes the closed window naturally
- `+N` overflow indicator unchanged
- `PreviewCardVm.Close` property added with no-op default — `ShowForHwnd` path unaffected
- `e.Handled = true` in `PreviewCardClose_Click` blocks outer card-body `Click` (no accidental activate)
- survived-WM_CLOSE diagnostic: deferred `BeginInvoke(Background)` check logs
  `survived WM_CLOSE — tray or WM_CLOSE-intercepting app` when `IsWindow(hwnd)` is still true
  one pump cycle after PostMessage
- known limitation: tray-resident and WM_CLOSE-intercepting apps (Discord, Steam, Slack, etc.)
  do not close on WM_CLOSE by design; chip persists accurately reflecting real window state;
  matches Windows taskbar behavior for the same apps; force-close is out of scope

## Key Findings

- UW 100% - Large (40 DIP) feels best.
- Launch DPI behavior mostly OK.
- Runtime DPI change manual UX passed under current system-DPI-aware mode.
- Taskbar parity design note: Windows taskbar behavior cannot be perfectly replicated via window style inspection alone. Shell-managed taskbar state may diverge from EnumWindows eligibility.

## Active Backlog

TB-005 is retained as the historical ID for completed Build37D Auto Density and is not reused below.

### P1 Critical

#### TB-001 Runtime DPI hardening — VALIDATION COMPLETE

Partial hardening delivered alongside Build40 (commit f52f7b2):

* Hover preview position now computed entirely in WPF DIP space
  (DeviceToDipPoint via PresentationSource.TransformFromDevice)
* Preview top uses Top + ActualHeight instead of physical-pixel conversion
* Screen-edge clamping now uses DIP screen bounds (was physical pixels — mismatch at non-100% DPI)
* QueueDisplayRefresh now tears down hover preview and clears hover state on display change
* WM_DPICHANGED decoded and logged (dpiX, dpiY, suggested RECT)
* QueryAndApplyPosition logs DPI scale and AppBar RECT on every reposition

Build42A diagnostics (commit fbc32e5):

* File-backed diagnostics enabled for zero-debugger AppBar / DPI validation
* Process/thread DPI awareness logged at startup
* GetDpiForMonitor HRESULT / raw DPI, GetDpiForWindow, and WPF transform logs added
* Runtime scaling manual UX passed: bar position/size and hover preview showed no obvious regression
* AppBar remained stable; no progressive downward drift observed
* Current app mode confirmed system-DPI-aware (processAwareness=1)
* No WM_DPICHANGED observed; hwndDpi remained 96; WPF transforms remained 1.0

TB-001 real-device validation (commit a4a5ab6):

* Mixed-DPI detection diagnostics ([AllScreensDpi]) added and confirmed working
* Runtime scaling change validated: DISPLAY1 100% → 125% while TopBar running
  — WM_SETTINGCHANGE wParam=0x009F is the effective refresh path for DPI changes
  — WM_DPICHANGED does not fire for system-DPI-aware apps during runtime scaling changes
  — WinForms Screen.Bounds correctly reflects updated virtual coordinates after scaling
  — DisplayRefresh correctly re-measured bar width and AppBar reservation on both monitors
  — Three-burst WM_SETTINGCHANGE oscillation absorbed cleanly by settle timer and debounce
* Non-primary monitor validated: portrait secondary at negative Y origin (Y=-476) stable
* Negative-origin AppBar validated: rc registered at Y=-476; no obstacle displacement observed
* AppBar RECT stable across all repositioning events; no downward drift
* WP-1 / WP-2 not reproducible on current hardware (all physical monitor DPIs = 96)
  — GetDpiForMonitor returns physical hardware DPI (96) regardless of Windows scaling setting
  — WP-1 / WP-2 remain theoretical risks for monitors with physical DPI ≠ 96

Validation result: PASS on all exercisable scenarios. No code changes required.

#### TB-002 Multi-monitor complete validation

Negative-origin, vertical stack, non-primary, runtime layout changes.

### P2 Important

#### TB-003 Grouped close UX / edge cases

Remaining grouped close work:

* sequential grouped close behavior
* stale handle handling strategy
* grouped close semantics / UX ambiguity
* popup interaction edge cases

#### TB-004 Context actions expansion

Pin / Hide / Remove candidates.

#### TB-007 Build38B — Known Non-Taskbar Surface Filter

Reduce helper / transient windows that appear in TopBar but not in the Windows taskbar.

Observed:

* Steam helper surfaces
* Discord helper surfaces

Notes:

* style rules alone cannot fully match Windows taskbar behavior
* possible ITaskbarList / shell state divergence
* prefer investigation + targeted filtering
* avoid blanket ApplicationFrameHost removal
* avoid aggressive deny lists without evidence
* Build45B added Release-visible, state-change-deduplicated `[WindowEnum]` diagnostics
  without changing eligibility behavior
* Build45B runtime validation excluded the observed Steam and Discord helper surfaces;
  the unexpected helper chip was not reproduced
* keep TB-007 open as a watch item during Build44C work-PC trial and daily use

### P3 Optional

#### TB-006 Density polish

Residual visual polish only. Build37C / Build37D density behavior is already implemented.

#### TB-008 Hover Preview Polish

Base hover preview delivered in Build40. Grouped horizontal preview cards delivered in Build41A.
Core hover preview polish delivered in Build41B. Close button delivered in Build41C.

Remaining work:

* Glass / transparency treatment (AllowsTransparency + DWM thumbnail validation)
* Dynamic grouped preview removal after WM_CLOSE

#### TB-009 Future UX — Window Chip Display Modes

Potential user-selectable display modes:

* icon only
* icon + title

### Next Build Candidates

#### Next - Build46B Hover Preview Glass Polish

Continue hover preview visual refinement with the glass treatment scoped separately from
the completed Build46A animation lifecycle work.

#### After - Build46C Grouped Preview Close Button

Continue the grouped preview close-button work as a separate build without expanding the
Build46A animation scope.

#### Parallel - Build44C Real Work-PC Trial Execution

Transfer the verified portable ZIP to an authorized work PC and execute the prepared
trial checklist. Record corporate security results, AppBar lifecycle behavior, runtime
display recovery, daily application workflow, and diagnostics evidence.

#### After Trial - Build44D Daily-Use Hardening

Proceed only if the Build44C work-PC trial finds a specific reproducible failure.
Scope fixes to approved blockers or daily-use risks; do not make speculative runtime changes.

#### P1 — TB-001 Runtime DPI hardening — COMPLETE

Validated in Build42A diagnostics and TB-001 real-device testing (commit a4a5ab6).
All exercisable scenarios passed. See TB-001 section in Active Backlog for full findings.

#### P1 — Hover preview polish (remaining visual refinements)

TB-008 follow-up. Fade animation completed in Build46A. Close button delivered in Build41C.
Remaining: glass / transparency treatment and dynamic grouped preview removal after WM_CLOSE.

#### P2 — Runtime taskbar auto-hide coexistence hardening

ABN_POSCHANGED guarded handling delivered in Build42B. Settle timer and
WM_WINDOWPOSCHANGING correction preserved. Remaining edge cases: top-edge taskbar
auto-hide, multi-session coexistence, and full-screen app interactions.

#### P3 — Future UX enhancements and backlog items

Window chip display modes, overflow / high-chip-count polish, right-click context actions,
and runtime taskbar filter improvements. See Active Backlog for full list.

## Future Design Direction

### Hover Preview Roadmap — Build41+ Concept

#### Current state

Build40 delivers single-window DWM hover preview.
Build41A delivers grouped horizontal preview cards.
Build41B delivers core hover preview polish.
Build41C delivers hover preview close button.
Build46A delivers hover preview fade-in/fade-out lifecycle polish.

#### Planned direction

Follow Windows taskbar hover preview UX closely.

#### Grouped preview delivered

Grouped chip hover shows multiple live previews horizontally — one preview card per grouped window.

Delivered layout:

```
[ preview A ][ preview B ][ preview C ]
```

Each preview card:

* DWM live thumbnail (client area)
* Title strip at bottom
* Activate on click
* Shared PreviewCardVm / ShowForCards pipeline
* 10 DIP spacing between cards
* WS_EX_NOACTIVATE / WM_MOUSEACTIVATE no-activate handling
* Aspect-ratio-preserving thumbnail destination
* +N overflow indicator when grouped windows exceed MaxVisiblePreviewCards

#### Remaining polish topics

* Optional glass / transparency treatment (AllowsTransparency + DWM thumbnail validation)
* Dynamic grouped preview removal after WM_CLOSE

#### Priority intent

Hover Preview Polish core is complete as Build41B.
Close button delivered as Build41C.
Fade animation delivered as Build46A.

---

### TB-001 Remaining Direction — VALIDATION COMPLETE

Real-device validation completed (commit a4a5ab6).

Validated:

* Runtime scaling change while running (WM_SETTINGCHANGE path confirmed)
* Non-primary monitor (portrait secondary at negative Y origin)
* Negative-origin monitor AppBar positioning
* AppBar RECT stability across multiple repositioning events

Key finding — Windows behavior:

* System-DPI-aware apps do not receive WM_DPICHANGED during runtime scaling changes
* WM_SETTINGCHANGE wParam=0x009F is the effective trigger for DPI recalculation
* WinForms Screen.Bounds correctly reflects virtual coordinate changes after scaling adjustments
* GetDpiForMonitor returns physical hardware DPI (96) regardless of Windows scaling setting
* Windows sends multiple WM_SETTINGCHANGE bursts per scaling change; settle timer absorbs them

Residual risks (not reproducible on current hardware):

* WP-1: Width formula divergence on monitors with physical hardware DPI ≠ 96
* WP-2: AppBar RECT height on those same monitors
* Monitor hot-plug: disconnect / reconnect not validated

Per-monitor DPI hardening should be investigated only if a visual regression appears on HiDPI
physical hardware (laptop panels, 4K displays) or if such hardware becomes available for testing.

---

## Known Problems

### KP-001 Runtime DPI runtime instability

Validated in Build42A diagnostics and TB-001 real-device testing (commit a4a5ab6). Runtime
scaling change (100% → 125%) validated: DisplayRefresh correctly adapted bar width and AppBar
reservation. Residual risk: HiDPI physical hardware (monitors with physical DPI > 96) not
available for testing; WP-1 / WP-2 remain theoretical for that scenario.

### KP-002 Grouped multi-window close edge cases

Grouped close can still exhibit intermittent stale-handle or popup interaction edge cases.

### KP-003 Negative-origin monitor validation — COMPLETE

Portrait secondary monitor at Y=-476 validated in TB-001 real-device testing. AppBar correctly
registered at negative Y; no obstacle displacement observed; bar position stable.
