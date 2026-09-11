import { Injectable } from '@angular/core';
import { EnvironmentProviders } from '@angular/core';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';
import { of } from 'rxjs';
import en from '../../../../public/i18n/en.json';
import sv from '../../../../public/i18n/sv.json';

const TRANSLATIONS: Record<string, Translation> = { sv, en };

@Injectable()
class TestTranslocoLoader implements TranslocoLoader {
  getTranslation(lang: string) {
    return of(TRANSLATIONS[lang] ?? {});
  }
}

/** Real sv.json/en.json content, loaded synchronously with no HTTP round-trip
 * — for unit tests only. Keeps test assertions on rendered text meaningful
 * (they check the actual translation, not a stub) without needing
 * HttpTestingController plumbing in every spec that touches a translated
 * component.
 *
 * Gotcha: Transloco only actually loads the active language once something
 * triggers it — a template's `| transloco` pipe binding does this
 * automatically, but a component method calling `TranslocoService.translate()`
 * imperatively does not. A spec whose `beforeEach` never calls
 * `fixture.detectChanges()` (e.g. it calls `ngOnInit()` directly to avoid
 * rendering) will see raw keys instead of translated text from any
 * `.translate()` call made before the first render. Fix: call
 * `fixture.detectChanges()` at least once before asserting on translated
 * content, even in specs that otherwise test pure component logic. */
export function provideTranslocoTesting(): EnvironmentProviders[] {
  return provideTransloco({
    config: { availableLangs: ['sv', 'en'], defaultLang: 'sv', reRenderOnLangChange: true },
    loader: TestTranslocoLoader,
  });
}
