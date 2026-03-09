import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { BenchmarkService } from '../../services/benchmark.service';
import {
  AllScenariosResult,
  HealthStatus,
  ScenarioInfo,
  ScenarioResult,
} from '../../models/scenario.models';
import { ScenarioCardComponent } from '../scenario-card/scenario-card.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDividerModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
    MatSnackBarModule,
    ScenarioCardComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly benchmarkService = inject(BenchmarkService);
  private readonly snackBar = inject(MatSnackBar);

  /* ── Scenarios metadata ───────────────────────────────────────── */
  readonly scenarios = signal<ScenarioInfo[]>([]);
  readonly scenariosLoading = signal(false);

  /* ── Health ───────────────────────────────────────────────────── */
  readonly healthStatus = signal<HealthStatus>('unknown');

  /* ── Run All ──────────────────────────────────────────────────── */
  readonly runAllLoading = signal(false);
  readonly allResults = signal<AllScenariosResult | null>(null);
  readonly scenarioResults = signal<Map<number, ScenarioResult>>(new Map());

  /* ── Summary table columns ────────────────────────────────────── */
  readonly summaryColumns = [
    'scenario', 'antipattern', 'badMs', 'fixedMs', 'improvement', 'rows',
  ];

  /* ── Computed stats ───────────────────────────────────────────── */
  readonly totalMs = computed(() => this.allResults()?.totalElapsedMs ?? 0);
  readonly ranAt = computed(() => this.allResults()?.ranAt ?? null);

  readonly summaryRows = computed(() =>
    this.allResults()?.scenarios ?? []
  );

  ngOnInit(): void {
    this.loadScenarios();
    this.checkHealth();
  }

  /* ── Load scenario metadata ───────────────────────────────────── */
  loadScenarios(): void {
    this.scenariosLoading.set(true);
    this.benchmarkService.listScenarios().subscribe({
      next: (list) => {
        this.scenarios.set(list);
        this.scenariosLoading.set(false);
      },
      error: () => {
        this.scenariosLoading.set(false);
        this.snackBar.open('Could not load scenarios — is the API running?', 'Dismiss', {
          duration: 5000,
          panelClass: 'error-snack',
        });
      },
    });
  }

  /* ── Health check ─────────────────────────────────────────────── */
  checkHealth(): void {
    this.healthStatus.set('checking');
    this.benchmarkService.health().subscribe({
      next: (h) => this.healthStatus.set(h.status === 'healthy' ? 'healthy' : 'unhealthy'),
      error: () => this.healthStatus.set('unhealthy'),
    });
  }

  /* ── Run all scenarios ────────────────────────────────────────── */
  runAll(): void {
    this.runAllLoading.set(true);
    this.allResults.set(null);
    this.benchmarkService.runAll().subscribe({
      next: (result) => {
        this.allResults.set(result);
        // Push each result into the map so cards can display it
        const map = new Map<number, ScenarioResult>();
        result.scenarios.forEach((s) => map.set(s.id, s));
        this.scenarioResults.set(map);
        this.runAllLoading.set(false);
        this.snackBar.open(
          `All scenarios completed in ${(result.totalElapsedMs / 1000).toFixed(1)}s`,
          '✓',
          { duration: 4000 }
        );
      },
      error: (err: Error) => {
        this.runAllLoading.set(false);
        this.snackBar.open(`Run All failed: ${err.message}`, 'Dismiss', {
          duration: 6000,
          panelClass: 'error-snack',
        });
      },
    });
  }

  /* ── Helpers ──────────────────────────────────────────────────── */
  resultFor(id: number): ScenarioResult | null {
    return this.scenarioResults().get(id) ?? null;
  }

  bestFixedMs(scenario: ScenarioResult): number {
    const fixed = scenario.runs.filter((r) => !r.isBad);
    return fixed.length ? Math.min(...fixed.map((r) => r.elapsedMs)) : 0;
  }

  badMs(scenario: ScenarioResult): number {
    return scenario.runs.find((r) => r.isBad)?.elapsedMs ?? 0;
  }

  healthIcon(): string {
    const s = this.healthStatus();
    if (s === 'healthy') return 'check_circle';
    if (s === 'unhealthy') return 'error';
    if (s === 'checking') return 'autorenew';
    return 'help_outline';
  }

  healthLabel(): string {
    const s = this.healthStatus();
    if (s === 'healthy') return 'API is healthy';
    if (s === 'unhealthy') return 'API unreachable';
    if (s === 'checking') return 'Checking…';
    return 'Status unknown';
  }

  totalRowCount(): number {
    const results = this.allResults();
    if (!results) return 0;
    return results.scenarios.reduce((sum, s) => {
      const fixedRun = s.runs.find((r) => !r.isBad);
      return sum + (fixedRun?.rowCount ?? 0);
    }, 0);
  }
}
