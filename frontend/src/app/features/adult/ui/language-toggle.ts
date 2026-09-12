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
          class="inline-flex min-h-9 items-center gap-1 rounded-md px-2.5 text-sm font-medium transition"
          [class.bg-adult-accent]="lang.currentLang() === option"
          [class.text-white]="lang.currentLang() === option"
          [class.text-adult-text-secondary]="lang.currentLang() !== option"
        >
          <span aria-hidden="true">{{ option === 'sv' ? '🇸🇪' : '🇬🇧' }}</span>
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
