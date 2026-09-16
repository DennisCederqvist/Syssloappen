import { Component, input } from '@angular/core';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

/** Shared tilted/wobbling pastel-card shell (wrapper, photo/placeholder-gift-icon
 * block, name + points-badge header) used by ChildRewardCard and
 * ChildRedemptionCard — a wish is for the same reward, so the same image
 * handling applies to both. The footer (CTA button vs. status banner) is
 * projected in by the caller via <ng-content>. */
@Component({
  selector: 'app-child-reward-base-card',
  template: `
    <article
      [id]="cardId()"
      tabindex="-1"
      class="flex flex-col rounded-[22px] p-3.5 outline-none sm:p-4 {{ paletteClasses() }}"
      [style.rotate.deg]="tiltDeg()"
      [style.--tilt.deg]="tiltDeg()"
      [style.animation-name]="wobbling() ? 'child-card-wobble' : 'none'"
      [style.animation-duration.s]="0.9"
      [style.animation-timing-function]="'ease-in-out'"
      [style.animation-iteration-count]="1"
    >
      @if (imageUrl(); as url) {
        <img [src]="url" alt="" class="aspect-[16/8] w-full rounded-[15px] object-cover" />
      } @else {
        <div
          class="flex aspect-[16/8] items-center justify-center rounded-[15px] border-2 border-dashed border-white/70 bg-white/40"
          aria-hidden="true"
        >
          <svg
            viewBox="0 0 24 24"
            class="size-8 text-child-text-secondary/50"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <rect x="3" y="8" width="18" height="13" rx="1.5" />
            <path d="M3 12h18" />
            <path d="M12 8v13" />
            <path
              d="M12 8H8.5a2 2 0 1 1 0-4c1.5 0 2.7 1.2 3.5 4zM12 8h3.5a2 2 0 1 0 0-4c-1.5 0-2.7 1.2-3.5 4z"
            />
          </svg>
        </div>
      }

      <div class="mt-3 flex items-start justify-between gap-2">
        <h3 class="font-display text-[17px] leading-snug font-semibold text-child-text">
          {{ name() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-white/70 px-2.5 py-0.5 text-sm font-bold text-child-text"
          [attr.aria-label]="pointsAriaLabel()"
        >
          <svg
            viewBox="0 0 20 20"
            class="size-3.5 shrink-0"
            fill="var(--color-child-star-fill)"
            stroke="var(--color-child-star-stroke)"
            stroke-width="1"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path
              d="M10 1.5l2.6 5.3 5.9.85-4.27 4.16 1.01 5.87L10 14.9l-5.24 2.78 1.01-5.87L1.5 7.65l5.9-.85z"
            />
          </svg>
          {{ pointsCost() }}
        </span>
      </div>

      <ng-content />
    </article>
  `,
})
export class ChildRewardBaseCard {
  readonly cardId = input.required<string>();
  readonly name = input.required<string>();
  readonly pointsCost = input.required<number>();
  readonly pointsAriaLabel = input.required<string>();
  readonly imageUrl = input<string | null>(null);
  readonly palette = input<ChildCardPalette>('blue');
  readonly tiltDeg = input(0);
  readonly wobbling = input(false);

  paletteClasses(): string {
    return CHILD_CARD_PALETTE_CLASSES[this.palette()];
  }
}
