import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, switchMap, takeUntil, tap } from 'rxjs/operators';
import { ProjectTask } from './models/project-task';
import { TaskApiService } from './services/task-api.service';
import { TemporalHubService } from './services/temporal-hub.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
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
          }

          if (this.mode === 'live') {
            return this.taskApi.getCurrentTasks();
          }

          return this.taskApi.getTasksAtTime(new Date(ms).toISOString());
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

  asTimelineLabel(ms: number): string {
    return new Date(ms).toLocaleString();
  }

  private loadCurrentTasks(): void {
    this.isLoading = true;
    this.taskApi.getCurrentTasks()
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

  private startRealtimeSync(): void {
    this.hubService.start().catch(() => {
      this.errorMessage = 'Live sync connection failed. Time travel queries are still available.';
    });

    this.hubService.taskUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((task) => {
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

    this.hubService.taskDeleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe((task) => {
        if (this.mode !== 'live') {
          this.pendingLiveEvents += 1;
          return;
        }

        this.tasks = this.tasks.filter((t) => t.id !== task.id);
      });
  }
}
