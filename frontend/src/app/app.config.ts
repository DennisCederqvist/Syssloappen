import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { credentialsInterceptor } from './core/auth/credentials.interceptor';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { LanguageService } from './core/i18n/language.service';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([credentialsInterceptor])),
    provideTransloco({
      config: {
        availableLangs: ['sv', 'en'],
        defaultLang: 'sv',
        reRenderOnLangChange: true,
      },
      loader: TranslocoHttpLoader,
    }),
    // Applies the persisted (or default) language and waits for its
    // translation file to load before the app renders, so there's no flash
    // of the wrong language or untranslated keys.
    provideAppInitializer(() => inject(LanguageService).initialize()),
  ],
};
