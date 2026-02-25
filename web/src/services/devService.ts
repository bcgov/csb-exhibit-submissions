import httpClient from './httpClient'

export interface HealthResponse {
  status: string
  timestamp: string
  version?: string
}

export async function getHealth(): Promise<boolean> {
  const response = await httpClient.get<boolean>('/dev/health');
  console.log(response);
  return response.data;
}