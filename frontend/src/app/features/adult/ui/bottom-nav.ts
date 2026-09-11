import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';

interface AdultNavItem {
  labelKey: string;
  route: string;
  icon: 'home' | 'list' | 'star' | 'gear';
}

const NAV_ITEMS: AdultNavItem[] = [
  { labelKey: 'adult.nav.home', route: '/vuxen', icon: 'home' },
  { labelKey: 'adult.nav.chores', route: '/vuxen/sysslor', icon: 'list' },
  { labelKey: 'adult.nav.rewards', route: '/vuxen/belöningar', icon: 'star' },
  { labelKey: 'adult.nav.settings', route: '/vuxen/installningar', icon: 'gear' },
];

/** The 4-item adult tab bar: Hem, Sysslor, Belöningar, Inställningar.
 * Adult-only — the child view has its own ChildSideNav. */
@Component({
  selector: 'app-adult-bottom-nav',
  imports: [RouterLink],
  template: `
    <nav
      class="fixed inset-x-0 bottom-0 z-50 border-t border-adult-border bg-adult-surface/95 px-3 pb-[max(.6rem,env(safe-area-inset-bottom))] pt-2 backdrop-blur md:static md:w-20 md:border-t-0 md:border-r md:px-2 md:pt-6"
      [attr.aria-label]="navAriaLabel()"
    >
      <div
        class="mx-auto grid w-full max-w-lg grid-cols-4 gap-1 md:h-full md:w-auto md:grid-cols-1 md:justify-start md:gap-3"
      >
        @for (item of items(); track item.route) {
          <a
            [routerLink]="item.route"
            [attr.aria-current]="item.active ? 'page' : null"
            class="flex min-h-11 min-w-0 flex-col items-center justify-center gap-1 rounded-lg px-1 py-1.5 text-center text-[11px] leading-none font-medium transition"
            [class.text-adult-accent]="item.active"
            [class.text-adult-text-secondary]="!item.active"
          >
            <svg
              viewBox="0 0 24 24"
              class="size-5"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              @switch (item.icon) {
                @case ('home') {
                  <path d="M4 11.5 12 4l8 7.5" />
                  <path d="M6 10v9a1 1 0 0 0 1 1h3v-5h4v5h3a1 1 0 0 0 1-1v-9" />
                }
                @case ('list') {
                  <path d="M9 6h11M9 12h11M9 18h11" />
                  <path d="M4 6h.01M4 12h.01M4 18h.01" />
                }
                @case ('star') {
                  <path
                    d="M12 3.5l2.8 5.7 6.3.9-4.55 4.45 1.08 6.3L12 17.8l-5.63 3.05 1.08-6.3L2.9 10.1l6.3-.9z"
                  />
                }
                @case ('gear') {
                  <circle cx="12" cy="12" r="3" />
                  <path
                    d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"
                  />
                }
              }
            </svg>
            <span>{{ item.label }}</span>
          </a>
        }
      </div>
    </nav>
  `,
})
export class AdultBottomNav {
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  readonly navAriaLabel = computed(() => {
    this.transloco.activeLang();
    return this.transloco.translate('adult.nav.ariaLabel');
  });
  readonly items = computed(() => {
    this.transloco.activeLang();
    // Router.url percent-encodes Swedish route characters (e.g. ö); decode before
    // comparing so /vuxen/belöningar receives its active state.
    const currentUrl = decodeURIComponent(this.router.url.split(/[?#]/, 1)[0]);
    return NAV_ITEMS.map((item) => ({
      ...item,
      label: this.transloco.translate(item.labelKey),
      active: currentUrl === item.route,
    }));
  });
}
