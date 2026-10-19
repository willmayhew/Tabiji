export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

export interface HealthCheck {
  name: string;
  status: HealthStatus;
  description: string | null;
}

export interface HealthReport {
  status: HealthStatus;
  totalDurationMilliseconds: number;
  checks: HealthCheck[];
}
