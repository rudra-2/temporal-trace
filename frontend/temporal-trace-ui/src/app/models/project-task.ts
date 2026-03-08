export interface ProjectTask {
  id: number;
  title: string;
  description: string;
  status: string;
  priority: number;
  updatedAt: string;
  completedAt: string | null;
}

export interface TimelineWindow {
  minTime: string;
  maxTime: string;
  yesterdayStartUtc: string;
  yesterdayEndUtc: string;
  usedFallbackWindow: boolean;
}
