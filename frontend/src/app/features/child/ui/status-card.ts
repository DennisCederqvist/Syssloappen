import { Component, input } from '@angular/core';

export type ChildStatusCardKind = 'pending' | 'needsRedo';

/** A chore that isn't actionable right now: waiting on a parent's review, or
 * sent back with a comment. Same rounded-card language as ChildTaskCard, but
 * no CTA — just a status message, so it's a distinct shape rather than a
 * variant of the same component. */
@Component({
  selector: 'app-child-status-card',
  template: `
    <article
      [id]="cardId()"
      tabindex="-1"
      class="rounded-[30px] bg-white p-5 shadow-[4px_6px_0_rgba(0,0,0,0.04)] outline-none sm:p-6"
    >
      <div class="flex items-start justify-between gap-3">
        <h3 class="font-display text-[19px] leading-snug font-semibold text-child-text">
          {{ title() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-child-nav-bg px-2.5 py-1 text-sm font-bold text-child-text"
          [attr.aria-label]="points() + ' poäng'"
        >
          <svg
            viewBox="0 0 20 20"
            class="size-3.5 shrink-0"
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
          {{ points() }}
        </span>
      </div>

      @if (kind() === 'pending') {
        <p class="mt-4 rounded-2xl bg-amber-50 p-4 text-sm leading-6 font-semibold text-amber-900">
          Bra jobbat! En vuxen tittar på uppgiften innan poängen delas ut.
        </p>
      } @else {
        <div class="mt-4 rounded-2xl bg-red-50 p-4 text-red-950">
          <p class="text-sm font-bold">Kommentar från en vuxen</p>
          <p class="mt-1 leading-6">
            {{ message() || 'Försök en gång till och rapportera när du är klar.' }}
          </p>
        </div>
      }
    </article>
  `,
})
export class ChildStatusCard {
  readonly cardId = input.required<string>();
  readonly title = input.required<string>();
  readonly points = input.required<number>();
  readonly kind = input.required<ChildStatusCardKind>();
  readonly message = input<string | null>(null);
}
