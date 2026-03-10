import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectTask } from '../models/project-task';
import { TaskComparison } from '../models/task-comparison';

@Injectable({
  providedIn: 'root'
})
export class TaskApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = '/api/task';

  getCurrentTasks(): Observable<ProjectTask[]> {
    return this.http.get<ProjectTask[]>(this.apiBase);
  }

  getTasksAtTime(targetTimeIso: string): Observable<ProjectTask[]> {
    const params = new HttpParams().set('targetTime', targetTimeIso);
    return this.http.get<ProjectTask[]>(`${this.apiBase}/at`, { params });
  }

  getTaskComparison(taskId: number, targetTimeIso: string): Observable<TaskComparison> {
    const params = new HttpParams().set('targetTime', targetTimeIso);
    return this.http.get<TaskComparison>(`${this.apiBase}/${taskId}/compare`, { params });
  }
}
