import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { HealthReport } from '../models/health-status';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  /** Liveness probe. Does not touch the database. */
  getLiveness(): Observable<HealthReport> {
    return this.http.get<HealthReport>(`${environment.apiBaseUrl}/health`);
  }

  /** Readiness probe. Verifies that the database is reachable. */
  getReadiness(): Observable<HealthReport> {
    return this.http.get<HealthReport>(`${environment.apiBaseUrl}/health/ready`);
  }
}
