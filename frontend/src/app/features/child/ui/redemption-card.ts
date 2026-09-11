import { Component, inject, input } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

export type ChildRedemptionCardStatus = 'Requested' | 'Approved' | 'Cancelled' | 'Delivered';

const STATUS_LABEL_KEYS: Record<ChildRedemptionCardStatus, string> = {
  Requested: 'child.redemptions.status.requested',
  Approved: 'child.redemptions.status.approved',
  Cancelled: 'child.redemptions.status.cancelled',
  Delivered: 'child.redemptions.status.delivered',
};

const STATUS_BANNER_CLASSES: Record<ChildRedemptionCardStatus, string> = {
  Requested: 'bg-amber-50 text-amber-900',
  Approved: 'bg-emerald-50 text-emerald-900',
  Delivered: 'bg-emerald-50 text-emerald-900',
  Cancelled: 'bg-red-50 text-red-900',
};

/** A wished-for reward and its status, in the same tilted/wobbling
 * pastel-card shape as ChildRewardCard (including the same photo/placeholder
 * handling — a wish is for the same reward, so the same image applies here
 * too) — but with a status banner where the CTA button would be, since
 * there's nothing left for the child to do on this card. */
@Component({
  selector: 'app-child-redemption-card',
  imports: [TranslocoPipe],
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
      @if (imageUrl(); as url) {
        <img
          [src]="url"
          alt=""
          class="aspect-[16/10] w-full rounded-[20px] object-cover"
        />
      } @else {
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
      }

      <div class="mt-4 flex items-start justify-between gap-3">
        <h3 class="font-display text-[19px] leading-snug font-semibold text-child-text">
          {{ name() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-white/70 px-2.5 py-1 text-sm font-bold text-child-text"
          [attr.aria-label]="'child.common.pointsAria' | transloco: { points: pointsCost() }"
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

      <div class="mt-4 rounded-2xl p-4 {{ bannerClasses() }}">
        <p class="text-sm font-bold">{{ statusLabel() }}</p>
        @if (comment()) {
          <p class="mt-1 text-[15px] leading-6">{{ comment() }}</p>
        }
      </div>
    </article>
  `,
})
export class ChildRedemptionCard {
  private readonly transloco = inject(TranslocoService);
  readonly cardId = input.required<string>();
  readonly name = input.required<string>();
  readonly pointsCost = input.required<number>();
  readonly status = input.required<ChildRedemptionCardStatus>();
  readonly comment = input<string | null>(null);
  readonly imageUrl = input<string | null>(null);
  readonly palette = input<ChildCardPalette>('blue');
  readonly tiltDeg = input(0);
  readonly wobbling = input(false);

  paletteClasses(): string {
    return CHILD_CARD_PALETTE_CLASSES[this.palette()];
  }

  statusLabel(): string {
    return this.transloco.translate(STATUS_LABEL_KEYS[this.status()]);
  }

  bannerClasses(): string {
    return STATUS_BANNER_CLASSES[this.status()];
  }
}
