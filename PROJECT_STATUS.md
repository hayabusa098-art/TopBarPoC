# TopBar Project Status

## Latest

Latest commits:

f52f7b2 fix: harden hover preview DPI coordinate handling
20f0724 fix: resolve chip drag/drop hit-test dead zone in gap column
41578a0 feat: add DWM hover preview for single window chips

Build40 + DPI hardening completed, validated, and pushed to origin/master.

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
- Grouped chip preview: deferred (IsGrouped guard in place as hookup point)

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

* Grouped chip preview (multiple HWNDs per chip)
* Hover close button (requires WS_EX_NOACTIVATE)
* Aspect ratio preservation (DwmQueryThumbnailSourceSize)
* Animated fade in / out
* Glass and transparency treatment (AllowsTransparency + DWM thumbnail validation)

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

## Key Findings

- UW 100% - Large (40 DIP) feels best.
- Launch DPI behavior mostly OK.
- Runtime DPI change while running still needs hardening.
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

Remaining: full runtime DPI change validation (monitor hot-plug, scaling change while running,
multi-monitor DPI divergence). Not yet tested under live DPI change conditions.

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

Base hover preview delivered in Build40. Remaining work:

* Grouped chip preview (multiple HWNDs — show last-active or stacked mini-previews)
* Hover close button (requires WS_EX_NOACTIVATE on preview HWND)
* Aspect ratio preservation (DwmQueryThumbnailSourceSize)
* Animated fade in / out
* Glass / transparency treatment (AllowsTransparency + DWM thumbnail validation)

#### TB-009 Future UX — Window Chip Display Modes

Potential user-selectable display modes:

* icon only
* icon + title

### Next Build Candidates

#### P1 — TB-001 Runtime DPI hardening (partial)

Hover preview DPI coordinate handling hardened (f52f7b2). Diagnostic logging added.
Full live DPI change validation still required.

#### P2 — Runtime taskbar auto-hide overlap hardening

Verify TopBar coexistence with Windows taskbar auto-hide across edge cases and
monitor configurations.

#### P3 — Hover preview polish / grouped chip previews

TB-008 follow-up. Grouped chip preview, hover close button, aspect ratio, fade.

#### P3 — 10+ chip and reorder follow-up polish

Overflow, scroll, and drag/drop behavior under high chip counts and edge positions.

## Known Problems

### KP-001 Runtime DPI runtime instability

### KP-002 Grouped multi-window close edge cases

Grouped close can still exhibit intermittent stale-handle or popup interaction edge cases.

### KP-003 Negative-origin monitor validation incomplete
