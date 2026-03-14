export interface ProjectTask {
  id: number;
  title: string;
  description: string;
  status: string;
  priority: number;
  updatedAt: string;
  completedAt: string | null;
}
