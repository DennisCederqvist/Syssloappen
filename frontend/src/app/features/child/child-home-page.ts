import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { focusAfterRender } from '../../shared/focus';
import { vibrateOnTap } from '../../shared/haptics';
import { ChildPageHeader } from './ui/page-header';
import { ChildSideNav } from './ui/side-nav';
import { ChildStatusCard } from './ui/status-card';
import { ChildTaskCard, ChildTaskCardPalette } from './ui/task-card';
import { ChildChoreAssignment } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

const PALETTES: ChildTaskCardPalette[] = ['blue', 'pink', 'yellow', 'peach', 'mint'];
const WOBBLE_DELAYS_S = [0, 2, 4, 5, 7, 8];

@Component({
  selector: 'app-child-home-page',
  imports: [ChildSideNav, ChildPageHeader, ChildTaskCard, ChildStatusCard],
  templateUrl: './child-home-page.html',
})
export class ChildHomePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly childChoresService = inject(ChildChoresService);

  readonly childName = computed(() => this.auth.user()?.name || 'där');
  readonly assignments = signal<ChildChoreAssignment[]>([]);
  readonly availablePoints = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly submittingAssignmentIds = signal<ReadonlySet<number>>(new Set());
  readonly submissionErrors = signal<Readonly<Record<number, string>>>({});
  readonly actionableAssignments = computed(() =>
    this.assignments().filter((assignment) => this.canSubmit(assignment)),
  );
  readonly pendingAssignments = computed(() =>
    this.assignments().filter((assignment) => assignment.status === 'PendingApproval'),
  );

  readonly motivationMessage = computed(() => {
    const remaining = this.actionableAssignments().length;
    if (remaining === 0) return 'Du är klar med allt för idag — bra jobbat!';
    if (remaining === 1) return 'Du har 1 syssla kvar idag — du fixar det!';
    return `Du har ${remaining} sysslor kvar idag — du fixar det!`;
  });

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
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
        error: () => this.loadError.set('Dina sysslor och poäng kunde inte hämtas. Försök igen.'),
      });
  }

  submitAssignment(assignment: ChildChoreAssignment): void {
    if (!this.canSubmit(assignment) || this.isSubmitting(assignment.assignmentId)) return;

    vibrateOnTap();
    this.submittingAssignmentIds.update((ids) => new Set(ids).add(assignment.assignmentId));
    this.submissionErrors.update(({ [assignment.assignmentId]: _, ...errors }) => errors);

    this.childChoresService
      .submitAssignment(assignment.assignmentId)
      .pipe(
        finalize(() =>
          this.submittingAssignmentIds.update((ids) => {
            const next = new Set(ids);
            next.delete(assignment.assignmentId);
            return next;
          }),
        ),
      )
      .subscribe({
        next: (submitted) => {
          this.assignments.update((assignments) =>
            assignments.map((item) =>
              item.assignmentId === submitted.assignmentId
                ? {
                    ...item,
                    status: submitted.status,
                    submittedAt: submitted.submittedAt,
                    reviewComment: null,
                  }
                : item,
            ),
          );
          focusAfterRender(`child-assignment-${assignment.assignmentId}`);
        },
        error: (error: HttpErrorResponse) => {
          const message =
            error.status === 404
              ? 'Sysslan finns inte längre i din lista.'
              : error.status === 409
                ? 'Sysslan har redan ändrats. Uppdatera listan och försök igen.'
                : 'Sysslan kunde inte rapporteras. Försök igen.';
          this.submissionErrors.update((errors) => ({
            ...errors,
            [assignment.assignmentId]: message,
          }));
        },
      });
  }

  canSubmit(assignment: ChildChoreAssignment): boolean {
    return assignment.status === 'Assigned' || assignment.status === 'NeedsRedo';
  }

  isSubmitting(assignmentId: number): boolean {
    return this.submittingAssignmentIds().has(assignmentId);
  }

  paletteFor(index: number): ChildTaskCardPalette {
    return PALETTES[index % PALETTES.length];
  }

  tiltFor(index: number): number {
    return index % 2 === 0 ? -2 : 2;
  }

  wobbleDelayFor(index: number): number {
    return WOBBLE_DELAYS_S[index % WOBBLE_DELAYS_S.length];
  }
}
