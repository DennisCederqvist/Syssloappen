/** Shared 5-color rotating pastel palette for child-view cards (task cards,
 * reward cards, ...) — each with a matching down-and-right offset shadow. */
export type ChildCardPalette = 'blue' | 'pink' | 'yellow' | 'peach' | 'mint';

export const CHILD_CARD_PALETTES: ChildCardPalette[] = ['blue', 'pink', 'yellow', 'peach', 'mint'];

export const CHILD_CARD_PALETTE_CLASSES: Record<ChildCardPalette, string> = {
  blue: 'bg-child-card-blue shadow-[4px_6px_0_var(--color-child-card-blue-shadow)]',
  pink: 'bg-child-card-pink shadow-[4px_6px_0_var(--color-child-card-pink-shadow)]',
  yellow: 'bg-child-card-yellow shadow-[4px_6px_0_var(--color-child-card-yellow-shadow)]',
  peach: 'bg-child-card-peach shadow-[4px_6px_0_var(--color-child-card-peach-shadow)]',
  mint: 'bg-child-card-mint shadow-[4px_6px_0_var(--color-child-card-mint-shadow)]',
};
