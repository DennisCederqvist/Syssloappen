import { Component, input } from '@angular/core';

/** Used only by the two child pages not yet migrated to ChildPageHeader
 * (Belöningar, Önskningar — see docs/child-view-redesign). No logout button:
 * the child view never exposes logout, it's parent-managed. */
@Component({
  selector: 'app-user-header',
  template: `<header class="flex items-start gap-3 sm:items-center sm:gap-4">
    <div
      class="grid size-12 shrink-0 place-items-center rounded-2xl bg-amber-100 text-2xl shadow-sm"
      aria-hidden="true"
    >
      🌟
    </div>
    <div class="min-w-0">
      <p class="text-xs font-extrabold tracking-wider text-brand-600 uppercase">
        {{ eyebrow() }}
      </p>
      <h1 class="text-xl leading-tight font-black sm:text-2xl">{{ title() }}</h1>
    </div>
  </header>`,
})
export class UserHeader {
  readonly title = input.required<string>();
  readonly eyebrow = input.required<string>();
}
