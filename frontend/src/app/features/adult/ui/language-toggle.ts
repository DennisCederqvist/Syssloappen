import { Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AppLanguage, LanguageService } from '../../../core/i18n/language.service';

/** Compact flag + short-code SV|ENG segmented toggle, used for the adult
 * settings page and the login page alike. */
@Component({
  selector: 'app-adult-language-toggle',
  imports: [TranslocoPipe],
  template: `
    <div
      class="inline-flex rounded-lg border border-adult-border bg-adult-surface p-0.5"
      role="group"
      [attr.aria-label]="'common.settings.language.label' | transloco"
    >
      @for (option of options; track option) {
        <button
          type="button"
          [attr.aria-pressed]="lang.currentLang() === option"
          (click)="select(option)"
          class="inline-flex min-h-11 items-center gap-1 rounded-md px-2.5 text-sm font-medium transition"
          [class.bg-adult-accent]="lang.currentLang() === option"
          [class.text-white]="lang.currentLang() === option"
          [class.text-adult-text-secondary]="lang.currentLang() !== option"
        >
          @if (option === 'sv') {
            <svg viewBox="0 0 16 10" class="h-3 w-4.5 shrink-0 rounded-[2px]" aria-hidden="true">
              <rect width="16" height="10" fill="#006AA7" />
              <rect x="5" width="2" height="10" fill="#FECC00" />
              <rect y="4" width="16" height="2" fill="#FECC00" />
            </svg>
          } @else {
            <svg viewBox="0 0 16 10" class="h-3 w-4.5 shrink-0 rounded-[2px]" aria-hidden="true">
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
export class AdultLanguageToggle {
  protected readonly lang = inject(LanguageService);
  protected readonly options: AppLanguage[] = ['sv', 'en'];

  select(option: AppLanguage): void {
    this.lang.setLanguage(option).subscribe();
  }
}
