import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

export interface NavItem {
  label: string;
  icon: string;
  active?: boolean;
  route?: string;
}

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink],
  template: `<nav
    class="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-white/95 px-3 pb-[max(.7rem,env(safe-area-inset-bottom))] pt-2 backdrop-blur md:static md:w-24 md:border-t-0 md:border-r md:px-2 md:pt-6"
    aria-label="Huvudnavigation"
  >
    <div
      class="mx-auto grid w-full max-w-lg gap-1 md:h-full md:w-auto md:grid-cols-1 md:justify-start md:gap-3"
      [class.grid-cols-3]="displayedItems().length === 3"
      [class.grid-cols-4]="displayedItems().length === 4"
      [class.grid-cols-6]="displayedItems().length === 6"
    >
      @for (item of displayedItems(); track item.label) {
        @if (item.route) {
          <a
            [routerLink]="item.route"
            [attr.aria-current]="item.active ? 'page' : null"
            class="flex min-h-14 min-w-0 flex-col items-center justify-center gap-1 rounded-2xl px-1 text-center text-[10px] leading-3 font-bold transition hover:bg-brand-50 md:min-h-16 md:min-w-16 md:px-2 md:text-xs"
            [class.bg-brand-50]="item.active"
            [class.text-brand-600]="item.active"
            [class.text-muted]="!item.active"
          >
            <span class="text-xl leading-none" aria-hidden="true">{{ item.icon }}</span
            >{{ item.label }}
          </a>
        } @else {
          <button
            type="button"
            disabled
            class="flex min-h-14 min-w-0 flex-col items-center justify-center gap-1 rounded-2xl px-1 text-center text-[10px] leading-3 font-bold text-muted opacity-60 md:min-h-16 md:min-w-16 md:px-2 md:text-xs"
          >
            <span class="text-xl leading-none" aria-hidden="true">{{ item.icon }}</span
            >{{ item.label }}
          </button>
        }
      }
    </div>
  </nav>`,
})
export class AppBottomNav {
  private readonly router = inject(Router);
  readonly items = input.required<NavItem[]>();
  readonly displayedItems = computed(() => {
    const isChild = this.items().some((item) => item.route?.startsWith('/barn'));
    // Router.url percent-encodes Swedish route characters. Decode it before
    // comparing so routes such as /vuxen/belöningar receive their active state.
    const currentUrl = decodeURIComponent(this.router.url.split(/[?#]/, 1)[0]);
    const navigation = isChild
      ? [
          { label: 'Idag', icon: '⌂', route: '/barn' },
          { label: 'Belöningar', icon: '★', route: '/barn/beloningar' },
          { label: 'Önskningar', icon: '♡', route: '/barn/onskningar' },
        ]
      : [
          { label: 'Hem', icon: '⌂', route: '/vuxen' },
          { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
          { label: 'Belöningar', icon: '★', route: '/vuxen/belöningar' },
          { label: 'Inställningar', icon: '⚙', route: '/vuxen/installningar' },
        ];
    return navigation.map((item) => ({ ...item, active: currentUrl === item.route }));
  });
}
