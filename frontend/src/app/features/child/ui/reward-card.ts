import { Component, input, output } from '@angular/core';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

/** A single reward in the Belöningsbutik. Same tilted/wobbling pastel-card
 * language as ChildTaskCard, plus an image-placeholder tile: rewards will
 * later get a parent-uploaded photo so a child who can't read yet still
 * recognizes what they're choosing, but that upload feature doesn't exist
 * yet — this generic gift icon is a stand-in for where that photo will go. */
@Component({
  selector: 'app-child-reward-card',
  template: `
    <article
      [id]="cardId()"
      tabindex="-1"
      class="flex flex-col rounded-[30px] p-5 outline-none sm:p-6 {{ paletteClasses() }}"
      [style.rotate.deg]="tiltDeg()"
      [style.--tilt.deg]="tiltDeg()"
      [style.animation-name]="wobbling() ? 'child-card-wobble' : 'none'"
      [style.animation-duration.s]="0.9"
      [style.animation-timing-function]="'ease-in-out'"
      [style.animation-iteration-count]="1"
    >
      <div
        class="flex aspect-[16/10] items-center justify-center rounded-[20px] border-2 border-dashed border-white/70 bg-white/40"
        aria-hidden="true"
      >
        <svg
          viewBox="0 0 24 24"
          class="size-10 text-child-text-secondary/50"
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

      <div class="mt-4 flex items-start justify-between gap-3">
        <h3 class="font-display text-[19px] leading-snug font-semibold text-child-text">
          {{ name() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-white/70 px-2.5 py-1 text-sm font-bold text-child-text"
          [attr.aria-label]="pointsCost() + ' poäng'"
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

      @if (description()) {
        <p class="mt-2 text-[15px] leading-6 text-child-text-secondary">{{ description() }}</p>
      }

      <button
        type="button"
        (click)="requested.emit()"
        [disabled]="disabled() || busy()"
        [attr.aria-label]="'Önska ' + name()"
        class="mt-5 min-h-14 w-full rounded-full bg-[linear-gradient(180deg,var(--color-child-cta-from),var(--color-child-cta-to))] text-base font-bold text-child-text shadow-[4px_6px_0_var(--color-child-cta-shadow)] transition active:translate-x-[2px] active:translate-y-[3px] active:shadow-[1px_2px_0_var(--color-child-cta-shadow)] disabled:pointer-events-none disabled:opacity-50"
      >
        {{ busy() ? 'Önskar…' : 'Önska belöning' }}
      </button>
    </article>
  `,
})
export class ChildRewardCard {
  readonly cardId = input.required<string>();
  readonly name = input.required<string>();
  readonly description = input<string | null>(null);
  readonly pointsCost = input.required<number>();
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
