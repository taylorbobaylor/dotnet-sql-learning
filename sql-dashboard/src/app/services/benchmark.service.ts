import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AllScenariosResult,
  HealthResponse,
  ScenarioInfo,
  ScenarioResult,
} from '../models/scenario.models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BenchmarkService {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  private url(path: string): string {
    const base = this.baseUrl;
    return base ? `${base}${path}` : path;
  }

  health(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(this.url('/health'));
  }

  listScenarios(): Observable<ScenarioInfo[]> {
    return this.http.get<ScenarioInfo[]>(this.url('/scenarios'));
  }

  runScenario(id: number): Observable<ScenarioResult> {
    return this.http.get<ScenarioResult>(this.url(`/scenarios/${id}`));
  }

  runAll(): Observable<AllScenariosResult> {
    return this.http.get<AllScenariosResult>(this.url('/scenarios/all'));
  }
}
