import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { HouseholdInvitation } from '../../../core/auth/auth.models';
import { focusAfterRender } from '../../../shared/focus';
import { AdultBottomNav } from '../ui/bottom-nav';
import {
  AdultDangerOutlineButton,
  AdultPrimaryButton,
  AdultSecondaryTintButton,
} from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultSheet } from '../ui/sheet';
import { HouseholdAdultsService } from './household-adults.service';
import { HouseholdAdult } from './household.models';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('newPassword')?.value === control.get('confirmNewPassword')?.value
    ? null
    : { passwordMismatch: true };
}

@Component({
  selector: 'app-adult-manage-adults-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    AdultBottomNav,
    AdultPrimaryButton,
    AdultSecondaryTintButton,
    AdultDangerOutlineButton,
    AdultPageHeader,
    AdultSheet,
  ],
  templateUrl: './adult-manage-adults-page.html',
})
export class AdultManageAdultsPage implements OnInit {
  private readonly householdAdults = inject(HouseholdAdultsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  readonly adults = signal<HouseholdAdult[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly ownUserId = this.auth.user()?.userId ?? null;

  readonly invitation = signal<HouseholdInvitation | null>(null);
  readonly isCreatingInvitation = signal(false);
  readonly invitationError = signal('');
  readonly invitationCopied = signal(false);

  readonly selectedAdult = signal<HouseholdAdult | null>(null);
  readonly confirmingDisconnect = signal(false);
  readonly isDisconnecting = signal(false);
  readonly disconnectError = signal('');

  readonly isSavingProfile = signal(false);
  readonly profileError = signal('');
  readonly profileSuccess = signal('');

  readonly isChangingPassword = signal(false);
  readonly passwordError = signal('');
  readonly passwordSuccess = signal('');

  readonly profileForm = this.formBuilder.nonNullable.group({
    firstName: ['', Validators.maxLength(100)],
    lastName: ['', Validators.maxLength(100)],
    nickname: ['', Validators.maxLength(50)],
  });

  readonly passwordForm = this.formBuilder.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: [
        '',
        [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/)],
      ],
      confirmNewPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.householdAdults
      .list()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (adults) => this.adults.set(adults),
        error: () => this.loadError.set('De vuxna kunde inte hämtas. Försök igen.'),
      });
  }

  createInvitation(): void {
    this.isCreatingInvitation.set(true);
    this.invitationError.set('');
    this.invitationCopied.set(false);
    this.auth
      .createHouseholdInvitation()
      .pipe(finalize(() => this.isCreatingInvitation.set(false)))
      .subscribe({
        next: (invitation) => this.invitation.set(invitation),
        error: () => this.invitationError.set('Inbjudningskoden kunde inte skapas. Försök igen.'),
      });
  }

  async copyInvitationCode(): Promise<void> {
    const code = this.invitation()?.code;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.invitationCopied.set(true);
    } catch {
      this.invitationError.set('Koden kunde inte kopieras automatiskt. Kopiera den manuellt.');
    }
  }

  openAdult(adult: HouseholdAdult): void {
    this.selectedAdult.set(adult);
    this.confirmingDisconnect.set(false);
    this.disconnectError.set('');
    this.profileError.set('');
    this.profileSuccess.set('');
    this.passwordError.set('');
    this.passwordSuccess.set('');
    this.passwordForm.reset();

    if (adult.id === this.ownUserId) {
      const user = this.auth.user();
      this.profileForm.setValue({
        firstName: user?.firstName ?? '',
        lastName: user?.lastName ?? '',
        nickname: user?.nickname ?? '',
      });
    }

    focusAfterRender('adult-detail-panel');
  }

  closeAdult(): void {
    const closedId = this.selectedAdult()?.id;
    this.selectedAdult.set(null);
    if (closedId) focusAfterRender(`adult-card-${closedId}`);
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const { firstName, lastName, nickname } = this.profileForm.getRawValue();
    this.isSavingProfile.set(true);
    this.profileError.set('');
    this.profileSuccess.set('');
    this.householdAdults
      .updateOwnProfile({
        firstName: firstName || null,
        lastName: lastName || null,
        nickname: nickname || null,
      })
      .pipe(finalize(() => this.isSavingProfile.set(false)))
      .subscribe({
        next: (updated) => {
          this.auth.updateOwnDisplayFields({ firstName, lastName, nickname });
          this.adults.update((adults) =>
            adults.map((current) => (current.id === updated.id ? updated : current)),
          );
          this.selectedAdult.set(updated);
          this.profileSuccess.set('Uppgifterna är sparade.');
        },
        error: () => this.profileError.set('Uppgifterna kunde inte sparas. Försök igen.'),
      });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.isChangingPassword.set(true);
    this.passwordError.set('');
    this.passwordSuccess.set('');
    this.householdAdults
      .changeOwnPassword({ currentPassword, newPassword })
      .pipe(finalize(() => this.isChangingPassword.set(false)))
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.passwordSuccess.set('Lösenordet är ändrat.');
        },
        error: (error: HttpErrorResponse) =>
          this.passwordError.set(
            error.status === 400
              ? 'Nuvarande lösenord stämmer inte, eller det nya lösenordet uppfyller inte kraven.'
              : 'Lösenordet kunde inte ändras. Försök igen.',
          ),
      });
  }

  requestDisconnect(): void {
    this.confirmingDisconnect.set(true);
    this.disconnectError.set('');
    focusAfterRender('cancel-disconnect');
  }

  cancelDisconnect(): void {
    this.confirmingDisconnect.set(false);
    focusAfterRender('request-disconnect');
  }

  disconnect(): void {
    const adult = this.selectedAdult();
    if (!adult || this.isDisconnecting()) return;

    this.isDisconnecting.set(true);
    this.disconnectError.set('');
    const isSelf = adult.id === this.ownUserId;

    this.householdAdults
      .disconnect(adult.id)
      .pipe(finalize(() => this.isDisconnecting.set(false)))
      .subscribe({
        next: () => {
          if (isSelf) {
            this.auth.forgetSession();
            this.router.navigateByUrl('/login');
            return;
          }
          this.adults.update((adults) => adults.filter((current) => current.id !== adult.id));
          this.selectedAdult.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.disconnectError.set(
            error.status === 404
              ? 'Den vuxna finns inte längre i familjen. Uppdatera listan och försök igen.'
              : error.status === 409
                ? 'Den här personen kan inte kopplas bort just nu.'
                : 'Kunde inte koppla bort. Försök igen om en liten stund.',
          ),
      });
  }
}
