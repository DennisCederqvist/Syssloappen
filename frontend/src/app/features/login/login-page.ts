import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { CurrentUser, RegisterAdultResponse } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { focusAfterRender } from '../../shared/focus';

type LoginMode = 'adult' | 'child';
type AdultView = 'login' | 'register';
type ChildLoginMode = 'pairing' | 'fallback';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('password')?.value === control.get('confirmPassword')?.value
    ? null
    : { passwordMismatch: true };
}

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule],
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  readonly mode = signal<LoginMode>('adult');
  readonly adultView = signal<AdultView>('login');
  readonly childLoginMode = signal<ChildLoginMode>('pairing');
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly registrationResult = signal<RegisterAdultResponse | null>(null);
  readonly familyCodeCopied = signal(false);

  readonly adultForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly registrationForm = this.formBuilder.nonNullable.group(
    {
      householdName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
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
  readonly childForm = this.formBuilder.nonNullable.group({
    familyCode: ['', Validators.required],
    userName: ['', Validators.required],
    password: ['', Validators.required],
  });
  readonly pairingForm = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(8)]],
  });

  selectMode(mode: LoginMode): void {
    this.mode.set(mode);
    this.errorMessage.set('');
  }
  selectAdultView(view: AdultView): void {
    this.adultView.set(view);
    this.registrationResult.set(null);
    this.familyCodeCopied.set(false);
    this.errorMessage.set('');
  }
  selectChildLoginMode(mode: ChildLoginMode): void {
    this.childLoginMode.set(mode);
    this.errorMessage.set('');
  }
  submitAdult(): void {
    if (this.adultForm.invalid) {
      this.adultForm.markAllAsTouched();
      focusAfterRender(this.adultForm.controls.email.invalid ? 'adult-email' : 'adult-password');
      return;
    }
    this.signIn(this.auth.loginAdult(this.adultForm.getRawValue()));
  }
  submitRegistration(): void {
    if (this.registrationForm.invalid) {
      this.registrationForm.markAllAsTouched();
      const firstInvalidId = this.registrationForm.controls.householdName.invalid
        ? 'household-name'
        : this.registrationForm.controls.email.invalid
          ? 'registration-email'
          : this.registrationForm.controls.password.invalid
            ? 'registration-password'
            : 'confirm-registration-password';
      focusAfterRender(firstInvalidId);
      return;
    }

    const { householdName, email, password } = this.registrationForm.getRawValue();
    this.isSubmitting.set(true);
    this.errorMessage.set('');
    this.auth
      .registerAdult({ householdName, email, password })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (result) => this.registrationResult.set(result),
        error: (error: HttpErrorResponse) =>
          this.errorMessage.set(
            error.status === 400
              ? 'Kontot kunde inte skapas. E-postadressen kan redan användas eller uppgifterna behöver rättas.'
              : 'Något gick fel när kontot skapades. Försök igen om en liten stund.',
          ),
      });
  }

  continueToLogin(): void {
    const result = this.registrationResult();
    if (!result) return;
    this.adultForm.controls.email.setValue(result.email);
    this.adultView.set('login');
    this.registrationResult.set(null);
    this.familyCodeCopied.set(false);
  }

  async copyFamilyCode(): Promise<void> {
    const familyCode = this.registrationResult()?.familyCode;
    if (!familyCode) return;
    try {
      await navigator.clipboard.writeText(familyCode);
      this.familyCodeCopied.set(true);
    } catch {
      this.errorMessage.set(
        'Koden kunde inte kopieras automatiskt. Markera och kopiera den manuellt.',
      );
    }
  }
  submitChild(): void {
    if (this.childForm.invalid) {
      this.childForm.markAllAsTouched();
      focusAfterRender(
        this.childForm.controls.familyCode.invalid
          ? 'family-code'
          : this.childForm.controls.userName.invalid
            ? 'child-username'
            : 'child-password',
      );
      return;
    }
    this.signIn(this.auth.loginChild(this.childForm.getRawValue()));
  }
  submitPairingCode(): void {
    if (this.pairingForm.invalid) {
      this.pairingForm.markAllAsTouched();
      focusAfterRender('pairing-code-input');
      return;
    }
    this.signIn(this.auth.pairChildDevice(this.pairingForm.getRawValue()));
  }

  private signIn(request: Observable<CurrentUser>): void {
    this.isSubmitting.set(true);
    this.errorMessage.set('');
    request.pipe(finalize(() => this.isSubmitting.set(false))).subscribe({
      next: (user) => this.router.navigateByUrl(this.auth.homeFor(user.role)),
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(
          error.status === 429
            ? 'För många försök. Vänta en liten stund och försök igen.'
            : 'Inloggningen lyckades inte. Kontrollera uppgifterna och försök igen.',
        ),
    });
  }
}
