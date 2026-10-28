import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { CsrfTokenService } from './csrf-token.service';

const HEADER = 'X-XSRF-TOKEN';

const UNSAFE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/**
 * Proves to the API that a state-changing request came from this page.
 *
 * The token travels in a header rather than in the body, so it is added here
 * once for every write instead of by each caller. The first write of a page
 * load collects the token; after that it is reused until the session changes.
 */
export const csrfInterceptor: HttpInterceptorFn = (request, next) => {
  if (!UNSAFE_METHODS.has(request.method.toUpperCase())) {
    return next(request);
  }

  const tokens = inject(CsrfTokenService);
  const held = tokens.token();

  if (held !== null) {
    return next(withToken(request, held));
  }

  return tokens.load().pipe(switchMap((fresh) => next(withToken(request, fresh))));
};

function withToken(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { [HEADER]: token } });
}
