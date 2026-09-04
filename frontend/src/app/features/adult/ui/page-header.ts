import { Component, input } from '@angular/core';

/** Plain title + optional subtitle + optional projected inline action button.
 * Replaces the old branded hero header on every adult screen. */
@Component({
  selector: 'app-adult-page-header',
  template: `
    <header class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <h1 class="text-[21px] leading-tight font-semibold text-adult-text">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="mt-1 text-sm text-adult-text-secondary">{{ subtitle() }}</p>
        }
      </div>
      <ng-content select="[headerAction]" />
    </header>
  `,
})
export class AdultPageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
}
