# TopBar Project Status

## Latest

Latest commits:

0b67ea3 feat: polish hover previews
56e3325 feat: add grouped hover preview cards for grouped window chips
f52f7b2 fix: harden hover preview DPI coordinate handling
20f0724 fix: resolve chip drag/drop hit-test dead zone in gap column
41578a0 feat: add DWM hover preview for single window chips

Build41B hover preview polish completed, validated, and pushed to origin/master.

Build42A diagnostics enablement and TB-001 live DPI validation completed locally.

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

* Hover close button
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
- TB-001 partially validated:
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

## Key Findings

- UW 100% - Large (40 DIP) feels best.
- Launch DPI behavior mostly OK.
- Runtime DPI change manual UX passed under current system-DPI-aware mode.
- Taskbar parity design note: Windows taskbar behavior cannot be perfectly replicated via window style inspection alone. Shell-managed taskbar state may diverge from EnumWindows eligibility.

## Active Backlog

TB-005 is retained as the historical ID for completed Build37D Auto Density and is not reused below.

### P1 Critical

#### TB-001 Runtime DPI hardening

Partial progress delivered alongside Build40 (commit f52f7b2):

* Hover preview position now computed entirely in WPF DIP space
  (DeviceToDipPoint via PresentationSource.TransformFromDevice)
* Preview top uses Top + ActualHeight instead of physical-pixel conversion
* Screen-edge clamping now uses DIP screen bounds (was physical pixels — mismatch at non-100% DPI)
* QueueDisplayRefresh now tears down hover preview and clears hover state on display change
* WM_DPICHANGED decoded and logged (dpiX, dpiY, suggested RECT)
* QueryAndApplyPosition logs DPI scale and AppBar RECT on every reposition

Build42A diagnostics and validation:

* File-backed diagnostics enabled for zero-debugger AppBar / DPI validation
* Process/thread DPI awareness logged at startup
* GetDpiForMonitor HRESULT / raw DPI, GetDpiForWindow, and WPF transform logs added
* Runtime scaling manual UX passed: bar position/size and hover preview showed no obvious regression
* AppBar remained stable; no progressive downward drift observed
* Current app mode confirmed system-DPI-aware (processAwareness=1)
* No WM_DPICHANGED observed; hwndDpi remained 96; WPF transforms remained 1.0

Remaining monitored risks: mixed DPI, monitor hot-plug, explicit per-monitor DPI behavior,
and topology mutation edge cases. No immediate fix recommended unless a visual regression,
mixed-DPI requirement, or runtime topology bug is reproduced.

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

### P3 Optional

#### TB-006 Density polish

Residual visual polish only. Build37C / Build37D density behavior is already implemented.

#### TB-008 Hover Preview Polish

Base hover preview delivered in Build40. Grouped horizontal preview cards delivered in Build41A.
Core hover preview polish delivered in Build41B.

Remaining work:

* Hover close button
* Animated fade in / out
* Glass / transparency treatment (AllowsTransparency + DWM thumbnail validation)
* Dynamic grouped preview removal after WM_CLOSE

#### TB-009 Future UX — Window Chip Display Modes

Potential user-selectable display modes:

* icon only
* icon + title

### Next Build Candidates

#### P1 — TB-001 Runtime DPI hardening (partial)

Hover preview DPI coordinate handling hardened (f52f7b2). Build42A diagnostics added.
TB-001 is partially validated and remains a monitored technical limitation.

#### P2 — Runtime taskbar auto-hide overlap hardening

Verify TopBar coexistence with Windows taskbar auto-hide across edge cases and
monitor configurations.

#### P3 — Hover preview polish

TB-008 follow-up. Hover close button, fade, glass / transparency, dynamic grouped preview removal.

#### P3 — 10+ chip and reorder follow-up polish

Overflow, scroll, and drag/drop behavior under high chip counts and edge positions.

## Future Design Direction

### Hover Preview Roadmap — Build41+ Concept

#### Current state

Build40 delivers single-window DWM hover preview.
Build41A delivers grouped horizontal preview cards.
Build41B delivers core hover preview polish.

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

* Individual × close button
* Optional fade animation (DoubleAnimation on Opacity)
* Optional glass / transparency treatment (AllowsTransparency + DWM thumbnail validation)
* Dynamic grouped preview removal after WM_CLOSE

#### Priority intent

Hover Preview Polish core is complete as Build41B.
Close button remains gated for follow-up investigation and explicit approval.

---

### TB-001 Remaining Direction

Still open — not marked complete:

* Mixed-DPI behavior: per-monitor DPI divergence across multiple monitors
* Monitor hot-plug: disconnect / reconnect after startup
* Explicit per-monitor DPI awareness behavior
* Topology mutation edge cases
* Non-primary monitor DPI edge cases

Partial hardening delivered in f52f7b2 (hover preview coordinate fix, display-refresh teardown,
WM_DPICHANGED diagnostic logging). Build42A partially validated runtime scaling UX and confirmed
current system-DPI-aware behavior. Per-monitor DPI hardening should be investigated only if a
visual regression appears, mixed-DPI requirements increase, or topology bugs are reproduced.

---

## Known Problems

### KP-001 Runtime DPI runtime instability

Partially validated in Build42A. No immediate visual regression reproduced under current
system-DPI-aware mode; mixed-DPI, hot-plug, and per-monitor DPI behavior remain monitored risks.

### KP-002 Grouped multi-window close edge cases

Grouped close can still exhibit intermittent stale-handle or popup interaction edge cases.

### KP-003 Negative-origin monitor validation incomplete
