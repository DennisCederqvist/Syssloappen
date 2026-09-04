import { Component, HostListener, input, output } from '@angular/core';

/** Full-screen sheet for forms — replaces the old floating modal cards
 * (layout rule 7). Covers the viewport on mobile; a centered dialog with a
 * backdrop from md upward. Emits (close) instead of managing its own
 * open/close state, so the host page keeps driving its existing signals and
 * focusAfterRender() calls unchanged. */
@Component({
  selector: 'app-adult-sheet',
  template: `
    <div class="fixed inset-0 z-50 flex justify-center md:items-center md:bg-black/30 md:p-4">
      <div
        [id]="panelId()"
        tabindex="-1"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="headingId()"
        class="flex h-full w-full flex-col overflow-y-auto bg-adult-surface outline-none md:h-auto md:max-h-[85vh] md:max-w-lg md:rounded-lg md:border md:border-adult-border md:shadow-xl"
      >
        <header
          class="flex items-start justify-between gap-3 border-b border-adult-border p-4"
        >
          <h2 [id]="headingId()" class="text-[17px] leading-tight font-semibold text-adult-text">
            {{ title() }}
          </h2>
          <button
            type="button"
            (click)="close.emit()"
            [attr.aria-label]="closeLabel()"
            class="grid min-h-11 min-w-11 shrink-0 place-items-center rounded-lg text-lg text-adult-text-secondary transition hover:bg-adult-bg"
          >
            ×
          </button>
        </header>
        <div class="flex-1 p-4">
          <ng-content />
        </div>
      </div>
    </div>
  `,
})
export class AdultSheet {
  readonly panelId = input.required<string>();
  readonly headingId = input.required<string>();
  readonly title = input.required<string>();
  readonly closeLabel = input('Stäng');
  readonly close = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }
}
