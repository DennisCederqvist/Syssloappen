import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ChildPageHeader } from './ui/page-header';
import { ChildSideNav } from './ui/side-nav';
import { ChildChoreAssignment } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

/** Child-facing settings. Currently just hosts "Senast godkända" — moved off
 * the main Idag page since kids won't visit it regularly (docs/barnvy
 * mockup.png's Idag screen has no room for it either). More settings content
 * can land here later. */
@Component({
  selector: 'app-child-settings-page',
  imports: [ChildSideNav, ChildPageHeader],
  templateUrl: './child-settings-page.html',
})
export class ChildSettingsPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly childChoresService = inject(ChildChoresService);

  readonly childName = computed(() => this.auth.user()?.name || 'där');
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
        error: () => this.loadError.set('Din historik kunde inte hämtas. Försök igen.'),
      });
  }
}
