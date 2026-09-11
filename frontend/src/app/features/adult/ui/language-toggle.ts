import { Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AppLanguage, LanguageService } from '../../../core/i18n/language.service';

/** Dense/tool-like SV|EN segmented toggle for the adult settings page. */
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
          class="min-h-9 rounded-md px-3 text-sm font-medium transition"
          [class.bg-adult-accent]="lang.currentLang() === option"
          [class.text-white]="lang.currentLang() === option"
          [class.text-adult-text-secondary]="lang.currentLang() !== option"
        >
          {{ ('common.settings.language.' + (option === 'sv' ? 'swedish' : 'english')) | transloco }}
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
