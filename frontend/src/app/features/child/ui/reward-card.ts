import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ChildCardPalette } from './palette';
import { ChildRewardBaseCard } from './reward-base-card';

/** A single reward in the Belöningsbutik. Same tilted/wobbling pastel-card
 * language as ChildTaskCard. Shows the parent-uploaded photo when the reward
 * has one, so a child who can't read yet still recognizes what they're
 * choosing; falls back to a generic gift icon otherwise. */
@Component({
  selector: 'app-child-reward-card',
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
      @if (description()) {
        <p class="mt-2 line-clamp-2 text-sm leading-5 text-child-text-secondary">
          {{ description() }}
        </p>
      }

      <button
        type="button"
        (click)="requested.emit()"
        [disabled]="disabled() || busy()"
        [attr.aria-label]="'child.rewardCard.requestAria' | transloco: { name: name() }"
        class="mt-3.5 min-h-11 w-full rounded-full bg-[linear-gradient(180deg,var(--color-child-cta-from),var(--color-child-cta-to))] text-sm font-bold text-child-text shadow-[3px_4px_0_var(--color-child-cta-shadow)] transition active:translate-x-[2px] active:translate-y-[3px] active:shadow-[1px_2px_0_var(--color-child-cta-shadow)] disabled:pointer-events-none disabled:opacity-50"
      >
        {{ (busy() ? 'child.rewardCard.requesting' : 'child.rewardCard.request') | transloco }}
      </button>
    </app-child-reward-base-card>
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
}
