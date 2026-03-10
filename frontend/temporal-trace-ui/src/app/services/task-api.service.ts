import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectTask } from '../models/project-task';

@Injectable({
  providedIn: 'root'
})
export class TaskApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = 'http://localhost:5294/api/task';

  getCurrentTasks(): Observable<ProjectTask[]> {
    return this.http.get<ProjectTask[]>(this.apiBase);
  }

  getTasksAtTime(targetTimeIso: string): Observable<ProjectTask[]> {
    const params = new HttpParams().set('targetTime', targetTimeIso);
    return this.http.get<ProjectTask[]>(`${this.apiBase}/at`, { params });
  }
}
