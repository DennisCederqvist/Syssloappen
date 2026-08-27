import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { AdultRewardRedemption, RewardRedemptionsService } from './reward-redemptions.service';

@Component({
  selector: 'app-adult-reward-redemptions-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './adult-reward-redemptions-page.html',
})
export class AdultRewardRedemptionsPage implements OnInit {
  private readonly service = inject(RewardRedemptionsService);
  private archiveUndoTimer: number | null = null;
  private archiveUndoClearTimer: number | null = null;
  readonly items = signal<AdultRewardRedemption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly busyId = signal<number | null>(null);
  readonly comments = signal<Readonly<Record<number, string>>>({});
  readonly lastArchivedId = signal<number | null>(null);
  readonly archiveUndoFading = signal(false);
  readonly showHiddenHistory = signal(false);
  readonly pending = computed(() => this.items().filter((item) => item.status === 'Requested'));
  readonly active = computed(() => this.items().filter((item) => item.status === 'Requested' || item.status === 'Approved'));
  readonly history = computed(() => this.items().filter((item) => !item.adultArchivedAt && (item.status === 'Cancelled' || item.status === 'Delivered')).slice(0, 10));
  readonly historyItems = computed(() => this.items().filter((item) =>
    (item.status === 'Cancelled' || item.status === 'Delivered') && (!item.adultArchivedAt || item.id === this.lastArchivedId()),
  ).slice(0, 10));
  readonly hiddenHistory = computed(() => this.items().filter((item) => item.adultArchivedAt && (item.status === 'Cancelled' || item.status === 'Delivered')));
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: 'H', route: '/vuxen' },
    { label: 'Beloningar', icon: '*', route: '/vuxen/beloningar' },
    { label: 'Onskningar', icon: '+', active: true, route: '/vuxen/onskningar' },
  ];

  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading.set(true); this.error.set('');
    this.service.get().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (items) => this.items.set(items), error: () => this.error.set('Forfragningarna kunde inte hamtas.'),
    });
  }
  setComment(id: number, comment: string): void {
    this.comments.update((items) => ({ ...items, [id]: comment }));
  }
  change(item: AdultRewardRedemption, action: 'approve' | 'cancel' | 'deliver'): void {
    if (this.busyId() !== null) return;
    const rawComment = this.comments()[item.id] ?? '';
    if (rawComment.length > 500) {
      this.error.set('Kommentaren far vara hogst 500 tecken.');
      return;
    }
    this.busyId.set(item.id);
    this.service.change(item.id, action, rawComment.trim() || null).pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: (updated) => this.items.update((items) => items.map((current) => current.id === updated.id ? updated : current)),
      error: (response: HttpErrorResponse) => this.error.set(response.status === 409 ? 'Forfragningen har redan hanterats. Uppdatera listan.' : 'Andringen kunde inte sparas.'),
    });
  }
  archive(item: AdultRewardRedemption): void {
    if (this.busyId() !== null) return;
    this.busyId.set(item.id);
    this.service.archive(item.id).pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: () => {
        this.items.update((items) => items.map((current) => current.id === item.id ? { ...current, adultArchivedAt: new Date().toISOString() } : current));
        this.showArchiveUndo(item.id);
      },
      error: () => this.error.set('Historiken kunde inte döljas.'),
    });
  }
  restore(item: AdultRewardRedemption): void {
    if (this.busyId() !== null) return;
    this.busyId.set(item.id);
    this.service.restore(item.id).pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: () => {
        this.items.update((items) => items.map((current) => current.id === item.id ? { ...current, adultArchivedAt: null } : current));
        this.clearArchiveUndo();
      },
      error: () => this.error.set('Historiken kunde inte återställas.'),
    });
  }

  private showArchiveUndo(id: number): void {
    this.clearArchiveUndo();
    this.lastArchivedId.set(id);
    this.archiveUndoTimer = window.setTimeout(() => this.archiveUndoFading.set(true), 3000);
    this.archiveUndoClearTimer = window.setTimeout(() => this.clearArchiveUndo(), 5000);
  }

  private clearArchiveUndo(): void {
    if (this.archiveUndoTimer !== null) window.clearTimeout(this.archiveUndoTimer);
    if (this.archiveUndoClearTimer !== null) window.clearTimeout(this.archiveUndoClearTimer);
    this.archiveUndoTimer = null;
    this.archiveUndoClearTimer = null;
    this.archiveUndoFading.set(false);
    this.lastArchivedId.set(null);
  }
}
