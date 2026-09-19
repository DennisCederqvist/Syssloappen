import { Component, input } from '@angular/core';
import { ChildImagePlaceholder } from './image-placeholder';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

/** Shared tilted/wobbling pastel-card shell (wrapper, photo/placeholder-gift-icon
 * block, name + points-badge header) used by ChildRewardCard and
 * ChildRedemptionCard — a wish is for the same reward, so the same image
 * handling applies to both. The footer (CTA button vs. status banner) is
 * projected in by the caller via <ng-content>. */
@Component({
  selector: 'app-child-reward-base-card',
  imports: [ChildImagePlaceholder],
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
        <app-child-image-placeholder kind="reward" />
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
