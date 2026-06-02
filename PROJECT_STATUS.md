# TopBar Project Status

## Latest

Latest commit:

677919f fix: harden runtime taskbar state refresh handling

Build35 completed and validated.

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

### P3 Optional

#### TB-005 Auto density mode

#### TB-006 Density polish

## Known Problems

### KP-001 Runtime DPI runtime instability

### KP-002 Grouped multi-window close edge cases

### KP-003 Negative-origin monitor validation incomplete
