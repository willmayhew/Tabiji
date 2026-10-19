import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { HealthService } from '../../core/api/health.service';
import { HealthReport, HealthStatus } from '../../core/models/health-status';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';

@Component({
  selector: 'app-home',
  imports: [StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly healthService = inject(HealthService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly liveness = signal<HealthReport | null>(null);
  protected readonly readiness = signal<HealthReport | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      liveness: this.healthService.getLiveness().pipe(catchError(() => of(null))),
      readiness: this.healthService.getReadiness().pipe(catchError(() => of(null))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ liveness, readiness }) => {
        this.liveness.set(liveness);
        this.readiness.set(readiness);
        this.error.set(
          liveness === null && readiness === null
            ? 'The Tabiji API is not reachable. Start it with "dotnet run --project backend/Tabiji.Api" and try again.'
            : null,
        );
        this.loading.set(false);
      });
  }

  protected toneFor(status: HealthStatus): BadgeTone {
    switch (status) {
      case 'Healthy':
        return 'healthy';
      case 'Degraded':
        return 'warning';
      default:
        return 'danger';
    }
  }
}
