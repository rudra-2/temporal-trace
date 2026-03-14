export interface TaskWorkUpdate {
  id: number;
  taskId: number;
  note: string;
  statusAfter: string | null;
  minutesSpent: number | null;
  createdAt: string;
}

export interface CreateTaskWorkUpdateRequest {
  note: string;
  statusAfter: string | null;
  minutesSpent: number | null;
}
