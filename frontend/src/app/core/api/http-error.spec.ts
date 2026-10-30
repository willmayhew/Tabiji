import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from './http-error';

function failure(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('describeHttpError', () => {
  it('should say when the API cannot be reached', () => {
    expect(describeHttpError(failure(0))).toContain('could not be reached');
  });

  it('should say when the session has expired', () => {
    expect(describeHttpError(failure(401))).toBe('Your session has expired. Sign in again.');
  });

  it('should say when the trip is not the reader’s', () => {
    expect(describeHttpError(failure(403))).toBe('You do not have access to this trip.');
  });

  it('should keep the API’s own reason', () => {
    const response = failure(400, { detail: 'The trip name is required.' });

    expect(describeHttpError(response)).toBe('The trip name is required.');
  });

  it('should fall back to the problem title', () => {
    const response = failure(404, { title: 'Trip not found' });

    expect(describeHttpError(response)).toBe('Trip not found');
  });

  it('should fall back to the status when the body says nothing', () => {
    expect(describeHttpError(failure(500))).toBe('The API returned 500.');
  });
});
