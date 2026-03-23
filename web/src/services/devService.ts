import api from './apiClient'

export interface HealthResponse {
  status: string
  timestamp: string
  version?: string
}

export async function getHealth(): Promise<boolean> {
  const response = await api.get<boolean>('/dev/health');
  console.log(response);
  return response.data;
}