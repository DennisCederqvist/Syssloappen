# Syssloappen — Child View Design Spec

Redesign target for the **child-facing screens only** ("Idag", "Belöningar",
"Önskningar", "Inställningar"). The adult view is intentionally separate
(calm, dense, tool-like — see `docs/adult-view-redesign/adult-view-design-spec.md`)
and is out of scope here — do not change its style.

Reference mockup: `docs/barnvy mockup.png` — target audience is roughly age
6–10, broad enough that a 10–12 year old wouldn't reject it as babyish.
Primary device is tablet; built mobile-first as usual, with the mockup's
tablet layout as the primary breakpoint to match.

## Status

"Idag" (`child-home-page`) is rebuilt on the new system. "Belöningar" and
"Önskningar" still use the old ad-hoc styling + the shared `AppBottomNav` —
bringing them in line with this spec is the next step, not yet done.

## Why

The child view previously reused generic Tailwind utility colors (amber/red/
emerald-50) with no shared component layer — every page hand-rolled its own
cards. Goal: a real bubbly, "gumdrop" design system (big rounded corners,
pill buttons, pastel cards with a permanent tilt) that a 6–10 year old finds
fun without reading as a leveling/gamer app — points are just points, no XP
or "level up" language anywhere.

## Fonts

- **Fredoka** (500/600/700) — headings/display text (`font-display` utility).
- **Inter** — body text.
- Both loaded from Google Fonts in `frontend/src/index.html`.

## Color

| Token                                                  | Value                 | Use                                                              |
| ------------------------------------------------------ | --------------------- | ---------------------------------------------------------------- |
| `child-nav-bg`                                         | `#f4f8ff`             | Sidebar/bottom-nav background (page itself stays white)          |
| `child-bg`                                             | `#ffffff`             | Page background                                                  |
| `child-text`                                           | `#1f2a37`             | Headings, card titles                                            |
| `child-text-secondary`                                 | `#4d5561`             | Body/meta text, subtitles (AA contrast against every card color) |
| `child-accent`                                         | `#0b6e5a`             | Active nav icon/label (AA contrast against white/nav-bg)         |
| `child-star-fill` / `child-star-stroke`                | `#ffc93c` / `#c99400` | Every star icon (points)                                         |
| `child-cta-from` / `child-cta-to`                      | `#7fe0c8` / `#4fc9a8` | "Jag är klar!" button gradient                                   |
| `child-cta-shadow`                                     | `#3aa88d`             | That button's offset shadow                                      |
| `child-card-{blue,pink,yellow,peach,mint}` + `-shadow` | see `styles.css`      | Rotating task-card palette                                       |

All defined as `@theme` tokens in `frontend/src/styles.css`, kept separate
from the adult tokens the same way the adult redesign kept its tokens
separate from the (now legacy) tokens the child view used to share with it.

## Shape, shadow direction, and motion

- Corner radius: **~30px** on cards, fully pill-shaped (`rounded-full`) CTA
  buttons. Not 8px like the adult view — deliberately much softer.
- Every shadow simulates a light source from the top-left: offset
  **down-and-right** (`4px 6px 0 <color>`, not `0 6px 0`). Applies to every
  card and button in the child view, not just task cards.
- Task cards get a permanent tilt (`--tilt`, roughly ±2°, alternating per
  card) plus a subtle idle "wobble" — a `child-card-wobble` keyframe
  (`styles.css`) that flicks a couple degrees off-rest once per ~15s cycle,
  staggered per card via `animation-delay` so cards don't move in sync.
- No XP/leveling language. "Points" only.

## Components (`frontend/src/app/features/child/ui/`)

- `ChildSideNav` — bottom tab bar on mobile, light-blue sidebar with a
  decorative avatar mark + pinned gear (Inställningar) on `md:` and up. No
  "Logga ut" — the child view never exposes logout, it's parent-managed.
- `ChildPageHeader` — avatar placeholder + "Hej {name}!" + subtitle, plus the
  points pill (top-right). The points pill is a plain white readout, never a
  button — no hover state, no border, no button-style shadow.
- `ChildTaskCard` — the rotating-palette, tilted, wobbling "to-do" card with
  the "Jag är klar!" CTA. Also covers `NeedsRedo` (still actionable) with an
  adult-comment banner.
- `ChildStatusCard` — `PendingApproval` (waiting on review, no CTA).
- `ChildCelebration` — confetti burst + "Bra jobbat!" pill, triggered via
  `.play()`. Quick (~1.7s) and non-blocking. No sound (a fanfare may be
  added later). Wired to a demo button on Idag for now — there's no live
  push signal for "an adult just approved this," so it can't fire
  automatically yet.

## Haptics

`frontend/src/app/shared/haptics.ts` exposes `vibrateOnTap()` — feature-detects
`navigator.vibrate` and no-ops everywhere it's unsupported (all of iOS
Safari/WebKit, installed or not). Called once, on tapping "Jag är klar!".
Progressive enhancement only — no UI or messaging built around it.
