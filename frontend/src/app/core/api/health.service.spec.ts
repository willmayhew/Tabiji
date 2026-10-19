import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HealthService } from './health.service';

describe('HealthService', () => {
  let service: HealthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(HealthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should request the liveness probe', () => {
    let status: string | undefined;

    service.getLiveness().subscribe((report) => (status = report.status));

    const request = http.expectOne('/health');
    expect(request.request.method).toBe('GET');
    request.flush({ status: 'Healthy', totalDurationMilliseconds: 1, checks: [] });

    expect(status).toBe('Healthy');
  });

  it('should request the readiness probe', () => {
    let status: string | undefined;

    service.getReadiness().subscribe((report) => (status = report.status));

    const request = http.expectOne('/health/ready');
    expect(request.request.method).toBe('GET');
    request.flush({ status: 'Healthy', totalDurationMilliseconds: 1, checks: [] });

    expect(status).toBe('Healthy');
  });
});
