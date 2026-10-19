import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

/**
 * Turns an API failure into a message that can be shown to the user.
 *
 * The API answers with an RFC 7807 problem document whose `detail` holds the
 * specific reason, for example "The trip name is required. (Parameter 'name')".
 * A status of 0 means the request never reached the API.
 */
export function describeHttpError(failure: HttpErrorResponse): string {
  if (failure.status === 0) {
    return 'The Tabiji API could not be reached. Is the backend running on http://localhost:5080?';
  }

  const problem = failure.error as ProblemDetails | undefined;
  const detail = problem?.detail;

  if (typeof detail === 'string' && detail.length > 0) {
    return detail;
  }

  const title = problem?.title;

  if (typeof title === 'string' && title.length > 0) {
    return title;
  }

  return `The API returned ${failure.status}.`;
}
