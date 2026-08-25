import { Component, input } from '@angular/core';

export interface NavItem {
  label: string;
  icon: string;
  active?: boolean;
}

@Component({
  selector: 'app-bottom-nav',
  template: `<nav
    class="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-white/95 px-3 pb-[max(.7rem,env(safe-area-inset-bottom))] pt-2 backdrop-blur md:static md:w-24 md:border-t-0 md:border-r md:px-2 md:pt-6"
    aria-label="Huvudnavigation"
  >
    <div
      class="mx-auto flex max-w-lg justify-around md:h-full md:flex-col md:justify-start md:gap-3"
    >
      @for (item of items(); track item.label) {
        <button
          type="button"
          [attr.aria-current]="item.active ? 'page' : null"
          [disabled]="!item.active"
          class="flex min-h-14 min-w-16 flex-col items-center justify-center gap-1 rounded-2xl px-2 text-[.68rem] font-bold transition md:min-h-16"
          [class.bg-brand-50]="item.active"
          [class.text-brand-600]="item.active"
          [class.text-muted]="!item.active"
        >
          <span class="text-xl leading-none" aria-hidden="true">{{ item.icon }}</span
          >{{ item.label }}
        </button>
      }
    </div>
  </nav>`,
})
export class AppBottomNav {
  readonly items = input.required<NavItem[]>();
}
