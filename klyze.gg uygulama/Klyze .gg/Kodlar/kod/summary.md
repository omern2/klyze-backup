# Klyze.gg WPF App — Session Summary

## Goal
- Fix AnalizPage drawing bug (lines/elo values missing on first open) and add animated tab bar with underline indicator.

## Constraints & Preferences
- Right bar (48 px): `#141414` sidebar‑color background, `#0FFFFFFF` left border, circular avatar (36 px) + bell icon (20 px, gray, no action yet).
- Profile panel slides in from right (200 ms) when avatar clicked; closes on outside click.
- Menu must match old app exactly: Home → Tools accordion (5 sub‑items) → [sep] → Play → Analiz → [sep] → Settings → Support → Info → [sep] → Timer (new). No profile section in sidebar.
- Profile avatar moved out of sidebar into top‑right of main content area (below min/close buttons).
- Active state: no label color change, only button background/border.
- Expand/Collapse uses old individual‑label if‑statements, not arrays.
- Tab bar design: full‑width horizontal bar, dark gray bg, rounded corners, thin border. Three tabs (İstatistikler, Maç Geçmişi, Canlı Maç) equally spaced. Active tab text white bold, others gray. Underline indicator slides between tabs with 200 ms cubic‑bezier animation. Content fades out/in on tab switch (150 ms).

## Progress
### Done
- Restored exact old sidebar structure (Tools accordion, Support button, old naming `MenuXxx`/`LblXxx`, old button order, inner‑Grid split).
- Removed profile section from sidebar.
- Added right bar (48 px) with `{StaticResource SidebarBg}`, border `#0FFFFFFF`, containing avatar + bell icon.
- Changed profile dropdown animation from vertical to horizontal (X slide, 20 px start → 0, 200 ms ease‑out).
- Changed dropdown positioning to right‑aligned (`Margin="0,56,48,0"`).
- Fixed code‑behind: `SetNavActive(btn, active)`, old event handlers (`MenuXxx_Click`, `ToolsMenu_Click`), old `ExpandSidebar`/`CollapseSidebar` with individual label ifs.
- Removed label foreground change in active state.
- Updated `GuncelleHesapBilgisi` / `ProfileLogout_Click` references from `ProfileAvatarImg` → `RightBarAvatarImg` / `ProfileRankIkon` → `RightBarRankIkon`.
- RootGrid now has 3 columns (sidebar, main, right bar 48 px). Header and overlays span 3 columns.
- Right bar background changed from `#0D0D0D` to `{StaticResource SidebarBg}` (user request).
- Fixed `EloAnimasyonuBaslat` to redraw grid lines + Y‑axis labels on each animation tick.
- **Rewrote PlayPage (Oyna) from scratch**: added `Helpers/CubicBezierEase.cs` (CSS cubic-bezier(0.34,1.56,0.64,1) implementation), rewrote `PlayPage.xaml`, `PlayPage.xaml.cs`, `PlayViewModel.cs` with full Firebase integration, player card, MAÇ BUL button, lobby matching, modal, and spring animations.
- Updated Firebase config in `FirebaseService.cs`: `ProjectId = "klyzegg"`, `ApiKey = "AIzaSyDIVzy4-HXXseudNlzQttP7wlZlTyrZCdE"`.
- Added tab bar to AnalizPage (İstatistikler, Maç Geçmişi, Canlı Maç tabs) with visibility‑binding and `TabCommand`.
- **Redesigned AnalizPage tab bar with animated underline indicator**: replaced DataTrigger button styling with `TabUnderline` Border + `TranslateTransform`. Underline slides to active tab (200 ms, `CubicBezierEase.Spring`). Content panels fade in on switch (150 ms). Panel size tracked via `TabBar_SizeChanged`.
- All builds succeed (0 errors; warnings only from pre‑existing ValorantViewModel).

### In Progress
- (none)

### Blocked
- (none)

## Key Decisions
- Tab underline uses `TranslateTransform` animated via code‑behind on `SelectedTab` property change, with `CubicBezierEase.Spring` (static instance) for spring‑like cubic‑bezier easing.
- Right bar background uses sidebar resource (`{StaticResource SidebarBg}` = `#141414`) to match user's "menü ile aynı renk" request.
- Grid/Y‑axis labels are redrawn inside the animation tick instead of moving them to a separate canvas layer – simplest fix for the disappearing‑lines bug.

## Relevant Files
- `Views\AnalizPage.xaml`: tab bar redesigned with `TabUnderline` Border + `TranslateTransform`, panels named `IstatistiklerPanel`, `MacGecmisiPanel`, `CanliMacPanel`.
- `Views\AnalizPage.xaml.cs`: `SekmeGecisAnimasyonu()` — underline slide + content fade; `TabBar_SizeChanged` — underline width/position init; `_oncekiSekme` tracking.
- `Helpers\CubicBezierEase.cs`: custom easing function; `Spring` static instance used for underline animation.
- `Views\PlayPage.xaml` / `PlayPage.xaml.cs` / `ViewModels\PlayViewModel.cs`: fully rewritten Oyna page.
- `Services\FirebaseService.cs`: updated with real Firebase project ID/API key.
- `MainWindow.xaml`: sidebar restored (lines ~448–556), right bar (lines ~1400–1430), header/overlays span 3 columns.
- `MainWindow.xaml.cs`: `RightBarAvatar_Click`, `SetNavActive`, old event handlers all updated.
