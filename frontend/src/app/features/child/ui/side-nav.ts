import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';

interface ChildNavItem {
  labelKey: string;
  route: string;
  icon: 'tasks' | 'star' | 'heart' | 'gear';
  pinned?: boolean;
}

const NAV_ITEMS: ChildNavItem[] = [
  { labelKey: 'child.nav.chores', route: '/barn', icon: 'tasks' },
  { labelKey: 'child.nav.rewards', route: '/barn/beloningar', icon: 'star' },
  { labelKey: 'child.nav.wishes', route: '/barn/onskningar', icon: 'heart' },
  { labelKey: 'child.nav.settings', route: '/barn/installningar', icon: 'gear', pinned: true },
];

/** The child view's persistent nav: a bottom tab bar on narrow screens, a
 * light-blue-tinted left sidebar with a decorative avatar mark and a pinned
 * "Inställningar" gear from md upward, matching docs/barnvy mockup.png.
 * No "Logga ut" here or anywhere in the child view — logout is parent-managed. */
@Component({
  selector: 'app-child-side-nav',
  imports: [RouterLink],
  template: `
    <nav
      class="fixed inset-x-0 bottom-0 z-50 border-t border-child-nav-bg bg-child-nav-bg px-3 pb-[max(.6rem,env(safe-area-inset-bottom))] pt-2 md:static md:flex md:h-dvh md:w-28 md:flex-col md:items-center md:border-t-0 md:px-3 md:py-6"
      [attr.aria-label]="navAriaLabel()"
    >
      <div
        class="mx-auto hidden size-12 shrink-0 place-items-center rounded-2xl bg-child-avatar-a-bg text-child-avatar-a-icon shadow-[4px_6px_0_rgba(0,0,0,0.05)] md:grid"
        aria-hidden="true"
      >
        <svg viewBox="0 0 24 24" class="size-6" fill="currentColor">
          <circle cx="12" cy="8" r="4" />
          <path d="M4 20c0-4.4 3.6-7 8-7s8 2.6 8 7v1H4z" />
        </svg>
      </div>

      <div
        class="mx-auto grid w-full max-w-lg grid-cols-4 gap-1 md:mt-8 md:flex md:h-full md:w-auto md:flex-col md:items-stretch md:gap-2"
      >
        @for (item of items(); track item.route) {
          <a
            [routerLink]="item.route"
            [attr.aria-current]="item.active ? 'page' : null"
            class="flex min-h-14 min-w-0 flex-col items-center justify-center gap-1 rounded-2xl px-1 py-1.5 text-center text-[11px] leading-none font-semibold transition md:min-h-16 md:w-20"
            [class]="itemClasses(item)"
          >
            <svg
              viewBox="0 0 24 24"
              class="size-5"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              @switch (item.icon) {
                @case ('tasks') {
                  <rect x="9" y="3" width="6" height="3.5" rx="1" />
                  <path
                    d="M9 4.75H7.5A1.5 1.5 0 0 0 6 6.25v13.5A1.5 1.5 0 0 0 7.5 21.25h9a1.5 1.5 0 0 0 1.5-1.5V6.25a1.5 1.5 0 0 0-1.5-1.5H15"
                  />
                  <path d="M9 13.2l2.2 2.2L15.5 11" />
                }
                @case ('star') {
                  <path
                    d="M12 3.5l2.8 5.7 6.3.9-4.55 4.45 1.08 6.3L12 17.8l-5.63 3.05 1.08-6.3L2.9 10.1l6.3-.9z"
                  />
                }
                @case ('heart') {
                  <path
                    d="M12 20s-7-4.35-9.5-8.7C1 8.4 2.6 5 6 5c2 0 3.3 1.1 4 2.2C10.7 6.1 12 5 14 5c3.4 0 5 3.4 3.5 6.3C19 15.65 12 20 12 20z"
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
export class ChildSideNav {
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  private readonly currentUrl = computed(() =>
    decodeURIComponent(this.router.url.split(/[?#]/, 1)[0]),
  );
  readonly navAriaLabel = computed(() => {
    this.transloco.activeLang();
    return this.transloco.translate('child.nav.ariaLabel');
  });
  readonly items = computed(() => {
    this.transloco.activeLang();
    return NAV_ITEMS.map((item) => ({
      ...item,
      label: this.transloco.translate(item.labelKey),
      active: this.currentUrl() === item.route,
    }));
  });

  itemClasses(item: ChildNavItem & { active: boolean }): string {
    const pinned = item.pinned ? 'md:mt-auto' : '';
    const state = item.active
      ? 'bg-white text-child-accent shadow-[4px_6px_0_rgba(0,0,0,0.04)]'
      : 'text-child-text-secondary';
    return `${pinned} ${state}`;
  }
}
