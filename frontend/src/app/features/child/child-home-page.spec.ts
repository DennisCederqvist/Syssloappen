import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ChildChoreAssignment, SubmittedChildChoreAssignment } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';
import { ChildHomePage } from './child-home-page';

const assignedChore: ChildChoreAssignment = {
  assignmentId: 7,
  choreId: 3,
  title: 'Mata katten',
  description: 'Fyll både mat och vatten.',
  points: 10,
  assignedAt: '2026-08-26T08:00:00Z',
  status: 'Assigned',
  submittedAt: null,
  reviewComment: null,
};

class FakeChildChoresService {
  assignments = [assignedChore];
  points = 25;
  rewards = { availablePoints: 25, rewards: [] };
  submitCalls: number[] = [];
  submission = new Subject<SubmittedChildChoreAssignment>();

  getAssignments() {
    return of(this.assignments);
  }

  getPoints() {
    return of({ totalPoints: this.points });
  }

  getRewards() {
    return of(this.rewards);
  }

  submitAssignment(assignmentId: number) {
    this.submitCalls.push(assignmentId);
    return this.submission.asObservable();
  }
}

describe('ChildHomePage', () => {
  let component: ChildHomePage;
  let fixture: ComponentFixture<ChildHomePage>;
  let service: FakeChildChoresService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChildHomePage],
      providers: [
        provideRouter([]),
        { provide: ChildChoresService, useClass: FakeChildChoresService },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ChildHomePage);
    component = fixture.componentInstance;
    service = TestBed.inject(ChildChoresService) as unknown as FakeChildChoresService;
    fixture.detectChanges();
  });

  it('shows real assignments and the authenticated child points', () => {
    expect(component.assignments()).toEqual([assignedChore]);
    expect(component.totalPoints()).toBe(25);
    expect(fixture.nativeElement.textContent).toContain('Mata katten');
    expect(fixture.nativeElement.textContent).toContain('25');
  });

  it('shows the adult comment and a submit button for NeedsRedo', () => {
    component.assignments.set([
      { ...assignedChore, status: 'NeedsRedo', reviewComment: 'Glöm inte vattnet.' },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Behöver göras om');
    expect(fixture.nativeElement.textContent).toContain('Glöm inte vattnet.');
    expect(
      fixture.nativeElement.querySelector('button[aria-label="Rapportera Mata katten som klar"]'),
    ).not.toBeNull();
  });

  it('prevents duplicate submission and updates the card immediately after success', () => {
    component.submitAssignment(assignedChore);
    component.submitAssignment(assignedChore);
    expect(service.submitCalls).toEqual([7]);
    expect(component.isSubmitting(7)).toBe(true);

    service.submission.next({
      assignmentId: 7,
      status: 'PendingApproval',
      submittedAt: '2026-08-26T09:00:00Z',
    });
    service.submission.complete();
    fixture.detectChanges();

    expect(component.assignments()[0].status).toBe('PendingApproval');
    expect(component.assignments()[0].reviewComment).toBeNull();
    expect(component.isSubmitting(7)).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Väntar på godkännande');
  });

  it('does not offer submission for pending or approved assignments', () => {
    component.assignments.set([
      { ...assignedChore, status: 'PendingApproval' },
      { ...assignedChore, assignmentId: 8, status: 'Approved' },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('button[aria-label^="Rapportera "]').length).toBe(
      0,
    );
  });

  it('shows a clear submission conflict on the correct card', () => {
    service.submitAssignment = (assignmentId: number) => {
      service.submitCalls.push(assignmentId);
      return throwError(() => new HttpErrorResponse({ status: 409 }));
    };
    component.submitAssignment(assignedChore);
    fixture.detectChanges();

    expect(component.submissionErrors()[7]).toContain('redan ändrats');
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(
      'Uppdatera listan',
    );
  });

  it('shows an empty state when there are no assignments', () => {
    component.assignments.set([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Inga sysslor just nu');
  });

  it('shows a load error and lets the child retry', () => {
    service.getAssignments = () => throwError(() => new HttpErrorResponse({ status: 500 }));
    component.loadPage();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('kunde inte hämtas');
    const retryButton = Array.from<HTMLButtonElement>(
      fixture.nativeElement.querySelectorAll('button'),
    ).find((button) => button.textContent?.includes('Försök igen'));
    expect(retryButton).toBeDefined();

    service.getAssignments = () => of([]);
    retryButton?.click();
    fixture.detectChanges();
    expect(component.loadError()).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Inga sysslor just nu');
  });
});
