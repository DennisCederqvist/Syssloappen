import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';
import { focusAfterRender } from '../../../shared/focus';
import { AdultBadge } from '../ui/badge';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultDangerOutlineButton, AdultPrimaryButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultSheet } from '../ui/sheet';
import { AdultTile } from '../ui/tile';
import { Reward } from './rewards.models';
import { RewardsService } from './rewards.service';

@Component({
  selector: 'app-adult-rewards-page',
  imports: [
    ReactiveFormsModule,
    AdultBadge,
    AdultBottomNav,
    AdultDangerOutlineButton,
    AdultPrimaryButton,
    AdultPageHeader,
    AdultSheet,
    AdultTile,
    TranslocoPipe,
  ],
  templateUrl: './adult-rewards-page.html',
})
export class AdultRewardsPage implements OnInit {
  private readonly rewardsService = inject(RewardsService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly transloco = inject(TranslocoService);
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
  readonly pendingImageFile = signal<File | null>(null);
  readonly imagePreviewUrl = signal<string | null>(null);
  readonly uploadingImage = signal(false);
  readonly imageError = signal('');
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
        error: () => this.loadError.set(this.transloco.translate('adult.rewards.loadError')),
      });
  }
  openCreate(): void {
    this.showForm.set(true);
    this.editing.set(null);
    this.formError.set('');
    this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
    this.resetImageState(null);
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
    this.resetImageState(reward.imageUrl);
    focusAfterRender('reward-name');
  }
  closeForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
    this.formError.set('');
    this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
    this.resetImageState(null);
  }
  selectImage(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) return;
    this.imageError.set('');
    const previous = this.pendingImageFile();
    if (previous) URL.revokeObjectURL(this.imagePreviewUrl()!);
    this.pendingImageFile.set(file);
    this.imagePreviewUrl.set(URL.createObjectURL(file));
  }
  private resetImageState(existingImageUrl: string | null): void {
    if (this.pendingImageFile()) URL.revokeObjectURL(this.imagePreviewUrl()!);
    this.pendingImageFile.set(null);
    this.imagePreviewUrl.set(existingImageUrl);
    this.imageError.set('');
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
        this.applyRewardToList(reward);
        const pendingImage = this.pendingImageFile();
        if (pendingImage) this.uploadPendingImage(reward.id, pendingImage);
        this.editing.set(null);
        this.showForm.set(false);
        this.rewardForm.reset({ name: '', description: '', pointsCost: 1, stockQuantity: 1 });
        this.resetImageState(null);
        this.showSuccess(
          this.transloco.translate(
            existing ? 'adult.rewards.saveSuccessUpdated' : 'adult.rewards.saveSuccessCreated',
            { name: reward.name },
          ),
        );
        focusAfterRender('rewards-success');
      },
      error: (error: HttpErrorResponse) =>
        this.formError.set(
          this.transloco.translate(
            error.status === 400
              ? 'adult.rewards.saveError.validation'
              : 'adult.rewards.saveError.generic',
          ),
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
          this.showSuccess(
            this.transloco.translate('adult.rewards.deactivateSuccess', { name: reward.name }),
          );
          focusAfterRender('rewards-success');
        },
        error: () =>
          this.formError.set(this.transloco.translate('adult.rewards.deactivateError')),
      });
  }

  private applyRewardToList(reward: Reward): void {
    this.rewards.update((items) =>
      [...items.filter((item) => item.id !== reward.id), reward].sort((a, b) =>
        a.name.localeCompare(b.name, 'sv'),
      ),
    );
  }
  private uploadPendingImage(rewardId: number, file: File): void {
    this.uploadingImage.set(true);
    this.rewardsService
      .uploadImage(rewardId, file)
      .pipe(finalize(() => this.uploadingImage.set(false)))
      .subscribe({
        next: (reward) => this.applyRewardToList(reward),
        error: () =>
          this.imageError.set(this.transloco.translate('adult.rewards.formSheet.imageUploadError')),
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
