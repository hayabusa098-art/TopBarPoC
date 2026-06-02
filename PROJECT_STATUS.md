# TopBar Project Status

## Latest

Latest commit:

0700358 feat: add glass minimal ui refresh and balanced chip density

Build37 completed, validated, and pushed to origin/master.

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

### Follow-up — Build37D (Auto Density)

Tracked canonically as TB-005 Build37D — Auto Density.

---

### Design Gaps / Not Yet Matching Mock

Still behind the Glass Minimal target:

1. True glass material

* no Acrylic/Mica/backdrop blur yet
* current implementation uses fake glass

2. Auto density system

* no automatic Expanded/Balanced/Compact switching yet

3. Typography

* spec: Manrope / JetBrains Mono
* current: Segoe UI

4. Status cluster completeness

* CPU/MEM / Wi-Fi / Battery design not fully matched

5. Launcher / pinned app treatment

* not fully aligned with mock

6. Overflow visual design

* compact collapse behavior not finalized

7. Adaptive contrast

* wallpaper brightness adaptation not implemented

8. DPI/material scaling polish

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

## Key Findings

- UW 100% - Large (40 DIP) feels best.
- Launch DPI behavior mostly OK.
- Runtime DPI change while running still needs hardening.

## Active Backlog

### P1 Critical

#### TB-001 Runtime DPI hardening

Runtime DPI changes while running still need validation / hardening.

#### TB-002 Multi-monitor complete validation

Negative-origin, vertical stack, non-primary, runtime layout changes.

### P2 Important

#### TB-003 Grouped multi-window close hardening

Multi-tab / grouped close handling. Stale handle risk. Sequential close edge cases. Close semantics ambiguity.

#### TB-004 Context actions expansion

Pin / Hide / Remove candidates.

#### TB-005 Build37D — Auto Density

Adaptive density switching.

Modes:

* Expanded
* Balanced
* Compact

Potential inputs:

* availableWidth
* chipCount
* overflow pressure

Behavior:

* low pressure / wide layout → Expanded or Balanced
* crowded layout → Compact

### P3 Optional

#### TB-006 Density polish

## Known Problems

### KP-001 Runtime DPI runtime instability

### KP-002 Grouped multi-window close edge cases

### KP-003 Negative-origin monitor validation incomplete
