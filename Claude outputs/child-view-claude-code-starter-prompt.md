# Starter prompt for Claude Code — Child ("Idag") view redesign

Copy everything below into Claude Code, with the attached reference screenshot (the "Merged / Mascot + Bubbly" mockup) included.

---

I want to redesign the child-facing side of Syssloappen (the "Idag" screen and the rest of the child nav) to match the attached mockup. This is a chore app for kids roughly age 6–10 (broad enough that a 10–12 year old wouldn't reject it as babyish). Primary device is tablet, but build mobile-first as usual — the tablet layout in the mockup is the primary breakpoint to match first.

## Overall look and feel

- Playful, bubbly, rounded — big rounded corners (~30px) on cards, pill-shaped buttons.
- Font pairing: "Fredoka" (500/600/700) for headings/display text, "Inter" for body text. Load both from Google Fonts.
- White page background (not tinted).
- Sidebar nav background: light blue tint (`#f4f8ff`), NOT white, so it separates visually from the white content area. Active nav item ("Idag") gets a white pill highlight inside the sidebar.
- Every card and button has a soft 3D "gumdrop" feel — not flat material design.

## Header (top of "Idag" page)

- Avatar placeholder (colored rounded-square icon for now, will later be a real uploaded photo of the child) + the child's actual name: "Hej Benny!" (bold, large, ~26px).
- Subtitle under the name: "Detta är dagens sysslor" (friendly, not the generic "Min dag" / "Mina sysslor").
- No "Logga ut" anywhere in the child view — logout is parent-managed from their own side, not exposed to the child.
- Points display, top right: a white rounded "pill" (border-radius ~18px, padding ~10px 18px, soft shadow `0 3px 0 rgba(0,0,0,0.06)`) containing a **gold** star icon (fill `#ffc93c`, stroke `#c99400`) and the point total in **black** bold text (e.g. `74`). No "poäng" label text next to it — just the icon + number. Important: it must look like an inert display/readout, NOT like a clickable button (no hover states, no border, no drop-shadow that reads as "raised button").
- A friendly motivational line below the header, in its own soft white rounded pill: e.g. "Du har 2 sysslor kvar idag — du fixar det!" with a small smiley/mascot icon on the left.

## Points / rewards language

- Do NOT use XP, "level up", or any gamer-leveling language anywhere. This is a "do chores → earn points → spend points on rewards" model, not an RPG leveling system. Points are just points.

## Task cards ("Idag" grid)

- 2-column grid of task cards (can wrap to more rows as needed).
- **Each card gets a distinct pastel background color** — do not make them all the same color. Use a rotating palette of at least 5 colors across the set of cards a child might see:
  - Blue: `#eaf6ff` (card shadow `#cbe6f7`)
  - Pink: `#ffe3e3` (card shadow `#f0c4c4`)
  - Yellow: `#fff3d6` (card shadow `#f0dfab`)
  - Peach: `#fff1de` (card shadow `#f0dcc0`)
  - Mint: `#e2efec` (card shadow `#c9ded8`, roughly)
- Each card has a slight **permanent tilt/skew** (small random rotation, e.g. -2° to +2°, alternating direction card to card) — gives a hand-placed, non-grid-perfect feel.
- Each card also does a **subtle idle "wobble" animation** — a small rotation wiggle back to its resting tilt, playing every ~10–20 seconds. Stagger the animation delay per card (e.g. 0s, 4s, 6s, 7s...) so cards don't wobble in sync with each other — it should feel organic, not choreographed. Keep the wobble subtle and non-distracting (a few degrees max, not attention-grabbing).
- Card content: task title (bold, ~19px), a small star+point-value badge in the top-right corner of the card, a one-line description in muted body text, and a big pill-shaped "Jag är klar!" button at the bottom (green gradient, `linear-gradient(180deg, #7fe0c8, #4fc9a8)`).
- **Drop shadows should NOT sit straight underneath elements.** Simulate a light source from the top-left: shadows on cards and on the "Jag är klar!" buttons should be offset down-and-to-the-right (e.g. `4px 6px 0 <shadow-color>` instead of `0 6px 0 <shadow-color>`). Apply this consistently to every card and button shadow in the child view, not just this screen.

## Celebration / approval animation

- When a task gets approved (or as a demo trigger for now), play a small celebratory animation: a handful of confetti/star/circle shapes bursting outward and fading, plus a "Bra jobbat!" message in a white rounded pill with the gold star icon.
- No sound effects needed yet (a trumpet/fanfare sound may be added later — don't build audio now).
- Keep the animation quick and non-blocking — it shouldn't force the user to wait or dismiss anything.

## Haptic feedback (nice-to-have, not a hard requirement)

- On supporting devices/browsers, trigger a short haptic vibration (`navigator.vibrate(...)`) when the child taps "Jag är klar!". This only works on Android/Chrome (including installed PWA) — it is **not** supported on iOS Safari at all, including Chrome on iOS (still WebKit under the hood), even as an installed PWA. Implement it as a progressive enhancement (feature-detect `navigator.vibrate` and no-op if unavailable) — don't build any UI or messaging around it, and don't treat it as a core feature.

## Sidebar navigation

Icons + labels, vertically stacked, centered:
- Idag (home icon) — active by default
- Belöningar (star/badge icon)
- Önskningar (heart icon)
- (spacer pushes the rest down)
- Inställningar (gear icon)

No "Logga ut" item in this list at all.

## Other child-facing pages to bring in line with this style (once "Idag" is solid)

- **Belöningar**: reward items should each show an icon/illustration (not just text) — later this becomes a parent-uploaded photo per reward, but for now use a placeholder icon per reward type.
- **Önskningar**: show 6 wish items in a clean 2-column grid (not 5) — status per item (e.g. "Utlämnad" / "Avslag") should be visible on the card.
- **Inställningar**: this is also where "Senast godkända" (recently approved tasks) now lives — it's been moved off the main "Idag" page since kids won't visit it regularly, but it should still be reachable from here.

## Reference

The attached mockup screenshot is the target look — match its colors, shapes, spacing, and card variety as closely as possible. If anything above conflicts with the screenshot, the screenshot wins on visual details (exact colors/shapes); this text is here mainly to explain the *behavior* (wobble, shadow direction, haptics, no-XP-language) that a static screenshot can't show.
