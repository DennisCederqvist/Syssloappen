import { Component, input } from '@angular/core';

/** Stand-in shown on child cards that have no uploaded picture: a broom for chores,
 * a gift box for rewards and wishes. Same 16:8 footprint as the real image. */
@Component({
  selector: 'app-child-image-placeholder',
  template: `
    <div
      class="flex aspect-[16/8] items-center justify-center rounded-[15px] bg-white/40"
      aria-hidden="true"
    >
      <svg
        viewBox="0 0 24 24"
        class="size-12 text-child-text-secondary/60"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        @if (kind() === 'chore') {
          <path d="M20 4l-7.5 7.5" />
          <path d="M9.5 10.5l4 4-3 5.5-6.5.5.5-6.5z" />
          <path d="M8 15.5l2 2M6 17.5l1.5 1.5" />
        } @else {
          <rect x="3" y="8" width="18" height="13" rx="1.5" />
          <path d="M3 12h18" />
          <path d="M12 8v13" />
          <path
            d="M12 8H8.5a2 2 0 1 1 0-4c1.5 0 2.7 1.2 3.5 4zM12 8h3.5a2 2 0 1 0 0-4c-1.5 0-2.7 1.2-3.5 4z"
          />
        }
      </svg>
    </div>
  `,
})
export class ChildImagePlaceholder {
  readonly kind = input.required<'chore' | 'reward'>();
}
