export interface DecisionReplayEvent {
  timestamp: string;
  eventType: string;
  title: string;
  details: string;
  outcome: string;
}

export interface DecisionReplay {
  taskId: number;
  generatedAt: string;
  events: DecisionReplayEvent[];
  summary: string;
}

export interface BranchScoreResult {
  branchId: string;
  branchName: string;
  leadTimeImpact: number;
  riskScore: number;
  effortCost: number;
  overallScore: number;
  isRecommended: boolean;
  reasons: string[];
}

export interface BranchScore {
  taskId: number;
  targetTime: string;
  branches: BranchScoreResult[];
}

export interface DailyStandup {
  targetDate: string;
  completedToday: string[];
  inProgressToday: string[];
  blockedToday: string[];
  highlights: string[];
  narrative: string;
}
