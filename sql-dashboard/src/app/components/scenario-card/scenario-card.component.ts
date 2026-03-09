import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';

import { BenchmarkService } from '../../services/benchmark.service';
import { ScenarioInfo, ScenarioResult, ProcRun } from '../../models/scenario.models';

@Component({
  selector: 'app-scenario-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDividerModule,
  ],
  templateUrl: './scenario-card.component.html',
  styleUrl: './scenario-card.component.scss',
})
export class ScenarioCardComponent {
  readonly scenario = input.required<ScenarioInfo>();
  /** Allow the parent dashboard to push a result (from "Run All") */
  readonly externalResult = input<ScenarioResult | null>(null);

  private readonly benchmarkService = inject(BenchmarkService);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private readonly _result = signal<ScenarioResult | null>(null);

  /** Prefer result set by "Run All" from the parent, fall back to own result */
  readonly result = computed(() => this.externalResult() ?? this._result());

  readonly badRun = computed<ProcRun | null>(() =>
    this.result()?.runs.find((r) => r.isBad) ?? null
  );

  readonly fixedRuns = computed<ProcRun[]>(() =>
    this.result()?.runs.filter((r) => !r.isBad) ?? []
  );

  readonly bestFixedRun = computed<ProcRun | null>(() => {
    const runs = this.fixedRuns();
    if (!runs.length) return null;
    return runs.reduce((best, r) => (r.elapsedMs < best.elapsedMs ? r : best));
  });

  readonly improvement = computed<number>(
    () => this.result()?.improvementFactor ?? 0
  );

  readonly maxMs = computed<number>(() => {
    const r = this.result();
    if (!r) return 1;
    return Math.max(...r.runs.map((run) => run.elapsedMs), 1);
  });

  barWidth(ms: number): string {
    const pct = (ms / this.maxMs()) * 100;
    return `${Math.max(pct, 2)}%`;
  }

  formatMs(ms: number): string {
    if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
    return `${ms} ms`;
  }

  run(): void {
    this.loading.set(true);
    this.error.set(null);
    this._result.set(null);

    this.benchmarkService.runScenario(this.scenario().id).subscribe({
      next: (result) => {
        this._result.set(result);
        this.loading.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message ?? 'Request failed');
        this.loading.set(false);
      },
    });
  }

  scenarioIcon(id: number): string {
    const icons: Record<number, string> = {
      1: 'loop',
      2: 'memory',
      3: 'date_range',
      4: 'select_all',
      5: 'search',
      6: 'functions',
    };
    return icons[id] ?? 'bug_report';
  }

  scenarioColor(id: number): string {
    const colors: Record<number, string> = {
      1: '#e53935',
      2: '#8e24aa',
      3: '#fb8c00',
      4: '#039be5',
      5: '#00897b',
      6: '#6d4c41',
    };
    return colors[id] ?? '#546e7a';
  }
}
