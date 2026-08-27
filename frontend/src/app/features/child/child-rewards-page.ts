import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';
import { ChildReward } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({ selector: 'app-child-rewards-page', imports: [AppBottomNav, UserHeader], templateUrl: './child-rewards-page.html' })
export class ChildRewardsPage implements OnInit {
  private readonly service = inject(ChildChoresService);
  readonly rewards = signal<ChildReward[]>([]);
  readonly availablePoints = signal(0);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly busyId = signal<number | null>(null);
  readonly navItems: NavItem[] = [
    { label: 'Idag', icon: 'H', route: '/barn' },
    { label: 'Belöningar', icon: '*', active: true, route: '/barn/beloningar' },
    { label: 'Önskningar', icon: '+', route: '/barn/onskningar' },
  ];
  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading.set(true); this.error.set('');
    this.service.getRewards().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (result) => { this.rewards.set(result.rewards); this.availablePoints.set(result.availablePoints); },
      error: () => this.error.set('Belöningarna kunde inte hämtas. Försök igen.'),
    });
  }
  request(reward: ChildReward): void {
    if (this.busyId() !== null) return;
    this.busyId.set(reward.id); this.error.set('');
    this.service.requestReward(reward.id, crypto.randomUUID()).pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: (redemption) => { this.availablePoints.set(redemption.availablePoints); this.rewards.update((items) => items.filter((item) => item.id !== reward.id)); },
      error: (error: HttpErrorResponse) => this.error.set(error.status === 409 ? 'Belöningen är redan önskad eller poängen räcker inte.' : 'Belöningen kunde inte önskas. Försök igen.'),
    });
  }
}
