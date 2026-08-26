import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AdultChildrenPage } from './adult-children-page';
import { CreateChildRequest, UpdateChildRequest } from './children.models';
import { ChildrenService } from './children.service';

class FakeChildrenService {
  createCalls = 0;
  pairingCalls: number[] = [];
  deviceSessionCalls: number[] = [];
  revokeCalls: { childId: number; sessionId: string }[] = [];
  updateCalls: { childId: number; request: UpdateChildRequest }[] = [];
  deactivateCalls: number[] = [];

  getActiveChildren() {
    return of([{ id: 1, name: 'Leo' }]);
  }

  createChild(request: CreateChildRequest) {
    this.createCalls += 1;
    return of({ id: 2, name: request.name, userName: request.userName, role: 'Child' as const });
  }

  createPairingCode(childId: number) {
    this.pairingCalls.push(childId);
    return of({ code: 'ABC234XY', expiresAt: '2026-08-25T20:10:00Z' });
  }

  getDeviceSessions(childId: number) {
    this.deviceSessionCalls.push(childId);
    return of([
      {
        sessionId: '11111111-1111-1111-1111-111111111111',
        createdAt: '2026-08-25T10:00:00Z',
        lastSeenAt: '2026-08-25T12:00:00Z',
        expiresAt: '2099-08-31T12:00:00Z',
        absoluteExpiresAt: '2099-09-24T10:00:00Z',
        revokedAt: null,
      },
    ]);
  }

  revokeDeviceSession(childId: number, sessionId: string) {
    this.revokeCalls.push({ childId, sessionId });
    return of(void 0);
  }

  updateChild(childId: number, request: UpdateChildRequest) {
    this.updateCalls.push({ childId, request });
    return of({ id: childId, name: request.name });
  }

  deactivateChild(childId: number) {
    this.deactivateCalls.push(childId);
    return of(void 0);
  }
}

describe('AdultChildrenPage', () => {
  let component: AdultChildrenPage;
  let service: FakeChildrenService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdultChildrenPage],
      providers: [provideRouter([]), { provide: ChildrenService, useClass: FakeChildrenService }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdultChildrenPage);
    component = fixture.componentInstance;
    service = TestBed.inject(ChildrenService) as unknown as FakeChildrenService;
    component.ngOnInit();
  });

  it('does not submit when the passwords differ', () => {
    component.childForm.setValue({
      name: 'Maja',
      userName: 'maja',
      password: 'Secret12',
      confirmPassword: 'Different12',
    });

    component.submitChild();

    expect(component.childForm.hasError('passwordMismatch')).toBe(true);
    expect(service.createCalls).toBe(0);
  });

  it('adds a successfully created child to the visible list', () => {
    component.childForm.setValue({
      name: 'Maja',
      userName: 'maja',
      password: 'Secret12',
      confirmPassword: 'Secret12',
    });

    component.submitChild();

    expect(service.createCalls).toBe(1);
    expect(component.children()).toEqual([
      { id: 1, name: 'Leo' },
      { id: 2, name: 'Maja' },
    ]);
    expect(component.createdChild()?.userName).toBe('maja');
  });

  it('keeps the one-time pairing code only in page state', () => {
    component.generatePairingCode({ id: 1, name: 'Leo' });

    expect(service.pairingCalls).toEqual([1]);
    expect(component.pairingCode()).toEqual({
      childId: 1,
      childName: 'Leo',
      code: 'ABC234XY',
      expiresAt: '2026-08-25T20:10:00Z',
    });

    component.closePairingCode();
    expect(component.pairingCode()).toBeNull();
  });

  it('connects expandable child panels to accessible controls', () => {
    const fixture = TestBed.createComponent(AdultChildrenPage);
    fixture.detectChanges();

    const createButton = fixture.nativeElement.querySelector(
      '#create-child-trigger',
    ) as HTMLButtonElement;
    expect(createButton.getAttribute('aria-controls')).toBeNull();
    expect(createButton.getAttribute('aria-expanded')).toBe('false');

    createButton.click();
    fixture.detectChanges();
    expect(createButton.getAttribute('aria-expanded')).toBe('true');
    expect(createButton.getAttribute('aria-controls')).toBe('create-child-panel');
    expect(fixture.nativeElement.querySelector('#create-child-panel')).not.toBeNull();
  });

  it('loads device sessions for the selected child', () => {
    component.openDeviceSessions({ id: 1, name: 'Leo' });

    expect(service.deviceSessionCalls).toEqual([1]);
    expect(component.deviceSessionsChild()).toEqual({ id: 1, name: 'Leo' });
    expect(component.deviceSessions()).toHaveLength(1);
  });

  it('revokes a confirmed device session and marks it logged out', () => {
    component.openDeviceSessions({ id: 1, name: 'Leo' });
    const session = component.deviceSessions()[0];
    component.requestRevocation(session.sessionId);

    component.revokeDeviceSession(session);

    expect(service.revokeCalls).toEqual([
      { childId: 1, sessionId: '11111111-1111-1111-1111-111111111111' },
    ]);
    expect(component.deviceSessions()[0].revokedAt).not.toBeNull();
    expect(component.confirmingRevocation()).toBeNull();
  });

  it('updates the selected child name in the visible list', () => {
    component.openEditChild({ id: 1, name: 'Leo' });
    component.editChildForm.setValue({ name: 'Leon' });

    component.updateChild();

    expect(service.updateCalls).toEqual([{ childId: 1, request: { name: 'Leon' } }]);
    expect(component.children()).toContainEqual({ id: 1, name: 'Leon' });
    expect(component.editingChild()).toEqual({ id: 1, name: 'Leon' });
  });

  it('requires confirmation before deactivating and removes the child after success', () => {
    component.openEditChild({ id: 1, name: 'Leo' });

    component.deactivateChild();
    expect(service.deactivateCalls).toEqual([]);

    component.requestDeactivation();
    component.deactivateChild();

    expect(service.deactivateCalls).toEqual([1]);
    expect(component.children()).toEqual([]);
    expect(component.editingChild()).toBeNull();
    expect(component.deactivationSuccess()).toContain('Leo');
  });
});
