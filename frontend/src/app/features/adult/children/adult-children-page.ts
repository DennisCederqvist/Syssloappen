import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import {
  ChildDeviceSession,
  ChildPairingCode,
  ChildSummary,
  CreatedChild,
} from './children.models';
import { ChildrenService } from './children.service';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('password')?.value === control.get('confirmPassword')?.value
    ? null
    : { passwordMismatch: true };
}

@Component({
  selector: 'app-adult-children-page',
  imports: [AppBottomNav, DatePipe, ReactiveFormsModule, UserHeader],
  templateUrl: './adult-children-page.html',
})
export class AdultChildrenPage implements OnInit {
  private readonly childrenService = inject(ChildrenService);
  private readonly formBuilder = inject(FormBuilder);

  readonly children = signal<ChildSummary[]>([]);
  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly showForm = signal(false);
  readonly loadError = signal('');
  readonly formError = signal('');
  readonly createdChild = signal<CreatedChild | null>(null);
  readonly pairingCode = signal<(ChildPairingCode & { childName: string }) | null>(null);
  readonly generatingCodeFor = signal<number | null>(null);
  readonly pairingError = signal('');
  readonly pairingCodeCopied = signal(false);
  readonly deviceSessionsChild = signal<ChildSummary | null>(null);
  readonly deviceSessions = signal<ChildDeviceSession[]>([]);
  readonly deviceSessionsLoading = signal(false);
  readonly deviceSessionsError = signal('');
  readonly confirmingRevocation = signal<string | null>(null);
  readonly revokingSession = signal<string | null>(null);
  readonly revocationError = signal('');
  readonly editingChild = signal<ChildSummary | null>(null);
  readonly isUpdatingChild = signal(false);
  readonly editChildError = signal('');
  readonly editChildSuccess = signal('');
  readonly confirmingDeactivation = signal(false);
  readonly isDeactivatingChild = signal(false);
  readonly deactivationError = signal('');
  readonly deactivationSuccess = signal('');

  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', active: true, route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];

  readonly childForm = this.formBuilder.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(100)]],
      userName: ['', [Validators.required, Validators.maxLength(50)]],
      password: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/),
        ],
      ],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  readonly editChildForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    this.loadChildren();
  }

  loadChildren(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.childrenService
      .getActiveChildren()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (children) => this.children.set(children),
        error: () => this.loadError.set('Barnen kunde inte hämtas. Försök igen.'),
      });
  }

  openForm(): void {
    this.createdChild.set(null);
    this.formError.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formError.set('');
    this.childForm.reset();
  }

  submitChild(): void {
    if (this.childForm.invalid) {
      this.childForm.markAllAsTouched();
      return;
    }

    const { name, userName, password } = this.childForm.getRawValue();
    this.isSubmitting.set(true);
    this.formError.set('');
    this.childrenService
      .createChild({ name, userName, password })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (child) => {
          this.children.update((children) =>
            [...children, { id: child.id, name: child.name }].sort((a, b) =>
              a.name.localeCompare(b.name, 'sv'),
            ),
          );
          this.createdChild.set(child);
          this.showForm.set(false);
          this.childForm.reset();
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 409) {
            this.formError.set('Användarnamnet används redan av ett barn i familjen.');
          } else if (error.status === 400) {
            this.formError.set('Kontrollera namn, användarnamn och lösenord.');
          } else {
            this.formError.set('Barnkontot kunde inte skapas. Försök igen om en liten stund.');
          }
        },
      });
  }

  generatePairingCode(child: ChildSummary): void {
    this.generatingCodeFor.set(child.id);
    this.pairingError.set('');
    this.pairingCode.set(null);
    this.pairingCodeCopied.set(false);
    this.childrenService
      .createPairingCode(child.id)
      .pipe(finalize(() => this.generatingCodeFor.set(null)))
      .subscribe({
        next: (result) => this.pairingCode.set({ ...result, childName: child.name }),
        error: (error: HttpErrorResponse) =>
          this.pairingError.set(
            error.status === 404
              ? 'Barnet är inte längre aktivt eller saknar ett konto.'
              : 'Koden kunde inte skapas. Försök igen om en liten stund.',
          ),
      });
  }

  closePairingCode(): void {
    this.pairingCode.set(null);
    this.pairingCodeCopied.set(false);
  }

  async copyPairingCode(): Promise<void> {
    const code = this.pairingCode()?.code;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.pairingCodeCopied.set(true);
    } catch {
      this.pairingError.set('Koden kunde inte kopieras automatiskt. Kopiera den manuellt.');
    }
  }

  openDeviceSessions(child: ChildSummary): void {
    this.deviceSessionsChild.set(child);
    this.confirmingRevocation.set(null);
    this.revocationError.set('');
    this.loadDeviceSessions(child);
  }

  closeDeviceSessions(): void {
    this.deviceSessionsChild.set(null);
    this.deviceSessions.set([]);
    this.deviceSessionsError.set('');
    this.confirmingRevocation.set(null);
    this.revocationError.set('');
  }

  retryDeviceSessions(): void {
    const child = this.deviceSessionsChild();
    if (child) this.loadDeviceSessions(child);
  }

  requestRevocation(sessionId: string): void {
    this.confirmingRevocation.set(sessionId);
    this.revocationError.set('');
  }

  cancelRevocation(): void {
    this.confirmingRevocation.set(null);
  }

  revokeDeviceSession(session: ChildDeviceSession): void {
    const child = this.deviceSessionsChild();
    if (!child || this.revokingSession()) return;

    this.revokingSession.set(session.sessionId);
    this.revocationError.set('');
    this.childrenService
      .revokeDeviceSession(child.id, session.sessionId)
      .pipe(finalize(() => this.revokingSession.set(null)))
      .subscribe({
        next: () => {
          this.deviceSessions.update((sessions) =>
            sessions.map((current) =>
              current.sessionId === session.sessionId
                ? { ...current, revokedAt: new Date().toISOString() }
                : current,
            ),
          );
          this.confirmingRevocation.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.revocationError.set(
            error.status === 404
              ? 'Enheten finns inte längre. Uppdatera listan och försök igen.'
              : 'Enheten kunde inte loggas ut. Försök igen om en liten stund.',
          ),
      });
  }

  isSessionExpired(session: ChildDeviceSession): boolean {
    return new Date(session.expiresAt).getTime() <= Date.now();
  }

  openEditChild(child: ChildSummary): void {
    this.editingChild.set(child);
    this.editChildForm.setValue({ name: child.name });
    this.editChildError.set('');
    this.editChildSuccess.set('');
    this.confirmingDeactivation.set(false);
    this.deactivationError.set('');
  }

  closeEditChild(): void {
    this.editingChild.set(null);
    this.editChildForm.reset();
    this.editChildError.set('');
    this.editChildSuccess.set('');
    this.confirmingDeactivation.set(false);
    this.deactivationError.set('');
  }

  updateChild(): void {
    const child = this.editingChild();
    if (!child) return;
    if (this.editChildForm.invalid) {
      this.editChildForm.markAllAsTouched();
      return;
    }

    const name = this.editChildForm.getRawValue().name.trim();
    if (!name) {
      this.editChildForm.controls.name.setErrors({ required: true });
      this.editChildForm.controls.name.markAsTouched();
      return;
    }

    this.isUpdatingChild.set(true);
    this.editChildError.set('');
    this.editChildSuccess.set('');
    this.childrenService
      .updateChild(child.id, { name })
      .pipe(finalize(() => this.isUpdatingChild.set(false)))
      .subscribe({
        next: (updatedChild) => {
          this.children.update((children) =>
            children
              .map((current) => (current.id === updatedChild.id ? updatedChild : current))
              .sort((a, b) => a.name.localeCompare(b.name, 'sv')),
          );
          this.editingChild.set(updatedChild);
          this.editChildForm.setValue({ name: updatedChild.name });
          this.editChildSuccess.set('Namnet är uppdaterat.');
        },
        error: (error: HttpErrorResponse) =>
          this.editChildError.set(
            error.status === 404
              ? 'Barnet är inte längre aktivt eller finns inte i din familj.'
              : error.status === 400
                ? 'Skriv ett namn med högst 100 tecken.'
                : 'Namnet kunde inte uppdateras. Försök igen om en liten stund.',
          ),
      });
  }

  requestDeactivation(): void {
    this.confirmingDeactivation.set(true);
    this.deactivationError.set('');
  }

  cancelDeactivation(): void {
    this.confirmingDeactivation.set(false);
    this.deactivationError.set('');
  }

  deactivateChild(): void {
    const child = this.editingChild();
    if (!child || !this.confirmingDeactivation() || this.isDeactivatingChild()) return;

    this.isDeactivatingChild.set(true);
    this.deactivationError.set('');
    this.childrenService
      .deactivateChild(child.id)
      .pipe(finalize(() => this.isDeactivatingChild.set(false)))
      .subscribe({
        next: () => {
          this.children.update((children) => children.filter((current) => current.id !== child.id));
          if (this.deviceSessionsChild()?.id === child.id) this.closeDeviceSessions();
          if (this.createdChild()?.id === child.id) this.createdChild.set(null);
          this.closePairingCode();
          this.editingChild.set(null);
          this.editChildForm.reset();
          this.confirmingDeactivation.set(false);
          this.deactivationSuccess.set(`${child.name} är avaktiverad och visas inte längre.`);
        },
        error: (error: HttpErrorResponse) =>
          this.deactivationError.set(
            error.status === 404
              ? 'Barnet är redan avaktiverat eller finns inte i din familj.'
              : 'Barnet kunde inte avaktiveras. Försök igen om en liten stund.',
          ),
      });
  }

  private loadDeviceSessions(child: ChildSummary): void {
    this.deviceSessionsLoading.set(true);
    this.deviceSessionsError.set('');
    this.deviceSessions.set([]);
    this.childrenService
      .getDeviceSessions(child.id)
      .pipe(
        finalize(() => {
          if (this.deviceSessionsChild()?.id === child.id) this.deviceSessionsLoading.set(false);
        }),
      )
      .subscribe({
        next: (sessions) => {
          if (this.deviceSessionsChild()?.id === child.id) this.deviceSessions.set(sessions);
        },
        error: (error: HttpErrorResponse) => {
          if (this.deviceSessionsChild()?.id !== child.id) return;
          this.deviceSessionsError.set(
            error.status === 404
              ? 'Barnet finns inte längre i den aktiva familjevyn.'
              : 'De kopplade enheterna kunde inte hämtas. Försök igen.',
          );
        },
      });
  }
}
