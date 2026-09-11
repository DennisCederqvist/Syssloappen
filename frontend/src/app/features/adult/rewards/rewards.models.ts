export interface Reward {
  id: number;
  name: string;
  description: string | null;
  pointsCost: number;
  stockQuantity: number;
  imageUrl: string | null;
  createdAt: string;
}

export interface RewardRequest {
  name: string;
  description: string | null;
  pointsCost: number;
  stockQuantity?: number;
}
