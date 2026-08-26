import { Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-user-header',
  template: `<header class="flex items-start justify-between gap-3 sm:items-center sm:gap-4">
    <div class="flex min-w-0 items-center gap-3">
      <div
        class="grid size-12 shrink-0 place-items-center rounded-2xl text-2xl shadow-sm"
        [class.bg-brand-100]="variant() === 'adult'"
        [class.bg-amber-100]="variant() === 'child'"
        aria-hidden="true"
      >
        {{ variant() === 'adult' ? '🏡' : '🌟' }}
      </div>
      <div class="min-w-0">
        <p class="text-xs font-extrabold tracking-wider text-brand-600 uppercase">
          {{ eyebrow() }}
        </p>
        <h1 class="text-xl leading-tight font-black sm:text-2xl">{{ title() }}</h1>
      </div>
    </div>
    <button
      type="button"
      (click)="logout()"
      class="min-h-12 shrink-0 rounded-xl border border-line bg-white px-3 text-sm font-bold text-muted shadow-sm hover:text-ink"
    >
      Logga ut
    </button>
  </header>`,
})
export class UserHeader {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly title = input.required<string>();
  readonly eyebrow = input.required<string>();
  readonly variant = input<'adult' | 'child'>('adult');
  logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }
}
