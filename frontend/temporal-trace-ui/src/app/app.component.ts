import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, switchMap, takeUntil, tap } from 'rxjs/operators';
import { ProjectTask } from './models/project-task';
import { DiffToken, TaskComparison } from './models/task-comparison';
import { BranchTimeline, CreateBranchRequest, TaskBranch, UpdateBranchOverrideRequest } from './models/task-branch';
import { CreateTaskWorkUpdateRequest, TaskWorkUpdate } from './models/task-work-update';
import { BranchScoreResult, DailyStandup, DecisionReplay } from './models/task-intelligence';
import { TaskApiService } from './services/task-api.service';
import { TemporalHubService } from './services/temporal-hub.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly taskApi = inject(TaskApiService);
  private readonly hubService = inject(TemporalHubService);
  private readonly destroy$ = new Subject<void>();
  private readonly sliderChanges$ = new Subject<number>();
  private doneRefreshTimer: ReturnType<typeof setInterval> | null = null;

  minMs = Date.now() - 24 * 60 * 60 * 1000;
  maxMs = Date.now();

  selectedMs = this.maxMs;
  nowTickMs = Date.now();
  tasks: ProjectTask[] = [];
  isLoading = false;
  errorMessage = '';
  mode: 'live' | 'history' = 'live';
  pendingLiveEvents = 0;

  // Startup cockpit
  searchTerm = '';
  statusFilter = 'all';
  hideDoneAfterMinutes = 5;

  newTaskTitle = '';
  newTaskDescription = '';
  newTaskPriority = 3;
  newTaskStatus = 'Open';
  isCreateTaskLoading = false;

  selectedTaskId: number | null = null;
  selectedTaskUpdates: TaskWorkUpdate[] = [];
  isTaskUpdatesLoading = false;
  newUpdateNote = '';
  newUpdateStatus: string | null = null;
  newUpdateMinutes: number | null = null;
  isUpdateSubmitLoading = false;
  decisionReplay: DecisionReplay | null = null;
  isReplayLoading = false;
  dailyStandup: DailyStandup | null = null;
  isStandupLoading = false;
  standupDate = new Date().toISOString().slice(0, 10);

  // Ghost diff state
  selectedComparison: TaskComparison | null = null;
  selectedDiffTaskId: number | null = null;
  isComparisonLoading = false;
  descriptionHistoricalTokens: DiffToken[] = [];
  descriptionCurrentTokens: DiffToken[] = [];

  // Branching state
  branches: TaskBranch[] = [];
  branchTaskId: number | null = null;
  selectedBranchId: string | null = null;
  isBranchLoading = false;
  showCreateBranchDialog = false;
  newBranchName = '';
  selectedBranchTimeline: BranchTimeline | null = null;
  isBranchTimelineLoading = false;
  branchDraftTitle = '';
  branchDraftDescription = '';
  branchDraftStatus = '';
  branchDraftPriority: number | null = null;
  branchScores: BranchScoreResult[] = [];
  isBranchScoring = false;

  ngOnInit(): void {
    this.initializeTimelineWindow();
    this.loadCurrentTasks();
    this.startRealtimeSync();

    this.doneRefreshTimer = setInterval(() => {
      const currentMs = Date.now();
      this.nowTickMs = currentMs;
      this.maxMs = currentMs;

      if (this.mode === 'live') {
        this.selectedMs = this.maxMs;
      }
    }, 30000);

    this.sliderChanges$
      .pipe(
        debounceTime(75),
        distinctUntilChanged(),
        tap(() => {
          this.isLoading = true;
          this.errorMessage = '';
        }),
        switchMap((ms) => {
          this.mode = ms >= this.maxMs ? 'live' : 'history';
          if (this.mode === 'live') {
            this.pendingLiveEvents = 0;
            this.clearComparison();
            this.clearBranchContext();
          }

          return this.mode === 'live'
            ? this.taskApi.getCurrentTasks()
            : this.taskApi.getTasksAtTime(new Date(ms).toISOString());
        }),
        catchError(() => {
          this.errorMessage = 'Unable to load task timeline right now.';
          return of([] as ProjectTask[]);
        }),
        finalize(() => {
          this.isLoading = false;
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((tasks) => {
        this.tasks = tasks;
        this.isLoading = false;
        if (this.mode === 'history' && this.selectedBranchId) {
          this.loadSelectedBranchTimeline();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.doneRefreshTimer) {
      clearInterval(this.doneRefreshTimer);
    }
    this.hubService.stop().catch(() => undefined);
  }

  get visibleTasks(): ProjectTask[] {
    const search = this.searchTerm.trim().toLowerCase();
    return this.tasks
      .filter((task) => {
        if (this.statusFilter !== 'all' && task.status.toLowerCase() !== this.statusFilter.toLowerCase()) {
          return false;
        }

        if (search.length > 0) {
          const target = `${task.title} ${task.description} ${task.status}`.toLowerCase();
          if (!target.includes(search)) {
            return false;
          }
        }

        if (this.mode === 'live' && this.shouldHideDone(task)) {
          return false;
        }

        return true;
      })
      .sort((a, b) => a.id - b.id);
  }

  get totalCount(): number {
    return this.visibleTasks.length;
  }

  get inProgressCount(): number {
    return this.visibleTasks.filter((t) => t.status.toLowerCase() === 'inprogress').length;
  }

  get blockedCount(): number {
    return this.visibleTasks.filter((t) => t.status.toLowerCase() === 'blocked').length;
  }

  get doneCount(): number {
    return this.visibleTasks.filter((t) => this.isDoneStatus(t.status)).length;
  }

  onSliderInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.selectedMs = value;
    this.sliderChanges$.next(value);
  }

  createTask(): void {
    if (!this.newTaskTitle.trim()) {
      return;
    }

    this.isCreateTaskLoading = true;
    this.taskApi
      .createTask({
        title: this.newTaskTitle.trim(),
        description: this.newTaskDescription.trim(),
        status: this.newTaskStatus,
        priority: this.newTaskPriority
      })
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to create task right now.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((created) => {
        this.isCreateTaskLoading = false;
        if (!created) {
          return;
        }

        this.newTaskTitle = '';
        this.newTaskDescription = '';
        this.newTaskPriority = 3;
        this.newTaskStatus = 'Open';

        if (this.mode === 'live') {
          this.tasks = [...this.tasks, created].sort((a, b) => a.id - b.id);
        }
      });
  }

  quickSetStatus(task: ProjectTask, status: string): void {
    this.taskApi
      .updateTask(task.id, {
        title: task.title,
        description: task.description,
        status,
        priority: task.priority
      })
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to update task status.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((updated) => {
        if (!updated) {
          return;
        }

        this.tasks = this.tasks.map((t) => (t.id === updated.id ? updated : t));
        if (this.selectedTaskId === updated.id) {
          this.loadTaskUpdates(updated.id);
        }
      });
  }

  selectTask(task: ProjectTask): void {
    this.selectedTaskId = task.id;
    this.decisionReplay = null;
    this.loadTaskUpdates(task.id);
  }

  addWorkUpdate(): void {
    if (!this.selectedTaskId || !this.newUpdateNote.trim()) {
      return;
    }

    const request: CreateTaskWorkUpdateRequest = {
      note: this.newUpdateNote.trim(),
      statusAfter: this.newUpdateStatus,
      minutesSpent: this.newUpdateMinutes
    };

    this.isUpdateSubmitLoading = true;
    this.taskApi
      .addTaskUpdate(this.selectedTaskId, request)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to add work update.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((result) => {
        this.isUpdateSubmitLoading = false;
        if (!result) {
          return;
        }

        this.newUpdateNote = '';
        this.newUpdateMinutes = null;
        this.newUpdateStatus = null;
        this.loadTaskUpdates(this.selectedTaskId!);
        if (this.mode === 'live') {
          this.loadCurrentTasks();
        }
      });
  }

  loadDecisionReplay(): void {
    if (!this.selectedTaskId) {
      return;
    }

    this.isReplayLoading = true;
    this.taskApi
      .getDecisionReplay(this.selectedTaskId)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to build decision replay right now.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((replay) => {
        this.isReplayLoading = false;
        this.decisionReplay = replay;
      });
  }

  generateDailyStandup(): void {
    this.isStandupLoading = true;
    this.taskApi
      .getDailyStandup(this.standupDate)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to generate standup summary.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((standup) => {
        this.isStandupLoading = false;
        this.dailyStandup = standup;
      });
  }

  scoreBranches(): void {
    if (!this.selectedDiffTaskId) {
      return;
    }

    this.isBranchScoring = true;
    this.taskApi
      .getBranchScores(this.selectedDiffTaskId, new Date(this.selectedMs).toISOString())
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to score branches at this timestamp.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((scores) => {
        this.isBranchScoring = false;
        this.branchScores = scores?.branches ?? [];
      });
  }

  onBranchSelectionChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selectedBranchId = value || null;
    this.clearComparison();
    this.selectedBranchTimeline = null;
    if (this.selectedBranchId) {
      this.loadSelectedBranchTimeline();
    }
  }

  onTaskHover(task: ProjectTask): void {
    if (this.mode !== 'history') {
      return;
    }

    this.selectedDiffTaskId = task.id;
    this.loadBranches(task.id);

    this.isComparisonLoading = true;
    this.taskApi
      .getTaskComparison(task.id, new Date(this.selectedMs).toISOString())
      .pipe(
        catchError(() => {
          this.selectedComparison = null;
          this.descriptionHistoricalTokens = [];
          this.descriptionCurrentTokens = [];
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((comparison) => {
        this.isComparisonLoading = false;
        if (!comparison) {
          return;
        }

        this.selectedComparison = comparison;
        this.descriptionHistoricalTokens = this.diffDescriptionTokens(
          comparison.historical.description,
          comparison.current.description,
          'historical'
        );
        this.descriptionCurrentTokens = this.diffDescriptionTokens(
          comparison.historical.description,
          comparison.current.description,
          'current'
        );
        if (this.selectedBranchId) {
          this.loadSelectedBranchTimeline();
        }
      });
  }

  openCreateBranchDialog(): void {
    if (this.mode !== 'history' || this.selectedDiffTaskId === null) {
      return;
    }

    this.showCreateBranchDialog = true;
    this.newBranchName = '';
  }

  createBranch(): void {
    if (!this.newBranchName.trim() || this.selectedDiffTaskId === null) {
      return;
    }

    const request: CreateBranchRequest = {
      targetTime: new Date(this.selectedMs).toISOString(),
      branchName: this.newBranchName.trim()
    };

    this.isBranchLoading = true;
    this.taskApi
      .createBranch(this.selectedDiffTaskId, request)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Failed to create branch.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((branch) => {
        this.isBranchLoading = false;
        this.showCreateBranchDialog = false;
        if (!branch) {
          return;
        }

        this.branches = [...this.branches, branch].sort((a, b) =>
          a.createdAt.localeCompare(b.createdAt)
        );
        this.selectedBranchId = branch.branchId;
        this.loadSelectedBranchTimeline();
      });
  }

  saveBranchOverrides(): void {
    if (!this.selectedBranchId) {
      return;
    }

    const request: UpdateBranchOverrideRequest = {
      overrideTitle: this.branchDraftTitle.trim() ? this.branchDraftTitle.trim() : null,
      overrideDescription: this.branchDraftDescription.trim() ? this.branchDraftDescription.trim() : null,
      overrideStatus: this.branchDraftStatus.trim() ? this.branchDraftStatus.trim() : null,
      overridePriority: this.branchDraftPriority
    };

    this.isBranchLoading = true;
    this.taskApi
      .updateBranchOverride(this.selectedBranchId, request)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Failed to save branch overrides.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((updated) => {
        this.isBranchLoading = false;
        if (!updated) {
          return;
        }

        this.branches = this.branches.map((b) => (b.branchId === updated.branchId ? updated : b));
        this.loadSelectedBranchTimeline();
      });
  }

  resetBranchOverrides(): void {
    this.branchDraftTitle = '';
    this.branchDraftDescription = '';
    this.branchDraftStatus = '';
    this.branchDraftPriority = null;
    this.saveBranchOverrides();
  }

  deleteSelectedBranch(): void {
    if (!this.selectedBranchId) {
      return;
    }

    const branchId = this.selectedBranchId;
    this.taskApi
      .deleteBranch(branchId)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Failed to delete branch.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.branches = this.branches.filter((b) => b.branchId !== branchId);
        this.selectedBranchId = null;
      });
  }

  asTimelineLabel(ms: number): string {
    return new Date(ms).toLocaleString();
  }

  asIsoLabel(isoDate: string): string {
    return new Date(isoDate).toLocaleString();
  }

  isChanged(field: string): boolean {
    return this.selectedComparison?.changedFields.includes(field) ?? false;
  }

  selectedBranchName(): string {
    if (!this.selectedBranchId) {
      return '';
    }

    return this.branches.find((b) => b.branchId === this.selectedBranchId)?.branchName ?? '';
  }

  selectedBranchCreatedAt(): string {
    if (!this.selectedBranchId) {
      return '';
    }

    const createdFrom = this.branches.find((b) => b.branchId === this.selectedBranchId)?.createdFromTime;
    return createdFrom ? this.asTimelineLabel(new Date(createdFrom).getTime()) : '';
  }

  branchCreatedAtLabel(createdFromTime: string): string {
    return this.asTimelineLabel(new Date(createdFromTime).getTime());
  }

  branchFieldChanged(field: string): boolean {
    return this.selectedBranchTimeline?.changedFields.includes(field) ?? false;
  }

  progressForTask(task: ProjectTask): number {
    const status = task.status.toLowerCase();
    if (status === 'open' || status === 'todo') {
      return 15;
    }
    if (status === 'inprogress') {
      return 55;
    }
    if (status === 'blocked') {
      return 35;
    }
    if (this.isDoneStatus(task.status)) {
      return 100;
    }
    return 45;
  }

  statusClass(task: ProjectTask): string {
    return `status-${task.status.toLowerCase()}`;
  }

  private loadCurrentTasks(): void {
    this.isLoading = true;
    this.taskApi
      .getCurrentTasks()
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to load current tasks.';
          return of([] as ProjectTask[]);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((tasks) => {
        this.tasks = tasks;
        this.isLoading = false;
      });
  }

  private initializeTimelineWindow(): void {
    this.taskApi
      .getTimelineWindow()
      .pipe(
        catchError(() => of(null)),
        takeUntil(this.destroy$)
      )
      .subscribe((window) => {
        if (!window) {
          return;
        }

        const minMs = new Date(window.minTime).getTime();
        const maxMs = new Date(window.maxTime).getTime();
        if (Number.isNaN(minMs) || Number.isNaN(maxMs) || minMs >= maxMs) {
          return;
        }

        this.minMs = minMs;
        this.maxMs = maxMs;
        if (this.selectedMs < this.minMs || this.selectedMs > this.maxMs || this.mode === 'live') {
          this.selectedMs = this.maxMs;
        }
      });
  }

  private loadTaskUpdates(taskId: number): void {
    this.isTaskUpdatesLoading = true;
    this.taskApi
      .getTaskUpdates(taskId)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to load task updates.';
          return of([] as TaskWorkUpdate[]);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((updates) => {
        this.selectedTaskUpdates = updates;
        this.isTaskUpdatesLoading = false;
      });
  }

  private loadBranches(taskId: number): void {
    if (this.branchTaskId === taskId && this.branches.length > 0) {
      return;
    }

    this.branchTaskId = taskId;
    this.isBranchLoading = true;
    this.taskApi
      .getBranches(taskId)
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to load branches.';
          return of([] as TaskBranch[]);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((branches) => {
        this.branches = branches;
        if (this.selectedBranchId && !branches.some((b) => b.branchId === this.selectedBranchId)) {
          this.selectedBranchId = null;
          this.selectedBranchTimeline = null;
        }
        this.isBranchLoading = false;
      });
  }

  private loadSelectedBranchTimeline(): void {
    if (!this.selectedBranchId) {
      return;
    }

    this.isBranchTimelineLoading = true;
    this.taskApi
      .getBranchTimeline(this.selectedBranchId, new Date(this.selectedMs).toISOString())
      .pipe(
        catchError(() => {
          this.errorMessage = 'Unable to load selected branch timeline.';
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((timeline) => {
        this.isBranchTimelineLoading = false;
        if (!timeline) {
          this.selectedBranchTimeline = null;
          return;
        }

        this.selectedBranchTimeline = timeline;
        this.branchDraftTitle = timeline.branchTaskSnapshot?.title ?? '';
        this.branchDraftDescription = timeline.branchTaskSnapshot?.description ?? '';
        this.branchDraftStatus = timeline.branchTaskSnapshot?.status ?? '';
        this.branchDraftPriority = timeline.branchTaskSnapshot?.priority ?? null;
      });
  }

  private startRealtimeSync(): void {
    this.hubService.start().catch(() => {
      this.errorMessage = 'Live sync connection failed. Time travel queries are still available.';
    });

    this.hubService.taskUpdated$.pipe(takeUntil(this.destroy$)).subscribe((task) => {
      if (this.mode !== 'live') {
        this.pendingLiveEvents += 1;
        return;
      }

      const existingIndex = this.tasks.findIndex((t) => t.id === task.id);
      if (existingIndex >= 0) {
        this.tasks[existingIndex] = task;
        this.tasks = [...this.tasks];
        return;
      }

      this.tasks = [...this.tasks, task].sort((a, b) => a.id - b.id);
    });

    this.hubService.taskDeleted$.pipe(takeUntil(this.destroy$)).subscribe((task) => {
      if (this.mode !== 'live') {
        this.pendingLiveEvents += 1;
        return;
      }

      this.tasks = this.tasks.filter((t) => t.id !== task.id);
    });
  }

  private clearComparison(): void {
    this.selectedComparison = null;
    this.descriptionHistoricalTokens = [];
    this.descriptionCurrentTokens = [];
  }

  private clearBranchContext(): void {
    this.branches = [];
    this.branchTaskId = null;
    this.selectedBranchId = null;
    this.selectedBranchTimeline = null;
    this.branchScores = [];
    this.showCreateBranchDialog = false;
  }

  private diffDescriptionTokens(historical: string, current: string, side: 'historical' | 'current'): DiffToken[] {
    const left = this.tokenize(historical);
    const right = this.tokenize(current);
    const referenceSet = new Set(side === 'historical' ? right : left);
    const source = side === 'historical' ? left : right;

    return source.map((token) => ({
      text: token,
      kind: referenceSet.has(token) ? 'same' : 'changed'
    }));
  }

  private tokenize(value: string): string[] {
    return value
      .split(/\s+/)
      .map((t) => t.trim())
      .filter((t) => t.length > 0);
  }

  private shouldHideDone(task: ProjectTask): boolean {
    if (!this.isDoneStatus(task.status)) {
      return false;
    }

    const referenceTime = task.completedAt ?? task.updatedAt;
    if (!referenceTime) {
      return false;
    }

    const completedMs = new Date(referenceTime).getTime();
    if (Number.isNaN(completedMs)) {
      return false;
    }

    const ageMinutes = (this.nowTickMs - completedMs) / 60000;
    if (ageMinutes < 0) {
      return false;
    }

    return ageMinutes >= this.hideDoneAfterMinutes;
  }

  private isDoneStatus(status: string): boolean {
    const normalized = status.toLowerCase();
    return normalized === 'done' || normalized === 'completed' || normalized === 'closed';
  }
}
