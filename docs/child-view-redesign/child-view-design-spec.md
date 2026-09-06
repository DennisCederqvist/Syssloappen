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

"Idag" (`child-home-page`) and "Belöningar" (`child-rewards-page`) are
rebuilt on the new system. "Önskningar" still uses the old ad-hoc styling +
the shared `AppBottomNav` — bringing it in line with this spec is the next
step, not yet done.

The nav's first item is "Sysslor" (not "Idag") with a clipboard-checkmark
icon (not a house) — renamed/re-iconed everywhere, including the legacy
`AppBottomNav` still serving Önskningar, so the label is consistent across
the whole child view even before every page is restyled.

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

| Token                                                  | Value                 | Use                                                                                                                                                                                                                                                                       |
| ------------------------------------------------------ | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `child-nav-bg`                                         | `#f4f8ff`             | Sidebar/bottom-nav background                                                                                                                                                                                                                                             |
| `child-bg`                                             | `#f9f6ef`             | Page background — a faint warm off-white, not pure white, so white cards/badges (points pill, motivation banner) read as distinct surfaces instead of melting into the page. Kept subtle: an earlier, noticeably darker attempt (`#f2ede1`) clashed with the pastel cards |
| `child-text`                                           | `#1f2a37`             | Headings, card titles                                                                                                                                                                                                                                                     |
| `child-text-secondary`                                 | `#4d5561`             | Body/meta text, subtitles (AA contrast against every card color)                                                                                                                                                                                                          |
| `child-accent`                                         | `#0b6e5a`             | Active nav icon/label (AA contrast against white/nav-bg)                                                                                                                                                                                                                  |
| `child-star-fill` / `child-star-stroke`                | `#ffc93c` / `#c99400` | Every star icon (points)                                                                                                                                                                                                                                                  |
| `child-cta-from` / `child-cta-to`                      | `#7fe0c8` / `#4fc9a8` | "Jag är klar!" button gradient                                                                                                                                                                                                                                            |
| `child-cta-shadow`                                     | `#3aa88d`             | That button's offset shadow                                                                                                                                                                                                                                               |
| `child-card-{blue,pink,yellow,peach,mint}` + `-shadow` | see `styles.css`      | Rotating task-card palette                                                                                                                                                                                                                                                |

All defined as `@theme` tokens in `frontend/src/styles.css`, kept separate
from the adult tokens the same way the adult redesign kept its tokens
separate from the (now legacy) tokens the child view used to share with it.

## Shape, shadow direction, and motion

- Corner radius: **~30px** on cards, fully pill-shaped (`rounded-full`) CTA
  buttons. Not 8px like the adult view — deliberately much softer.
- Every shadow simulates a light source from the top-left: offset
  **down-and-right** (`4px 6px 0 <color>`, not `0 6px 0`). Applies to every
  card and button in the child view, not just task cards.
- Task cards get a permanent tilt: a random magnitude (1.5–3°) and random
  sign per card, cached per assignment id in `child-home-page.ts` and applied
  as a plain inline `rotate` style. Deliberately random rather than
  alternating by grid position — alternating made every left-column card
  lean one way and every right-column card lean the other, which read as a
  mirrored, forced pattern rather than a scattered pile.
- Plus an idle "wobble" — a one-shot `child-card-wobble` CSS animation
  (`styles.css`, ~0.9s, two quick flicks off-rest and back) that a JS
  scheduler in `child-home-page.ts` triggers on a **random card at a random
  interval** (3–7s apart, averaging about one wobble every 5s) rather than a
  fixed per-card loop — it's meant to feel alive, not mechanical.
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
- `ChildRewardCard` — same tilted/wobbling pastel-card shape as
  `ChildTaskCard`, plus an image-placeholder tile (dashed border, generic
  gift icon) above the name/description. Rewards will eventually get a
  parent-uploaded photo so a child who can't read yet still recognizes what
  they're picking — that upload feature doesn't exist yet, this is just
  where the photo will go. The "Önska belöning" CTA disables (dimmed, not
  hidden) when the child can't afford it or a request is already in flight.
- `palette.ts` / `card-motion.ts` — the rotating 5-color palette and the
  random-tilt-plus-random-wobble scheduler are shared, not duplicated per
  card type. Any new child card type should reuse both rather than
  re-implementing them.

A celebratory approval animation (confetti + "Bra jobbat!") was prototyped
and removed again — there's no live push signal for "an adult just approved
this" yet, so it had nothing real to attach to. Revisit once that signal
exists.

## Haptics

`frontend/src/app/shared/haptics.ts` exposes `vibrateOnTap()` — feature-detects
`navigator.vibrate` and no-ops everywhere it's unsupported (all of iOS
Safari/WebKit, installed or not). Called once, on tapping "Jag är klar!".
Progressive enhancement only — no UI or messaging built around it.
