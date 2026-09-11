import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** "Hej {name}!" header for the child view: avatar placeholder (a real
 * uploaded photo later), name + subtitle, and a static points readout —
 * a plain white pill, not a button, so it must never look clickable. */
@Component({
  selector: 'app-child-page-header',
  imports: [TranslocoPipe],
  template: `
    <header class="flex items-start justify-between gap-3">
      <div class="flex min-w-0 items-center gap-3">
        <div
          class="grid size-14 shrink-0 place-items-center rounded-2xl bg-child-avatar-b-bg text-child-avatar-b-icon"
          aria-hidden="true"
        >
          <svg viewBox="0 0 24 24" class="size-7" fill="currentColor">
            <circle cx="12" cy="8" r="4" />
            <path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7v1H4z" />
          </svg>
        </div>
        <div class="min-w-0">
          <h1 class="font-display text-[26px] leading-tight font-bold text-child-text">
            {{ 'child.common.greeting' | transloco: { name: name() } }}
          </h1>
          <p class="mt-0.5 text-[15px] text-child-text-secondary">{{ subtitle() }}</p>
        </div>
      </div>

      <div
        class="flex shrink-0 items-center gap-1.5 rounded-[18px] bg-white px-[18px] py-2.5 shadow-[0_3px_0_rgba(0,0,0,0.06)]"
        [attr.aria-label]="'child.common.pointsAria' | transloco: { points: points() }"
      >
        <svg
          viewBox="0 0 20 20"
          class="size-5 shrink-0"
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
        <strong class="text-lg font-bold text-child-text">{{ points() }}</strong>
      </div>
    </header>
  `,
})
export class ChildPageHeader {
  readonly name = input.required<string>();
  readonly subtitle = input.required<string>();
  readonly points = input.required<number>();
}
