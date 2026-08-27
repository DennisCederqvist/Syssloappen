import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { AdultAssignment, ReviewedAssignment } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import { AdultReviewPage } from './adult-review-page';

const pendingAssignment: AdultAssignment = {
  assignmentId: 11,
  choreId: 4,
  choreTitle: 'Mata hunden',
  childId: 7,
  childName: 'Maja',
  points: 5,
  assignedAt: '2026-08-26T08:00:00Z',
  dueDate: '2026-08-26',
  status: 'PendingApproval',
  submittedAt: '2026-08-26T09:00:00Z',
  reviewedByUserId: null,
  reviewedAt: null,
  reviewComment: null,
  cancelledByUserId: null,
  cancelledAt: null,
  adultArchivedAt: null,
};

class FakeChoresService {
  assignments: AdultAssignment[] = [
    pendingAssignment,
    {
      ...pendingAssignment,
      assignmentId: 12,
      choreTitle: 'Bädda sängen',
      status: 'Approved',
      reviewedAt: '2026-08-26T08:30:00Z',
    },
  ];
  approveCalls: { assignmentId: number; comment: string | null }[] = [];
  rejectCalls: { assignmentId: number; comment: string | null }[] = [];
  reviewResponse = new Subject<ReviewedAssignment>();

  getAssignments() {
    return of(this.assignments);
  }

  approveAssignment(assignmentId: number, request: { comment: string | null }) {
    this.approveCalls.push({ assignmentId, comment: request.comment });
    return this.reviewResponse.asObservable();
  }

  rejectAssignment(assignmentId: number, request: { comment: string | null }) {
    this.rejectCalls.push({ assignmentId, comment: request.comment });
    return this.reviewResponse.asObservable();
  }
}

describe('AdultReviewPage', () => {
  let fixture: ComponentFixture<AdultReviewPage>;
  let component: AdultReviewPage;
  let service: FakeChoresService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdultReviewPage],
      providers: [provideRouter([]), { provide: ChoresService, useClass: FakeChoresService }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdultReviewPage);
    component = fixture.componentInstance;
    service = TestBed.inject(ChoresService) as unknown as FakeChoresService;
    fixture.detectChanges();
  });

  it('shows pending work and previous reviews separately', () => {
    expect(component.pendingAssignments()).toHaveLength(1);
    expect(component.reviewedAssignments()).toHaveLength(1);
    expect(fixture.nativeElement.textContent).toContain('Mata hunden');
    expect(fixture.nativeElement.textContent).toContain('Senaste granskningar');
  });

  it('approves once with a trimmed optional comment and updates the card immediately', () => {
    component.setComment(11, '  Bra jobbat!  ');
    component.reviewAssignment(pendingAssignment, 'approve');
    component.reviewAssignment(pendingAssignment, 'approve');
    expect(service.approveCalls).toEqual([{ assignmentId: 11, comment: 'Bra jobbat!' }]);

    service.reviewResponse.next({
      assignmentId: 11,
      status: 'Approved',
      reviewedAt: '2026-08-26T10:00:00Z',
      reviewComment: 'Bra jobbat!',
      pointsAwarded: 5,
    });
    service.reviewResponse.complete();
    fixture.detectChanges();

    expect(component.pendingAssignments()).toHaveLength(0);
    expect(component.reviewedAssignments()).toHaveLength(2);
    expect(component.successMessage()).toContain('5 poäng');
  });

  it('rejects with only the assignment id and comment', () => {
    component.setComment(11, '  Fyll på vattnet.  ');
    component.reviewAssignment(pendingAssignment, 'reject');
    expect(service.rejectCalls).toEqual([{ assignmentId: 11, comment: 'Fyll på vattnet.' }]);

    service.reviewResponse.next({
      assignmentId: 11,
      status: 'NeedsRedo',
      reviewedAt: '2026-08-26T10:00:00Z',
      reviewComment: 'Fyll på vattnet.',
      pointsAwarded: null,
    });
    service.reviewResponse.complete();

    expect(component.assignments()[0].status).toBe('NeedsRedo');
    expect(component.assignments()[0].reviewComment).toBe('Fyll på vattnet.');
  });

  it('gives both review actions descriptive accessible names', () => {
    expect(
      fixture.nativeElement.querySelector('button[aria-label="Godkänn Mata hunden för Maja"]'),
    ).not.toBeNull();
    expect(
      fixture.nativeElement.querySelector('button[aria-label="Be Maja göra om Mata hunden"]'),
    ).not.toBeNull();
  });

  it('shows a conflict on the correct assignment', () => {
    service.approveAssignment = () => throwError(() => new HttpErrorResponse({ status: 409 }));
    component.reviewAssignment(pendingAssignment, 'approve');
    fixture.detectChanges();

    expect(component.reviewErrors()[11]).toContain('redan granskats');
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(
      'Uppdatera listan',
    );
  });

  it('shows an empty state when nothing is waiting', () => {
    component.assignments.set([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Inget väntar på granskning');
  });
});
