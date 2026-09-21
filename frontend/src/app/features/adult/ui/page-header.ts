import { Component, input } from '@angular/core';

/** Branded green header used on every adult screen: title + optional subtitle,
 * and the Sysslo logo mark as a color splash on the right — so every page
 * carries the same visual anchor. This header carries no action buttons of
 * its own; a page's own action button (if any) lives below it. */
@Component({
  selector: 'app-adult-page-header',
  template: `
    <header
      class="flex items-center justify-between gap-3 rounded-2xl bg-adult-accent px-5 py-5 text-white sm:px-6 sm:py-6"
    >
      <div class="min-w-0">
        <h1 class="text-[21px] leading-tight font-semibold">{{ title() }}</h1>
        @if (points() !== null) {
          <p class="mt-1 flex items-center gap-1 text-sm font-semibold text-white">
            <svg
              viewBox="0 0 20 20"
              class="size-3.5"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path
                d="M10 2l2.35 4.76 5.26.76-3.8 3.71.9 5.24L10 14l-4.71 2.47.9-5.24-3.8-3.71 5.26-.76z"
              />
            </svg>
            <span aria-hidden="true">{{ points() }}</span>
            <span class="sr-only">{{ pointsLabel() }}</span>
          </p>
        }
        @if (subtitle()) {
          <p class="mt-1 text-sm text-white/80">{{ subtitle() }}</p>
        }
      </div>
      <img
        src="logo/mark.png"
        alt=""
        class="size-12 shrink-0 object-contain sm:size-14"
        aria-hidden="true"
      />
    </header>
  `,
})
export class AdultPageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  /** Optional star + number shown directly under the title. */
  readonly points = input<number | null>(null);
  /** Screen-reader text for the points, e.g. "12 poäng". */
  readonly pointsLabel = input('');
}
