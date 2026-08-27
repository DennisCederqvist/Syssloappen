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
      class="mx-auto flex max-w-lg justify-around md:h-full md:flex-col md:justify-start md:gap-3"
    >
      @for (item of displayedItems(); track item.label) {
        @if (item.route) {
          <a
            [routerLink]="item.route"
            [attr.aria-current]="item.active ? 'page' : null"
            class="flex min-h-14 min-w-16 flex-col items-center justify-center gap-1 rounded-2xl px-2 text-xs font-bold transition hover:bg-brand-50 md:min-h-16"
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
            class="flex min-h-14 min-w-16 flex-col items-center justify-center gap-1 rounded-2xl px-2 text-xs font-bold text-muted opacity-60 md:min-h-16"
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
    const currentUrl = this.router.url;
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
          { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
          { label: 'Önskningar', icon: '♡', route: '/vuxen/onskningar' },
          { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
        ];
    return navigation.map((item) => ({ ...item, active: currentUrl === item.route }));
  });
}
