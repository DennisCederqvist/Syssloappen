import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-session-page',
  imports: [TranslocoPipe],
  template: `<main class="grid min-h-dvh place-items-center p-6" aria-live="polite">
    <div class="flex flex-col items-center gap-4 text-center">
      <span
        class="grid size-14 animate-pulse place-items-center rounded-2xl bg-brand-500 text-2xl text-white"
        >✓</span
      >
      <p class="font-bold text-ink">{{ 'session.opening' | transloco }}</p>
    </div>
  </main>`,
})
export class SessionPage {}
