export interface ChildSummary {
  id: number;
  name: string;
}

export interface CreateChildRequest {
  name: string;
  userName: string;
  password: string;
}

export interface CreatedChild {
  id: number;
  name: string;
  userName: string;
  role: 'Child';
}
