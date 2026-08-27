export interface Reward {
  id: number;
  name: string;
  description: string | null;
  pointsCost: number;
  stockQuantity: number;
  createdAt: string;
}

export interface RewardRequest {
  name: string;
  description: string | null;
  pointsCost: number;
  stockQuantity?: number;
}
