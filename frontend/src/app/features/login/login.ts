import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/http-error';
import { SessionService } from '../../core/auth/session.service';

/**
 * Signing in.
 *
 * The password is handed to Identity and never inspected here. What this
 * screen adds is the message a reader needs when it is refused, and the way
 * back to the page they were trying to reach.
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
})
export class Login {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  /** Whether the reader has just created the account they are signing into. */
  protected readonly registered =
    this.route.snapshot.queryParamMap.get('registered') === 'true';

  protected submit(): void {
    this.error.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    this.session
      .signIn(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          void this.router.navigateByUrl(this.returnUrl());
        },
        error: (failure: HttpErrorResponse) => {
          this.submitting.set(false);

          // The API never says which of the two was wrong, and neither does
          // this screen: saying so would tell an attacker which addresses
          // have accounts.
          this.error.set(
            failure.status === 401
              ? 'The email or password is incorrect.'
              : describeHttpError(failure),
          );
        },
      });
  }

  protected errorFor(field: 'email' | 'password'): string | null {
    const control = this.form.controls[field];

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required')) {
      return 'This field is required.';
    }

    if (control.hasError('email')) {
      return 'Use an email address, like you@example.com.';
    }

    return null;
  }

  /**
   * Where to go after signing in. Only a path on this site is honoured, so a
   * crafted link cannot send the reader to another origin.
   */
  private returnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');

    return requested !== null && requested.startsWith('/') && !requested.startsWith('//')
      ? requested
      : '/';
  }
}
