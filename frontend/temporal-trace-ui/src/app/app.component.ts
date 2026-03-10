import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, switchMap, takeUntil, tap } from 'rxjs/operators';
import { ProjectTask } from './models/project-task';
import { DiffToken, TaskComparison } from './models/task-comparison';
import { BranchTimeline, CreateBranchRequest, TaskBranch, UpdateBranchOverrideRequest } from './models/task-branch';
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

  readonly nowMs = Date.now();
  readonly minMs = this.nowMs - 24 * 60 * 60 * 1000;
  readonly maxMs = this.nowMs;

  selectedMs = this.maxMs;
  tasks: ProjectTask[] = [];
  isLoading = false;
  errorMessage = '';
  mode: 'live' | 'history' = 'live';
  pendingLiveEvents = 0;

  selectedComparison: TaskComparison | null = null;
  selectedDiffTaskId: number | null = null;
  isComparisonLoading = false;
  descriptionHistoricalTokens: DiffToken[] = [];
  descriptionCurrentTokens: DiffToken[] = [];

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

  ngOnInit(): void {
    this.loadCurrentTasks();
    this.startRealtimeSync();

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
    this.hubService.stop().catch(() => undefined);
  }

  onSliderInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.selectedMs = value;
    this.sliderChanges$.next(value);
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
}
