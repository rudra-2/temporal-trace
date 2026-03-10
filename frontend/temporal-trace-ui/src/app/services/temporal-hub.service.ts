import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { ProjectTask } from '../models/project-task';

@Injectable({
  providedIn: 'root'
})
export class TemporalHubService {
  private readonly taskUpdatedSubject = new Subject<ProjectTask>();
  private readonly taskDeletedSubject = new Subject<ProjectTask>();
  private connection?: signalR.HubConnection;

  readonly taskUpdated$ = this.taskUpdatedSubject.asObservable();
  readonly taskDeleted$ = this.taskDeletedSubject.asObservable();

  async start(): Promise<void> {
    if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/temporal')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('taskUpdated', (task: ProjectTask) => {
      this.taskUpdatedSubject.next(task);
    });

    this.connection.on('taskDeleted', (task: ProjectTask) => {
      this.taskDeletedSubject.next(task);
    });

    await this.connection.start();
  }

  async stop(): Promise<void> {
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
  }
}
