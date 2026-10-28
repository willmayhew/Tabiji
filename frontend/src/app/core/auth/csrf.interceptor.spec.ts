import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CsrfTokenService } from './csrf-token.service';
import { csrfInterceptor } from './csrf.interceptor';

describe('csrfInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should leave a read alone', () => {
    client.get('/api/trips').subscribe();

    const request = http.expectOne('/api/trips');

    expect(request.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    request.flush([]);
  });

  it('should collect a token before the first write of the page', () => {
    client.post('/api/trips', { name: 'Japan' }).subscribe();

    const token = http.expectOne('/api/auth/csrf');
    expect(token.request.method).toBe('GET');
    token.flush({ token: 'abc' });

    const write = http.expectOne('/api/trips');
    expect(write.request.headers.get('X-XSRF-TOKEN')).toBe('abc');
    write.flush({});
  });

  it('should reuse the token it already holds', () => {
    const tokens = TestBed.inject(CsrfTokenService);

    tokens.load().subscribe();
    http.expectOne('/api/auth/csrf').flush({ token: 'abc' });

    client.put('/api/trips/1', { name: 'Japan' }).subscribe();

    const write = http.expectOne('/api/trips/1');
    expect(write.request.headers.get('X-XSRF-TOKEN')).toBe('abc');
    write.flush({});
  });

  it('should attach the token again after the session forgets it', () => {
    const tokens = TestBed.inject(CsrfTokenService);

    tokens.load().subscribe();
    http.expectOne('/api/auth/csrf').flush({ token: 'first' });

    tokens.forget();

    client.delete('/api/trips/1').subscribe();

    http.expectOne('/api/auth/csrf').flush({ token: 'second' });

    const write = http.expectOne('/api/trips/1');
    expect(write.request.headers.get('X-XSRF-TOKEN')).toBe('second');
    write.flush(null);
  });
});
