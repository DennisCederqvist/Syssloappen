import { Component, input } from '@angular/core';

/** Small pill for points (star + value) and count indicators (plain value). */
@Component({
  selector: 'app-adult-badge',
  template: `
    <span
      class="inline-flex items-center gap-1 rounded-lg px-2 py-0.5 text-xs font-semibold whitespace-nowrap"
      [class.bg-adult-points-bg]="variant() === 'points'"
      [class.text-adult-points-text]="variant() === 'points'"
      [class.bg-adult-accent-soft]="variant() === 'count'"
      [class.text-adult-accent-dark]="variant() === 'count'"
    >
      @if (variant() === 'points') {
        <svg
          viewBox="0 0 20 20"
          class="size-3"
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
      }
      {{ value() }}
    </span>
  `,
})
export class AdultBadge {
  readonly variant = input<'points' | 'count'>('count');
  readonly value = input.required<number | string>();
}
