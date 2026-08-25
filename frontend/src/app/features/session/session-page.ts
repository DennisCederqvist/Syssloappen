import { Component } from '@angular/core';

@Component({
  selector: 'app-session-page',
  template: `<main class="grid min-h-dvh place-items-center p-6" aria-live="polite">
    <div class="flex flex-col items-center gap-4 text-center">
      <span
        class="grid size-14 animate-pulse place-items-center rounded-2xl bg-brand-500 text-2xl text-white"
        >✓</span
      >
      <p class="font-bold text-ink">Öppnar Syssloappen…</p>
    </div>
  </main>`,
})
export class SessionPage {}
