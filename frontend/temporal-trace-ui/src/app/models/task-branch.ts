export interface TaskBranch {
  branchId: string;
  taskId: number;
  branchName: string;
  createdFromTime: string;
  createdAt: string;
  isMainTimeline: boolean;
}

export interface BranchTimeline {
  branchId: string;
  branchName: string;
  isMainTimeline: boolean;
  taskSnapshot?: any;
}

export interface CreateBranchRequest {
  targetTime: string;
  branchName: string;
}
