import { Directive } from '@angular/core';

/** Apply directly to a native <button>: keeps click handlers, [disabled], etc. native. */

/** Solid accent — "Godkänn", "Tilldela sysslan". */
@Directive({
  selector: '[adultPrimaryButton]',
  host: {
    class:
      'inline-flex min-h-11 items-center justify-center rounded-lg bg-adult-accent px-4 text-sm font-medium text-white transition hover:bg-adult-accent-dark disabled:pointer-events-none disabled:opacity-50',
  },
})
export class AdultPrimaryButton {}

/** Outlined red — "Avslå". */
@Directive({
  selector: '[adultDangerOutlineButton]',
  host: {
    class:
      'inline-flex min-h-11 items-center justify-center rounded-lg border border-adult-danger-border bg-adult-danger-bg px-4 text-sm font-medium text-adult-danger-text transition hover:bg-adult-danger-border/25 disabled:pointer-events-none disabled:opacity-50',
  },
})
export class AdultDangerOutlineButton {}

/** Tinted-outline accent — "Tilldela". */
@Directive({
  selector: '[adultSecondaryTintButton]',
  host: {
    class:
      'inline-flex min-h-11 items-center justify-center rounded-lg border border-adult-accent/25 bg-adult-accent-soft px-4 text-sm font-medium text-adult-accent-dark transition hover:bg-adult-accent-soft/60 disabled:pointer-events-none disabled:opacity-50',
  },
})
export class AdultSecondaryTintButton {}
