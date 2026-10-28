import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Credentials, CsrfToken, Session } from '../models/session';

/**
 * The account routes.
 *
 * Sign-in is delegated to ASP.NET Core Identity: this asks for the cookie and
 * never handles a password beyond handing it over. `useCookies` is what makes
 * the API answer with a cookie rather than a bearer token, and the browser
 * stores it where no script can read it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** Creates an account. It does not sign the reader in. */
  register(credentials: Credentials): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/api/auth/register`, credentials);
  }

  /** Signs in and receives the authentication cookie. */
  login(credentials: Credentials): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/api/auth/login?useCookies=true`,
      credentials,
    );
  }

  /** Clears the authentication cookie. */
  logout(): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/api/auth/logout`, {});
  }

  /** Reports who the browser is, or that it is nobody. */
  session(): Observable<Session> {
    return this.http.get<Session>(`${environment.apiBaseUrl}/api/auth/session`);
  }

  /** Collects the antiforgery token, and the cookie that goes with it. */
  csrf(): Observable<CsrfToken> {
    return this.http.get<CsrfToken>(`${environment.apiBaseUrl}/api/auth/csrf`);
  }
}
