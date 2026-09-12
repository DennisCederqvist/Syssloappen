import { Component, input } from '@angular/core';

/** Branded green header used on every adult screen: title + optional subtitle,
 * an optional projected inline action button, and the Sysslo logo mark as a
 * color splash on the right — so every page carries the same visual anchor. */
@Component({
  selector: 'app-adult-page-header',
  template: `
    <header
      class="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-adult-accent px-5 py-5 text-white sm:px-6 sm:py-6"
    >
      <div class="min-w-0">
        <h1 class="text-[21px] leading-tight font-semibold">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="mt-1 text-sm text-white/80">{{ subtitle() }}</p>
        }
      </div>
      <div class="flex shrink-0 items-center gap-3">
        <ng-content select="[headerAction]" />
        <img
          src="logo/mark.png"
          alt=""
          class="size-12 shrink-0 object-contain sm:size-14"
          aria-hidden="true"
        />
      </div>
    </header>
  `,
})
export class AdultPageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
}
