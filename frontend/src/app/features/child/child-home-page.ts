import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { focusAfterRender } from '../../shared/focus';
import { vibrateOnTap } from '../../shared/haptics';
import { ChildCardMotion } from './ui/card-motion';
import { CHILD_CARD_PALETTES } from './ui/palette';
import { ChildPageHeader } from './ui/page-header';
import { ChildSideNav } from './ui/side-nav';
import { ChildStatusCard } from './ui/status-card';
import { ChildTaskCard, ChildTaskCardPalette } from './ui/task-card';
import { ChildChoreAssignment } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({
  selector: 'app-child-home-page',
  imports: [ChildSideNav, ChildPageHeader, ChildTaskCard, ChildStatusCard, TranslocoPipe],
  templateUrl: './child-home-page.html',
})
export class ChildHomePage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly childChoresService = inject(ChildChoresService);
  private readonly transloco = inject(TranslocoService);
  private readonly motion = new ChildCardMotion(() =>
    this.actionableAssignments().map((assignment) => assignment.assignmentId),
  );

  readonly childName = computed(() => {
    this.transloco.activeLang();
    return this.auth.user()?.name || this.transloco.translate('child.common.fallbackName');
  });
  readonly wobblingAssignmentId = this.motion.wobblingId;
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
    this.transloco.activeLang();
    const remaining = this.actionableAssignments().length;
    if (remaining === 0) return this.transloco.translate('child.home.motivationDone');
    if (remaining === 1) return this.transloco.translate('child.home.motivationOne');
    return this.transloco.translate('child.home.motivationMany', { count: remaining });
  });

  ngOnInit(): void {
    this.loadPage();
    this.motion.start();
  }

  ngOnDestroy(): void {
    this.motion.stop();
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
        error: () => this.loadError.set(this.transloco.translate('child.home.loadError')),
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
          const message = this.transloco.translate(
            error.status === 404
              ? 'child.home.assignmentError404'
              : error.status === 409
                ? 'child.home.assignmentError409'
                : 'child.home.assignmentErrorGeneric',
          );
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
    return CHILD_CARD_PALETTES[index % CHILD_CARD_PALETTES.length];
  }

  tiltFor(assignmentId: number): number {
    return this.motion.tiltFor(assignmentId);
  }
}
