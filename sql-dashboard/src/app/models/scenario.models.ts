/** API response from GET /health */
export interface HealthResponse {
  status: string;
  timestamp: string;
}

/** Lightweight scenario description (no benchmark data) from GET /scenarios */
export interface ScenarioInfo {
  id: number;
  name: string;
  antipattern: string;
  fix: string;
  badProcedure: string;
  fixedProcedure: string;
}

/** A single stored procedure execution result */
export interface ProcRun {
  procedure: string;
  label: string;
  elapsedMs: number;
  rowCount: number;
  isBad: boolean;
}

/** Full benchmark result for one scenario from GET /scenarios/{id} */
export interface ScenarioResult {
  id: number;
  name: string;
  antipattern: string;
  fix: string;
  runs: ProcRun[];
  improvementFactor: number;
  ranAt: string;
}

/** Aggregate result from GET /scenarios/all */
export interface AllScenariosResult {
  totalElapsedMs: number;
  scenarios: ScenarioResult[];
  ranAt: string;
}

export type HealthStatus = 'healthy' | 'unhealthy' | 'unknown' | 'checking';

/** UI state for an individual scenario card */
export interface ScenarioCardState {
  info: ScenarioInfo;
  result: ScenarioResult | null;
  loading: boolean;
  error: string | null;
}
