# TopBar Project Status

## Latest

Latest commit:

9ef3386 fix: harden close diagnostics and window enumeration

Build38A completed, validated, and pushed to origin/master.

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

## Key Findings

- UW 100% - Large (40 DIP) feels best.
- Launch DPI behavior mostly OK.
- Runtime DPI change while running still needs hardening.
- Taskbar parity design note: Windows taskbar behavior cannot be perfectly replicated via window style inspection alone. Shell-managed taskbar state may diverge from EnumWindows eligibility.

## Active Backlog

TB-005 is retained as the historical ID for completed Build37D Auto Density and is not reused below.

### P1 Critical

#### TB-001 Runtime DPI hardening

Runtime DPI changes while running still need validation / hardening.

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

#### TB-008 Future UX — Hover Preview / Taskbar-style Preview

Investigate taskbar-style hover preview UX and a possible preview close button.

Not part of Build38A.

#### TB-009 Future UX — Window Chip Display Modes

Potential user-selectable display modes:

* icon only
* icon + title

## Known Problems

### KP-001 Runtime DPI runtime instability

### KP-002 Grouped multi-window close edge cases

Grouped close can still exhibit intermittent stale-handle or popup interaction edge cases.

### KP-003 Negative-origin monitor validation incomplete
