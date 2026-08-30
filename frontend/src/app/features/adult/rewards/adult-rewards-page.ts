import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { focusAfterRender } from '../../../shared/focus';
import { UserHeader } from '../../../shared/user-header';
import { Reward } from './rewards.models';
import { RewardsService } from './rewards.service';

@Component({
  selector: 'app-adult-rewards-page',
  imports: [AppBottomNav, ReactiveFormsModule, UserHeader],
  templateUrl: './adult-rewards-page.html',
})
export class AdultRewardsPage implements OnInit {
  private readonly rewardsService = inject(RewardsService);
  private readonly formBuilder = inject(FormBuilder);
  private successTimer: number | null = null;
  private successClearTimer: number | null = null;
  readonly rewards = signal<Reward[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly formError = signal('');
  readonly successMessage = signal('');
  readonly successFading = signal(false);
  readonly editing = signal<Reward | null>(null);
  readonly showForm = signal(false);
  readonly confirmingId = signal<number | null>(null);
  readonly openRewardMenuId = signal<number | null>(null);
  readonly busyId = signal<number | null>(null);
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Belöningar', icon: '★', active: true, route: '/vuxen/belöningar' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];
  readonly rewardForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    pointsCost: [1, [Validators.required, Validators.min(1), Validators.pattern(/^[0-9]+$/)]],
    stockQuantity: [1, [Validators.required, Validators.min(0), Validators.pattern(/^[0-9]+$/)]],
  });

  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.rewardsService
      .getRewards()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (rewards) => this.rewards.set(rewards),
        error: () => this.loadError.set('Belöningarna kunde inte hämtas. Försök igen.'),
      });
  }
  openCreate(): void {
    this.showForm.set(true);
    this.editing.set(null);
    this.formError.set('');
    this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
    focusAfterRender('reward-name');
  }
  openEdit(reward: Reward): void {
    this.showForm.set(true);
    this.editing.set(reward);
    this.formError.set('');
    this.rewardForm.setValue({
      name: reward.name,
      description: reward.description ?? '',
      pointsCost: reward.pointsCost,
      stockQuantity: reward.stockQuantity,
    });
    focusAfterRender('reward-name');
  }
  closeForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
    this.formError.set('');
    this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
  }
  save(): void {
    if (this.rewardForm.invalid) {
      this.rewardForm.markAllAsTouched();
      focusAfterRender('reward-name');
      return;
    }
    const value = this.rewardForm.getRawValue();
    const name = value.name.trim();
    if (!name) {
      this.rewardForm.controls.name.setErrors({ required: true });
      this.rewardForm.controls.name.markAsTouched();
      return;
    }
    const request = {
      name,
      description: value.description.trim() || null,
      pointsCost: value.pointsCost,
      stockQuantity: value.stockQuantity,
    };
    const existing = this.editing();
    this.busyId.set(existing?.id ?? 0);
    this.formError.set('');
    const operation = existing
      ? this.rewardsService.updateReward(existing.id, request)
      : this.rewardsService.createReward(request);
    operation.pipe(finalize(() => this.busyId.set(null))).subscribe({
      next: (reward) => {
        this.rewards.update((items) =>
          [...items.filter((item) => item.id !== reward.id), reward].sort((a, b) =>
            a.name.localeCompare(b.name, 'sv'),
          ),
        );
        this.editing.set(null);
        this.showForm.set(false);
        this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
        this.showSuccess(`${reward.name} är ${existing ? 'uppdaterad' : 'skapad'}.`);
        focusAfterRender('rewards-success');
      },
      error: (error: HttpErrorResponse) =>
        this.formError.set(
          error.status === 400
            ? 'Kontrollera namn, beskrivning och poängpris.'
            : 'Belöningen kunde inte sparas. Försök igen.',
        ),
    });
  }
  requestDeactivate(id: number): void {
    this.openRewardMenuId.set(null);
    this.confirmingId.set(id);
    this.successMessage.set('');
    focusAfterRender(`cancel-deactivate-reward-${id}`);
  }
  toggleRewardMenu(id: number): void {
    this.openRewardMenuId.update((current) => (current === id ? null : id));
  }
  cancelDeactivate(): void {
    const id = this.confirmingId();
    this.confirmingId.set(null);
    if (id) focusAfterRender(`deactivate-reward-${id}`);
  }
  deactivate(reward: Reward): void {
    if (this.confirmingId() !== reward.id || this.busyId() !== null) return;
    this.busyId.set(reward.id);
    this.rewardsService
      .deactivateReward(reward.id)
      .pipe(finalize(() => this.busyId.set(null)))
      .subscribe({
        next: () => {
          this.rewards.update((items) => items.filter((item) => item.id !== reward.id));
          this.confirmingId.set(null);
          this.showSuccess(`${reward.name} är bortplockad från belöningslistan.`);
          focusAfterRender('rewards-success');
        },
        error: () => this.formError.set('Belöningen kunde inte plockas bort. Försök igen.'),
      });
  }

  private showSuccess(message: string): void {
    if (this.successTimer !== null) window.clearTimeout(this.successTimer);
    if (this.successClearTimer !== null) window.clearTimeout(this.successClearTimer);
    this.successFading.set(false);
    this.successMessage.set(message);
    this.successTimer = window.setTimeout(() => this.successFading.set(true), 3000);
    this.successClearTimer = window.setTimeout(() => {
      this.successMessage.set('');
      this.successTimer = null;
      this.successClearTimer = null;
      this.successFading.set(false);
    }, 5000);
  }
}
