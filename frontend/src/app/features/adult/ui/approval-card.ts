import { Component, input } from '@angular/core';
import { AdultBadge } from './badge';

/** Card variant: title + meta line + optional badge + a projected action-button
 * row. Used identically for chore approvals and reward requests. */
@Component({
  selector: 'app-adult-approval-card',
  imports: [AdultBadge],
  template: `
    <article class="rounded-lg border border-adult-border bg-adult-surface p-3">
      <div class="flex items-start justify-between gap-3">
        <div class="min-w-0 flex-1">
          <h3 class="text-[15px] font-semibold text-adult-text">{{ title() }}</h3>
          @if (meta()) {
            <p class="mt-0.5 text-sm text-adult-text-secondary">{{ meta() }}</p>
          }
        </div>
        @if (badgeValue() !== null) {
          <app-adult-badge [variant]="badgeVariant()" [value]="badgeValue()!" />
        }
      </div>
      <div class="mt-3 flex flex-wrap gap-2">
        <ng-content />
      </div>
    </article>
  `,
})
export class AdultApprovalCard {
  readonly title = input.required<string>();
  readonly meta = input<string | null>(null);
  readonly badgeValue = input<number | string | null>(null);
  readonly badgeVariant = input<'points' | 'count'>('points');
}
