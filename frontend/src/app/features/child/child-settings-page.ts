import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PushNotificationsService } from '../../core/push/push-notifications.service';
import { ChildLanguageToggle } from './ui/language-toggle';
import { ChildPageHeader } from './ui/page-header';
import { ChildSideNav } from './ui/side-nav';
import { ChildChoreAssignment } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

/** Child-facing settings. Currently hosts the language toggle and "Senast
 * godkända" — moved off the main Idag page since kids won't visit it
 * regularly (docs/barnvy mockup.png's Idag screen has no room for it
 * either). More settings content can land here later. */
@Component({
  selector: 'app-child-settings-page',
  imports: [ChildSideNav, ChildLanguageToggle, ChildPageHeader, TranslocoPipe],
  templateUrl: './child-settings-page.html',
})
export class ChildSettingsPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly childChoresService = inject(ChildChoresService);
  private readonly transloco = inject(TranslocoService);
  protected readonly push = inject(PushNotificationsService);

  readonly isTogglingPush = signal(false);
  readonly pushError = signal('');

  readonly childName = computed(() => {
    this.transloco.activeLang();
    return this.auth.user()?.name || this.transloco.translate('child.common.fallbackName');
  });
  readonly childPhotoUrl = computed(() => this.auth.user()?.photoUrl ?? null);
  readonly assignments = signal<ChildChoreAssignment[]>([]);
  readonly availablePoints = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal('');

  readonly recentlyApprovedAssignments = computed(() =>
    this.assignments()
      .filter((assignment) => assignment.status === 'Approved')
      .slice(0, 5),
  );

  ngOnInit(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    forkJoin({
      assignments: this.childChoresService.getAssignments(),
      rewards: this.childChoresService.getRewards(),
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ assignments, rewards }) => {
          this.assignments.set(assignments);
          this.availablePoints.set(rewards.availablePoints);
        },
        error: () => this.loadError.set(this.transloco.translate('child.settings.loadError')),
      });
  }

  async togglePush(): Promise<void> {
    if (this.isTogglingPush()) return;
    this.isTogglingPush.set(true);
    this.pushError.set('');
    try {
      if (this.push.isSubscribed()) {
        await this.push.disable();
      } else {
        await this.push.enable();
      }
    } catch {
      this.pushError.set(this.transloco.translate('child.settings.notificationsError'));
    } finally {
      this.isTogglingPush.set(false);
    }
  }
}
