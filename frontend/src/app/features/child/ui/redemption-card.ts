import { Component, inject, input } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ChildCardPalette } from './palette';
import { ChildRewardBaseCard } from './reward-base-card';

type ChildRedemptionCardStatus = 'Requested' | 'Approved' | 'Cancelled' | 'Delivered';

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
  imports: [TranslocoPipe, ChildRewardBaseCard],
  template: `
    <app-child-reward-base-card
      [cardId]="cardId()"
      [name]="name()"
      [pointsCost]="pointsCost()"
      [pointsAriaLabel]="'child.common.pointsAria' | transloco: { points: pointsCost() }"
      [imageUrl]="imageUrl()"
      [palette]="palette()"
      [tiltDeg]="tiltDeg()"
      [wobbling]="wobbling()"
    >
      <div class="mt-3 rounded-2xl p-3 {{ bannerClasses() }}">
        <p class="text-sm font-bold">{{ statusLabel() }}</p>
        @if (comment()) {
          <p class="mt-1 text-sm leading-5">{{ comment() }}</p>
        }
      </div>
    </app-child-reward-base-card>
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

  statusLabel(): string {
    return this.transloco.translate(STATUS_LABEL_KEYS[this.status()]);
  }

  bannerClasses(): string {
    return STATUS_BANNER_CLASSES[this.status()];
  }
}
