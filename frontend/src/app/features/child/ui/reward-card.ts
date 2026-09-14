import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

/** A single reward in the Belöningsbutik. Same tilted/wobbling pastel-card
 * language as ChildTaskCard. Shows the parent-uploaded photo when the reward
 * has one, so a child who can't read yet still recognizes what they're
 * choosing; falls back to a generic gift icon otherwise. */
@Component({
  selector: 'app-child-reward-card',
  imports: [TranslocoPipe],
  template: `
    <article
      [id]="cardId()"
      tabindex="-1"
      class="flex flex-col rounded-[18px] p-2.5 outline-none sm:rounded-[22px] sm:p-4 {{
        paletteClasses()
      }}"
      [style.rotate.deg]="tiltDeg()"
      [style.--tilt.deg]="tiltDeg()"
      [style.animation-name]="wobbling() ? 'child-card-wobble' : 'none'"
      [style.animation-duration.s]="0.9"
      [style.animation-timing-function]="'ease-in-out'"
      [style.animation-iteration-count]="1"
    >
      @if (imageUrl(); as url) {
        <img
          [src]="url"
          alt=""
          class="aspect-square w-full rounded-[12px] object-cover sm:aspect-[16/10] sm:rounded-[15px]"
        />
      } @else {
        <div
          class="flex aspect-square items-center justify-center rounded-[12px] border-2 border-dashed border-white/70 bg-white/40 sm:aspect-[16/10] sm:rounded-[15px]"
          aria-hidden="true"
        >
          <svg
            viewBox="0 0 24 24"
            class="size-6 text-child-text-secondary/50 sm:size-7"
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

      <div class="mt-2 flex items-start justify-between gap-1.5 sm:mt-3 sm:gap-2">
        <h3
          class="font-display text-[13px] leading-snug font-semibold text-child-text sm:text-[15px]"
        >
          {{ name() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-white/70 px-1.5 py-0.5 text-[11px] font-bold text-child-text sm:px-2 sm:text-xs"
          [attr.aria-label]="'child.common.pointsAria' | transloco: { points: pointsCost() }"
        >
          <svg
            viewBox="0 0 20 20"
            class="size-3 shrink-0"
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

      @if (description()) {
        <p
          class="mt-1 line-clamp-2 text-[12px] leading-4 text-child-text-secondary sm:mt-1.5 sm:text-[13px] sm:leading-5"
        >
          {{ description() }}
        </p>
      }

      <button
        type="button"
        (click)="requested.emit()"
        [disabled]="disabled() || busy()"
        [attr.aria-label]="'child.rewardCard.requestAria' | transloco: { name: name() }"
        class="mt-2.5 min-h-9 w-full rounded-full bg-[linear-gradient(180deg,var(--color-child-cta-from),var(--color-child-cta-to))] text-[13px] font-bold text-child-text shadow-[3px_4px_0_var(--color-child-cta-shadow)] transition active:translate-x-[2px] active:translate-y-[3px] active:shadow-[1px_2px_0_var(--color-child-cta-shadow)] disabled:pointer-events-none disabled:opacity-50 sm:mt-3.5 sm:min-h-11 sm:text-sm"
      >
        {{ (busy() ? 'child.rewardCard.requesting' : 'child.rewardCard.request') | transloco }}
      </button>
    </article>
  `,
})
export class ChildRewardCard {
  readonly cardId = input.required<string>();
  readonly name = input.required<string>();
  readonly description = input<string | null>(null);
  readonly pointsCost = input.required<number>();
  readonly imageUrl = input<string | null>(null);
  readonly palette = input<ChildCardPalette>('blue');
  readonly tiltDeg = input(0);
  readonly wobbling = input(false);
  readonly busy = input(false);
  readonly disabled = input(false);
  readonly requested = output<void>();

  paletteClasses(): string {
    return CHILD_CARD_PALETTE_CLASSES[this.palette()];
  }
}
