import { Component } from '@angular/core';

/** Surface + border + 8px radius + 12px padding container — the base building
 * block every other adult-view component (Tile, ApprovalCard) visually echoes. */
@Component({
  selector: 'app-adult-card',
  template: `<div class="rounded-lg border border-adult-border bg-adult-surface p-3">
    <ng-content />
  </div>`,
})
export class AdultCard {}
