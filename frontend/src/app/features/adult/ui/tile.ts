import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Card variant sized for a 2-column grid: optional avatar/icon + title, plus a
 * projected stat line/body. Renders as a link when `routerLink` is set. */
@Component({
  selector: 'app-adult-tile',
  imports: [NgTemplateOutlet, RouterLink],
  template: `
    <ng-template #body>
      <div class="flex items-start justify-between gap-2">
        <div class="flex min-w-0 items-center gap-2">
          @if (avatarLabel()) {
            <span
              class="grid size-9 shrink-0 place-items-center rounded-full bg-adult-accent-soft text-sm font-semibold text-adult-accent-dark"
              aria-hidden="true"
              >{{ avatarLabel() }}</span
            >
          }
          <span class="min-w-0 flex-1 text-[15px] leading-snug font-semibold text-adult-text">{{
            title()
          }}</span>
        </div>
        <ng-content select="[tileAction]" />
      </div>
      <div class="mt-2">
        <ng-content />
      </div>
    </ng-template>
    @if (routerLink()) {
      <a
        [routerLink]="routerLink()"
        class="block rounded-lg border border-adult-border bg-adult-surface p-3 no-underline transition hover:border-adult-accent/40"
      >
        <ng-container *ngTemplateOutlet="body" />
      </a>
    } @else {
      <div class="rounded-lg border border-adult-border bg-adult-surface p-3">
        <ng-container *ngTemplateOutlet="body" />
      </div>
    }
  `,
})
export class AdultTile {
  readonly title = input.required<string>();
  readonly avatarLabel = input<string | null>(null);
  readonly routerLink = input<string | unknown[] | null>(null);
}
