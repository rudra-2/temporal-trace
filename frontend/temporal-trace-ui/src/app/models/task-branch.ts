export interface TaskBranch {
  branchId: string;
  taskId: number;
  branchName: string;
  createdFromTime: string;
  createdAt: string;
  isMainTimeline: boolean;
  hasOverrides?: boolean;
}

export interface BranchTimeline {
  branchId: string;
  branchName: string;
  isMainTimeline: boolean;
  mainTaskSnapshot?: {
    id: number;
    title: string;
    description: string;
    status: string;
    priority: number;
  };
  branchTaskSnapshot?: {
    id: number;
    title: string;
    description: string;
    status: string;
    priority: number;
  };
  changedFields: string[];
}

export interface CreateBranchRequest {
  targetTime: string;
  branchName: string;
}

export interface UpdateBranchOverrideRequest {
  overrideTitle: string | null;
  overrideDescription: string | null;
  overrideStatus: string | null;
  overridePriority: number | null;
}
