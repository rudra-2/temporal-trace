import { ProjectTask } from './project-task';

export interface TaskComparison {
  historical: ProjectTask;
  current: ProjectTask;
  changedFields: string[];
}

export interface DiffToken {
  text: string;
  kind: 'same' | 'changed';
}
