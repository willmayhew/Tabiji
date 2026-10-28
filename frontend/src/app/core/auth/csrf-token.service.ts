import { inject, Injectable, signal } from '@angular/core';
import { map, Observable, tap } from 'rxjs';
import { AuthService } from '../api/auth.service';

/**
 * The antiforgery token for this page, held in memory.
 *
 * It is never written to storage: it is only useful while the page is open,
 * and anything that could read storage could also read it.
 */
@Injectable({ providedIn: 'root' })
export class CsrfTokenService {
  private readonly auth = inject(AuthService);
  private readonly current = signal<string | null>(null);

  token(): string | null {
    return this.current();
  }

  /** Collects a token, replacing any held. */
  load(): Observable<string> {
    return this.auth.csrf().pipe(
      tap((response) => this.current.set(response.token)),
      map((response) => response.token),
    );
  }

  /**
   * Forgets the token. Signing in or out changes the account inside it, so a
   * token collected before the change is no longer accepted.
   */
  forget(): void {
    this.current.set(null);
  }
}
