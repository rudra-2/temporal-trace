import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectTask } from '../models/project-task';
import { TaskComparison } from '../models/task-comparison';
import { TaskBranch, BranchTimeline, CreateBranchRequest, UpdateBranchOverrideRequest } from '../models/task-branch';

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

  // Branch endpoints
  createBranch(taskId: number, request: CreateBranchRequest): Observable<TaskBranch> {
    return this.http.post<TaskBranch>(`${this.apiBase}/${taskId}/branch`, request);
  }

  getBranches(taskId: number): Observable<TaskBranch[]> {
    return this.http.get<TaskBranch[]>(`${this.apiBase}/${taskId}/branches`);
  }

  getBranchTimeline(branchId: string, targetTimeIso: string): Observable<BranchTimeline> {
    const params = new HttpParams().set('targetTime', targetTimeIso);
    return this.http.get<BranchTimeline>(`${this.apiBase}/branch/${branchId}/timeline`, { params });
  }

  updateBranchOverride(branchId: string, request: UpdateBranchOverrideRequest): Observable<TaskBranch> {
    return this.http.put<TaskBranch>(`${this.apiBase}/branch/${branchId}/override`, request);
  }

  deleteBranch(branchId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiBase}/branch/${branchId}`);
  }
}
