import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { CurrentUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

type LoginMode = 'adult' | 'child';
type ChildLoginMode = 'pairing' | 'fallback';

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
  readonly childLoginMode = signal<ChildLoginMode>('pairing');
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');

  readonly adultForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  readonly childForm = this.formBuilder.nonNullable.group({
    familyCode: ['', Validators.required],
    userName: ['', Validators.required],
    password: ['', Validators.required],
  });
  readonly pairingForm = this.formBuilder.nonNullable.group({
    code: ['', Validators.required],
  });

  selectMode(mode: LoginMode): void {
    this.mode.set(mode);
    this.errorMessage.set('');
  }
  selectChildLoginMode(mode: ChildLoginMode): void {
    this.childLoginMode.set(mode);
    this.errorMessage.set('');
  }
  submitAdult(): void {
    if (this.adultForm.invalid) {
      this.adultForm.markAllAsTouched();
      return;
    }
    this.signIn(this.auth.loginAdult(this.adultForm.getRawValue()));
  }
  submitChild(): void {
    if (this.childForm.invalid) {
      this.childForm.markAllAsTouched();
      return;
    }
    this.signIn(this.auth.loginChild(this.childForm.getRawValue()));
  }
  submitPairingCode(): void {
    if (this.pairingForm.invalid) {
      this.pairingForm.markAllAsTouched();
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
