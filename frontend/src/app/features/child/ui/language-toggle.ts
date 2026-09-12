import { Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AppLanguage, LanguageService } from '../../../core/i18n/language.service';

/** Bubbly SV|EN segmented toggle for the child settings page — same
 * mechanism as the adult one (AdultLanguageToggle), restyled to match the
 * child design system's rounded-pill, tap-feedback language. */
@Component({
  selector: 'app-child-language-toggle',
  imports: [TranslocoPipe],
  template: `
    <div
      class="inline-flex gap-1 rounded-full bg-white p-1 shadow-[4px_6px_0_rgba(0,0,0,0.04)]"
      role="group"
      [attr.aria-label]="'common.settings.language.label' | transloco"
    >
      @for (option of options; track option) {
        <button
          type="button"
          [attr.aria-pressed]="lang.currentLang() === option"
          (click)="select(option)"
          class="flex min-h-11 items-center gap-1.5 rounded-full px-4 text-[15px] font-semibold transition active:translate-x-px active:translate-y-px"
          [class]="
            lang.currentLang() === option
              ? 'bg-[linear-gradient(180deg,var(--color-child-cta-from),var(--color-child-cta-to))] text-child-text shadow-[4px_6px_0_var(--color-child-cta-shadow)]'
              : 'text-child-text-secondary'
          "
        >
          @if (option === 'sv') {
            <svg viewBox="0 0 16 10" class="h-3.5 w-5 shrink-0 rounded-[2px]" aria-hidden="true">
              <rect width="16" height="10" fill="#006AA7" />
              <rect x="5" width="2" height="10" fill="#FECC00" />
              <rect y="4" width="16" height="2" fill="#FECC00" />
            </svg>
          } @else {
            <svg viewBox="0 0 16 10" class="h-3.5 w-5 shrink-0 rounded-[2px]" aria-hidden="true">
              <rect width="16" height="10" fill="#00247D" />
              <path d="M0 0L16 10M16 0L0 10" stroke="#FFFFFF" stroke-width="2" />
              <path d="M0 0L16 10M16 0L0 10" stroke="#CF142B" stroke-width="0.8" />
              <rect x="6.5" width="3" height="10" fill="#FFFFFF" />
              <rect y="3.5" width="16" height="3" fill="#FFFFFF" />
              <rect x="7" width="2" height="10" fill="#CF142B" />
              <rect y="4" width="16" height="2" fill="#CF142B" />
            </svg>
          }
          <span>{{ option === 'sv' ? 'SV' : 'ENG' }}</span>
        </button>
      }
    </div>
  `,
})
export class ChildLanguageToggle {
  protected readonly lang = inject(LanguageService);
  protected readonly options: AppLanguage[] = ['sv', 'en'];

  select(option: AppLanguage): void {
    this.lang.setLanguage(option).subscribe();
  }
}
