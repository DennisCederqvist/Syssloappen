import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { CHILD_CARD_PALETTE_CLASSES, ChildCardPalette } from './palette';

export type ChildTaskCardPalette = ChildCardPalette;

/** A single "today's chore" card: permanently tilted a couple degrees,
 * matching docs/barnvy mockup.png. The tilt itself is a plain inline style
 * (`rotate`, always on); `wobbling` layers a one-shot CSS animation on top
 * of it — the parent decides *when* and *which* card wobbles (a random card
 * at a random interval), this component just plays the single ~0.9s event
 * when told to. Covers both a fresh assignment and one sent back for redo
 * (still actionable, with a "Jag är klar!" button) — a genuinely
 * waiting-on-review assignment has no CTA at all, so that state lives in
 * ChildStatusCard instead. */
@Component({
  selector: 'app-child-task-card',
  imports: [TranslocoPipe],
  template: `
    <article
      [id]="cardId()"
      tabindex="-1"
      class="flex flex-col rounded-[30px] p-5 outline-none sm:p-6 {{ paletteClasses() }}"
      [style.rotate.deg]="tiltDeg()"
      [style.--tilt.deg]="tiltDeg()"
      [style.animation-name]="wobbling() ? 'child-card-wobble' : 'none'"
      [style.animation-duration.s]="0.9"
      [style.animation-timing-function]="'ease-in-out'"
      [style.animation-iteration-count]="1"
    >
      <div class="flex items-start justify-between gap-3">
        <h3 class="font-display text-[19px] leading-snug font-semibold text-child-text">
          {{ title() }}
        </h3>
        <span
          class="flex shrink-0 items-center gap-1 rounded-full bg-white/70 px-2.5 py-1 text-sm font-bold text-child-text"
          [attr.aria-label]="'child.common.pointsAria' | transloco: { points: points() }"
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

      @if (description()) {
        <p class="mt-2 text-[15px] leading-6 text-child-text-secondary">{{ description() }}</p>
      }

      @if (reviewComment()) {
        <div class="mt-4 rounded-2xl bg-white/70 p-4 text-child-text">
          <p class="text-sm font-bold">{{ 'child.taskCard.reviewCommentLabel' | transloco }}</p>
          <p class="mt-1 text-[15px] leading-6">{{ reviewComment() }}</p>
        </div>
      }

      @if (errorMessage()) {
        <p class="mt-3 text-sm font-semibold text-red-700" role="alert">{{ errorMessage() }}</p>
      }

      <button
        type="button"
        (click)="done.emit()"
        [disabled]="submitting()"
        [attr.aria-label]="'child.taskCard.submitAria' | transloco: { title: title() }"
        class="mt-5 min-h-14 w-full rounded-full bg-[linear-gradient(180deg,var(--color-child-cta-from),var(--color-child-cta-to))] text-base font-bold text-child-text shadow-[4px_6px_0_var(--color-child-cta-shadow)] transition active:translate-x-[2px] active:translate-y-[3px] active:shadow-[1px_2px_0_var(--color-child-cta-shadow)] disabled:cursor-wait disabled:opacity-70"
      >
        {{ (submitting() ? 'child.taskCard.submitting' : 'child.taskCard.submit') | transloco }}
      </button>
    </article>
  `,
})
export class ChildTaskCard {
  readonly cardId = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
  readonly reviewComment = input<string | null>(null);
  readonly points = input.required<number>();
  readonly palette = input<ChildTaskCardPalette>('blue');
  readonly tiltDeg = input(0);
  readonly wobbling = input(false);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly done = output<void>();

  paletteClasses(): string {
    return CHILD_CARD_PALETTE_CLASSES[this.palette()];
  }
}
