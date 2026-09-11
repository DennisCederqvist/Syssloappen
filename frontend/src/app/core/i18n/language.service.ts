import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { map, tap, type Observable } from 'rxjs';

export type AppLanguage = 'sv' | 'en';

const STORAGE_KEY = 'syssloappen.lang';
const DEFAULT_LANGUAGE: AppLanguage = 'sv';

function isAppLanguage(value: string | null): value is AppLanguage {
  return value === 'sv' || value === 'en';
}

function readPersistedLanguage(): AppLanguage {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return isAppLanguage(stored) ? stored : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}

/** Single source of truth for the active UI language. Components that offer
 * a language toggle bind to this, not TranslocoService directly — components
 * that just need a translated string in .ts logic still inject
 * TranslocoService itself (see docs/i18n/i18n-retrofit.md). */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  readonly currentLang = signal<AppLanguage>(readPersistedLanguage());

  /** Loads and activates the persisted (or default) language. Returns an
   * observable so app bootstrap can wait for it via provideAppInitializer,
   * avoiding a flash of untranslated/wrong-language content. */
  initialize(): Observable<unknown> {
    const lang = this.currentLang();
    return this.transloco.load(lang).pipe(tap(() => this.transloco.setActiveLang(lang)));
  }

  setLanguage(lang: AppLanguage): Observable<unknown> {
    return this.transloco.load(lang).pipe(
      tap(() => {
        this.transloco.setActiveLang(lang);
        this.currentLang.set(lang);
        try {
          localStorage.setItem(STORAGE_KEY, lang);
        } catch {
          // Best-effort only — a private-browsing/full-storage failure here
          // just means the preference won't survive a reload.
        }
      }),
      map(() => undefined),
    );
  }
}
