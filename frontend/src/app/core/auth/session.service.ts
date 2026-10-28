import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, switchMap, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../api/auth.service';
import { Credentials, SessionUser } from '../models/session';
import { CsrfTokenService } from './csrf-token.service';

/**
 * Who the browser is signed in as.
 *
 * The first stateful service in the app: a signal the header reads, and the
 * three things that change it. Everything else in Tabiji is still anonymous,
 * so nothing protects a route on the strength of this yet.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly auth = inject(AuthService);
  private readonly csrf = inject(CsrfTokenService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly currentUser = signal<SessionUser | null>(null);
  private readonly ready = signal(false);

  /** The signed-in account, or null. */
  readonly user = this.currentUser.asReadonly();

  /** Whether anybody is signed in. */
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  /**
   * Whether the first question has been answered. Until it is, the shell
   * shows neither state, so a signed-in reader never sees "Sign in" flash.
   */
  readonly isReady = this.ready.asReadonly();

  /** Asks the API who this browser is. Failure means nobody. */
  restore(): void {
    this.auth
      .session()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.currentUser.set(session.user);
          this.ready.set(true);
        },
        error: () => {
          // An unreachable API must not blank the page; it just means the
          // reader is not known to be signed in.
          this.currentUser.set(null);
          this.ready.set(true);
        },
      });
  }

  /** Signs in, then reads back the account the API accepted. */
  signIn(credentials: Credentials): Observable<void> {
    return this.auth.login(credentials).pipe(
      tap(() => this.csrf.forget()),
      switchMap(() => this.auth.session()),
      tap((session) => {
        this.currentUser.set(session.user);
        this.ready.set(true);
      }),
      map(() => undefined),
    );
  }

  /**
   * Signs out. A stale cookie must not strand the reader, so a failed request
   * still clears the screen's idea of who they are.
   */
  signOut(): Observable<void> {
    return this.auth.logout().pipe(
      catchError(() => of(undefined)),
      tap(() => {
        this.currentUser.set(null);
        this.csrf.forget();
      }),
    );
  }
}
