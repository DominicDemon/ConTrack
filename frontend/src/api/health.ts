import { http } from './http';

export interface HealthResponse {
  status: string;
  masterDb: 'connected' | 'unreachable';
  time: string;
}

export async function getHealth(): Promise<HealthResponse> {
  const { data } = await http.get<HealthResponse>('/api/health');
  return data;
}
