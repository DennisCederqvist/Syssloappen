# Starter prompt for Claude Code (VS Code)

Paste this into Claude Code once you've opened the Syssloappen project folder:

---

We're redesigning the ADULT-facing side of this app only (there's a
separate child view — don't touch its styling, components, or routes).

I've attached two reference files in `docs/adult-view-redesign/`:
- `adult-view-design-spec.md` — the full design spec: exact colors, type
  scale, spacing, corner radius, and the layout rules to follow.
- `hem.png` and `sysslor.png` — reference mockups of the two screens we
  designed first (adult home/overview, and the chore/"Sysslor" screen).

Please read the spec file first, then look at the two reference images.

Step 1: Look at how the current adult screens are structured in the
frontend code (find the relevant components/pages) and identify the
repeating UI patterns already listed in the spec's "Reusable components"
section (Card, Tile, ApprovalCard, Badge, buttons, BottomNav, PageHeader).

Step 2: Build/refactor those as shared, reusable components matching the
spec exactly (colors, 8px radius, type scale, spacing) — don't hand-roll
one-off styles per page.

Step 3: Apply the new components to rebuild the "Hem" (home/overview)
screen first, matching `hem.png` as closely as possible using our real
data/state instead of the mockup's placeholder content. Stop after this
screen and show me the result before continuing — I want to check it
against the mockup before we roll it out further.

Once I approve Hem, we'll go screen by screen through the rest of the
adult view (Sysslor, Belöningar, Barn och konton, Bjud in vuxen, Historik,
Inställningar, the device-pairing/engångskod modals, and the adult login
screen) using the same shared components, applying the spec's rules
throughout — especially: no persistent branded header, "Logga ut" moved
into Inställningar, persistent login, 2-column grids for browse tiles vs.
full-width cards for approval/action cards, and full-screen sheets instead
of floating modal cards for forms.

Don't change any backend logic, data models, or the child view — this is
a frontend/styling pass on the adult UI only.

---
