import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';
import { RewardRedemption, RewardRedemptionStatus } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({
  selector: 'app-child-redemptions-page',
  imports: [AppBottomNav, DatePipe, UserHeader],
  templateUrl: './child-redemptions-page.html',
})
export class ChildRedemptionsPage implements OnInit {
  private readonly service = inject(ChildChoresService);
  readonly items = signal<RewardRedemption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly activeItems = computed(() =>
    this.items().filter((item) => item.status === 'Requested' || item.status === 'Approved'),
  );
  readonly recentFinalItems = computed(() =>
    this.items()
      .filter((item) => item.status === 'Cancelled' || item.status === 'Delivered')
      .slice(0, 5),
  );
  readonly navItems: NavItem[] = [
    { label: 'Idag', icon: 'H', route: '/barn' },
    { label: 'Belöningar', icon: '*', route: '/barn/beloningar' },
    { label: 'Önskningar', icon: '+', active: true, route: '/barn/onskningar' },
  ];
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.service
      .getRewardRedemptions()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.items.set(items),
        error: () => this.error.set('Dina önskningar kunde inte hämtas.'),
      });
  }
  label(status: RewardRedemptionStatus): string {
    return {
      Requested: 'Väntar på vuxen',
      Approved: 'Godkänd',
      Cancelled: 'Avslag',
      Delivered: 'Utlämnad',
    }[status];
  }
}
