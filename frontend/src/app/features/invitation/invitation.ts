import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/http-error';
import { InvitationsService } from '../../core/api/invitations.service';
import { SessionService } from '../../core/auth/session.service';
import { InvitationPreview, InvitationStatus } from '../../core/models/member';
import { SignInPrompt } from '../../shared/ui/sign-in-prompt';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';

/**
 * The page an invitation link opens.
 *
 * The token lives in the URL fragment, so it is never sent to the server as
 * part of the page request or a Referer. The page waits for the session probe,
 * asks the API to preview the invitation for the signed-in address, and then
 * offers the two decisions. It is the API that decides whether the link is
 * for this account, still pending, or already decided.
 */
@Component({
  selector: 'app-invitation',
  imports: [DatePipe, RouterLink, SignInPrompt, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invitation.html',
  styleUrl: './invitation.css',
})
export class Invitation {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionService);
  private readonly invitations = inject(InvitationsService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly token = this.readToken();

  private started = false;

  readonly loading = signal(true);
  readonly signedOut = signal(false);
  readonly invalid = signal(false);
  readonly error = signal<string | null>(null);
  readonly preview = signal<InvitationPreview | null>(null);
  readonly saving = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly declined = signal(false);

  /** Where signing in comes back to, token fragment included. */
  protected readonly returnUrl = computed(() =>
    this.token.length === 0 ? '/trips' : `/invite#token=${this.token}`,
  );

  constructor() {
    effect(() => {
      if (this.token.length === 0) {
        this.loading.set(false);
        this.invalid.set(true);

        return;
      }

      if (!this.session.isReady()) {
        return;
      }

      if (!this.session.isAuthenticated()) {
        this.loading.set(false);
        this.signedOut.set(true);

        return;
      }

      this.signedOut.set(false);

      if (!this.started) {
        this.started = true;
        this.load();
      }
    });
  }

  protected retry(): void {
    this.load();
  }

  protected accept(): void {
    this.decide(() =>
      this.invitations.accept(this.token).subscribe({
        next: (accepted) => {
          this.saving.set(false);
          void this.router.navigate(['/trips', accepted.tripId]);
        },
        error: (failure: HttpErrorResponse) => this.fail(failure),
      }),
    );
  }

  protected decline(): void {
    this.decide(() =>
      this.invitations.decline(this.token).subscribe({
        next: () => {
          this.saving.set(false);
          this.declined.set(true);
        },
        error: (failure: HttpErrorResponse) => this.fail(failure),
      }),
    );
  }

  protected toneFor(status: InvitationStatus): BadgeTone {
    switch (status) {
      case 'Pending':
        return 'warning';
      case 'Accepted':
        return 'healthy';
      case 'Declined':
      case 'Revoked':
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected statusMessage(status: InvitationStatus): string {
    switch (status) {
      case 'Expired':
        return 'has expired';
      case 'Declined':
        return 'was declined';
      case 'Revoked':
        return 'was revoked';
      case 'Accepted':
        return 'has already been accepted';
      default:
        return 'is no longer available';
    }
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.invitations
      .preview(this.token)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (preview) => {
          this.preview.set(preview);
          this.loading.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.loading.set(false);

          if (failure.status === 401) {
            this.session.markSignedOut();
            this.signedOut.set(true);
            return;
          }

          if (failure.status === 404) {
            this.invalid.set(true);
            return;
          }

          this.error.set(describeHttpError(failure));
        },
      });
  }

  private decide(run: () => void): void {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    run();
  }

  private fail(failure: HttpErrorResponse): void {
    this.saving.set(false);

    if (failure.status === 401) {
      this.session.markSignedOut();
      this.signedOut.set(true);
      return;
    }

    if (failure.status === 404) {
      this.invalid.set(true);
      return;
    }

    this.actionError.set(describeHttpError(failure));
  }

  private readToken(): string {
    const fragment = this.route.snapshot.fragment;

    if (fragment !== null && fragment.length > 0) {
      const fromFragment = new URLSearchParams(fragment).get('token');

      if (fromFragment !== null && fromFragment.length > 0) {
        return fromFragment;
      }
    }

    return this.route.snapshot.queryParamMap.get('token') ?? '';
  }
}
