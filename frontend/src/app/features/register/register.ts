import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/api/auth.service';
import { describeHttpError } from '../../core/api/http-error';
import {
  PASSWORD_MIN_LENGTH,
  passwordStrength,
  passwordsMatch,
} from '../../shared/validate/password';

/**
 * Creating an account.
 *
 * Registration does not sign the reader in: the API creates the account and
 * issues no cookie, so this hands them to the sign-in screen. The API's own
 * error codes are translated into messages beside the field they belong to,
 * because "PasswordTooShort" is not a sentence.
 */
@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;

  readonly form = this.formBuilder.group(
    {
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, passwordStrength]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);

  protected submit(): void {
    this.submitted.set(true);
    this.error.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    const { email, password } = this.form.getRawValue();

    this.auth
      .register({ email, password })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          void this.router.navigate(['/login'], { queryParams: { registered: 'true' } });
        },
        error: (failure: HttpErrorResponse) => {
          this.submitting.set(false);
          this.reportFailure(failure);
        },
      });
  }

  protected errorFor(field: 'email' | 'password' | 'confirmPassword'): string | null {
    const control = this.form.controls[field];
    const fromServer = control.errors?.['server'];

    if (typeof fromServer === 'string') {
      return fromServer;
    }

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required')) {
      return 'This field is required.';
    }

    if (control.hasError('email')) {
      return 'Use an email address, like you@example.com.';
    }

    if (control.hasError('passwordStrength')) {
      return `Use at least ${PASSWORD_MIN_LENGTH} characters, with an upper-case letter, a lower-case letter and a digit.`;
    }

    return null;
  }

  protected matchError(): string | null {
    if (!this.form.hasError('passwordsMatch')) {
      return null;
    }

    const confirmation = this.form.controls.confirmPassword;

    return this.submitted() || confirmation.touched ? 'The two passwords do not match.' : null;
  }

  /**
   * Turns the API's validation codes into a message on the field that caused
   * them. Anything that cannot be placed is shown as one message above the
   * button.
   */
  private reportFailure(failure: HttpErrorResponse): void {
    const errors = (failure.error as { errors?: Record<string, string[] | undefined> } | null)
      ?.errors;

    if (errors === undefined) {
      this.error.set(describeHttpError(failure));
      return;
    }

    let placed = false;

    if (errors['DuplicateEmail'] !== undefined || errors['DuplicateUserName'] !== undefined) {
      this.form.controls.email.setErrors({
        server: 'An account already uses this address.',
      });
      placed = true;
    }

    const passwordProblem =
      errors['PasswordTooShort'] ??
      errors['PasswordRequiresDigit'] ??
      errors['PasswordRequiresLower'] ??
      errors['PasswordRequiresUpper'];

    if (passwordProblem !== undefined && passwordProblem.length > 0) {
      this.form.controls.password.setErrors({ server: passwordProblem[0] });
      placed = true;
    }

    if (!placed) {
      this.error.set(describeHttpError(failure));
    }
  }
}
