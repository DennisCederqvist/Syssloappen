# Syssloappen — Adult View Design Spec

Redesign target for the **adult-facing screens only**. The child view is
intentionally separate (big, bubbly, playful) and is out of scope — do not
change its style.

Reference mockups: `hem.png`, `sysslor.png` (this folder), and the live
Claude Design canvas: https://claude.ai/code/artifact/ee712515-0597-458b-8459-8c5850638308

## Why

Current adult UI reuses the child view's design system: oversized rounded
cards, ~20px+ corner radius, large fonts, a persistent branded hero header
repeated on every screen ("FAMILJEN" + house icon + big title + "Logga ut"
button). Feedback: childish, cramped, and paradoxically causes a lot of
scrolling because so much vertical space goes to decorative chrome before
real content appears.

Goal: same app, same accent color family, but the adult view should feel
calm, dense, and tool-like — efficiency over "fun."

## Color

| Token | Value | Use |
|---|---|---|
| `accent` | `#0d5c4f` | Primary buttons, active nav state, primary actions |
| `accent-dark` | `#0a4a40` | Text/icons on tinted accent backgrounds |
| `accent-soft` | `#e2efec` | Tinted backgrounds (badges, avatar tiles, secondary buttons) |
| `bg` | `#f6f7f7` | Page background |
| `surface` | `#ffffff` | Card/tile background |
| `border` | `#e6e8e7` | Card/tile border (1px) |
| `text-primary` | `#14181a` | Headlines, card titles |
| `text-secondary` | `#6b7370` | Body/meta text, subtitles |
| `text-muted-nav` | `#8b938f` | Inactive bottom-nav icons/labels |
| `danger-bg` | `#fdf3f1` | "Avslå" (deny) button background |
| `danger-border` | `#e6b3ac` | "Avslå" button border |
| `danger-text` | `#b5433a` | "Avslå" button text |
| `points-bg` | `#fbf1dd` | Points/star badge background |
| `points-text` | `#92650f` | Points/star badge text |

Keep `accent` as the one visual thread shared with the child view — don't
introduce a second brand color.

## Type scale

| Role | Size | Weight | Notes |
|---|---|---|---|
| Page title | 20–22px | 600 (semibold) | e.g. "Sysslor", "Belöningar" |
| Section header | 15–16px | 600 | e.g. "Behöver godkännas" |
| Card/tile title | 14–15px | 600 | |
| Body / description | 13–14px | 400 | line-height ~1.4 |
| Button label | 14px | 500 (medium) | never bold |
| Small meta (timestamps, badges, nav labels) | 11–12px | 500–600 | |

Buttons: text can be small, but the tap target must stay **≥40–44px tall**
regardless of label size.

## Shape & spacing

- Corner radius: **8px** everywhere (cards, buttons, inputs, badges). Not
  sharp, not bubbly — a deliberate step down from the old ~20px+ radius.
- Card padding: ~12px.
- Grid/section gap: ~8–10px between related items, ~18–22px between
  sections.
- Card border: 1px solid `border` token, no drop shadow (flat, calm).

## Layout patterns

1. **No persistent branded header.** Remove the "FAMILJEN" eyebrow + house
   icon + big hero title bar that used to repeat on every screen. Replace
   with a plain small page-title per screen (see Hem/Sysslor examples —
   Hem uses a welcome line instead of a page title since it *is* the
   home screen).
2. **No "Logga ut" button pinned per-screen.** Move it into the
   Inställningar (settings) menu. Login is persistent — no repeated
   re-auth for normal use.
   - Exception (not yet built, flag for later): sensitive actions like
     disabling a child profile or removing an adult should prompt a PIN
     or password confirmation, even though the session itself stays
     logged in.
3. **No redundant top-right icon on Hem.** Inställningar already lives in
   the bottom nav — don't duplicate it as a top-right cog on every page.
4. **2-column grid** for short, uniform "browse" tiles: child summary
   tiles, chore bank tiles. Each tile: title + one stat/badge, nothing
   more.
5. **Full-width single-column cards** for anything with a multi-line
   description plus two action buttons — approval cards
   ("Behöver godkännas", "Belöningsönskningar" / reward requests). Do not
   force these into the 2-column grid; they get cramped.
6. **Shrink hero/CTA blocks.** The old design spent ~30–40% of the
   viewport on a big rounded hero card before showing real content. New
   pattern: a plain title + a small inline action button (e.g. "+ Ny
   syssla" as a compact pill button, not a giant green block).
7. **Forms/modals become full-screen sheets** on mobile instead of
   floating cards over visible background content (applies to "Lägg till
   barn", "Skapa belöning", "Skapa syssla", etc. — not shown in the two
   reference mockups but should follow this pattern everywhere it
   applies).
8. **Bottom tab bar stays as-is structurally**: Hem, Sysslor, Belöningar,
   Inställningar — icon + small (11px) label, active state in `accent`,
   inactive in `text-muted-nav`.

## Reusable components to extract

Both reference screens are built from the same handful of repeating
patterns — build these once as shared components, then apply them across
every adult screen rather than redesigning each page individually:

- **Card** — surface + border + 8px radius + 12px padding container.
- **Tile** (2-column) — Card variant sized for the grid; icon/avatar +
  title + one stat line.
- **ApprovalCard** — Card variant: title + meta line + Godkänn/Avslå
  button row. Used identically for chore approvals and reward requests.
- **Badge** — small pill for points (`points-bg`/`points-text`) and count
  indicators (`accent-soft`/`accent-dark`).
- **PrimaryButton** / **DangerOutlineButton** / **SecondaryTintButton** —
  the three button treatments seen (solid accent "Godkänn"/"Tilldela sysslan",
  outlined red "Avslå", tinted-outline accent "Tilldela").
- **BottomNav** — the 4-item tab bar, shared across all adult screens.
- **PageHeader** — plain title + optional inline action button (replaces
  the old branded hero).

## Icons

Line/stroke-based SVG icons only (no emoji, no filled glyphs) — see the
two reference screens for the house/list/star/gear/pencil/plus icon style
already used (1.8–2px stroke weight, rounded caps/joins).

## Scope

Apply this system to every adult-facing screen in the app: Hem, Sysslor
(chore bank, create/assign), Belöningar (reward list, create), Barn och
konton (children list, add/manage child), Bjud in vuxen, Historik,
Inställningar, engångskod/device-pairing modals, and the login screen
(the "Vuxen" side of it — the "Barn" login tab is part of the child
experience and out of scope here). The child-facing app itself is
untouched.
