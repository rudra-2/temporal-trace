import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectTask } from '../models/project-task';
import { TaskComparison } from '../models/task-comparison';
import { TaskBranch, BranchTimeline, CreateBranchRequest, UpdateBranchOverrideRequest } from '../models/task-branch';
import { CreateTaskWorkUpdateRequest, TaskWorkUpdate } from '../models/task-work-update';
import { BranchScore, DailyStandup, DecisionReplay } from '../models/task-intelligence';

@Injectable({
  providedIn: 'root'
})
export class TaskApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = '/api/task';

  getCurrentTasks(): Observable<ProjectTask[]> {
    return this.http.get<ProjectTask[]>(this.apiBase);
  }

  createTask(request: { title: string; description: string; status: string; priority: number }): Observable<ProjectTask> {
    return this.http.post<ProjectTask>(this.apiBase, request);
  }

  updateTask(taskId: number, request: { title: string; description: string; status: string; priority: number }): Observable<ProjectTask> {
    return this.http.put<ProjectTask>(`${this.apiBase}/${taskId}`, request);
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

  getTaskUpdates(taskId: number): Observable<TaskWorkUpdate[]> {
    return this.http.get<TaskWorkUpdate[]>(`${this.apiBase}/${taskId}/updates`);
  }

  addTaskUpdate(taskId: number, request: CreateTaskWorkUpdateRequest): Observable<TaskWorkUpdate> {
    return this.http.post<TaskWorkUpdate>(`${this.apiBase}/${taskId}/updates`, request);
  }

  getDecisionReplay(taskId: number, targetTimeIso?: string): Observable<DecisionReplay> {
    let params = new HttpParams();
    if (targetTimeIso) {
      params = params.set('targetTime', targetTimeIso);
    }

    return this.http.get<DecisionReplay>(`${this.apiBase}/${taskId}/replay`, { params });
  }

  getBranchScores(taskId: number, targetTimeIso?: string): Observable<BranchScore> {
    let params = new HttpParams();
    if (targetTimeIso) {
      params = params.set('targetTime', targetTimeIso);
    }

    return this.http.get<BranchScore>(`${this.apiBase}/${taskId}/branches/score`, { params });
  }

  getDailyStandup(targetDateIso?: string): Observable<DailyStandup> {
    let params = new HttpParams();
    if (targetDateIso) {
      params = params.set('targetDate', targetDateIso);
    }

    return this.http.get<DailyStandup>(`${this.apiBase}/standup/daily`, { params });
  }
}
