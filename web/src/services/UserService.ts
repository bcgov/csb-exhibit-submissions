import type { UserProfileModel } from '@/models/UserProfileModel';
import axios from 'axios';
import api from './apiClient';

export default function useUserService() {
  /**
   * The signed-in user's profile. Resolves to null when the API has no local row for them —
   * an ordinary state (the login-time upsert is best-effort), not a failure worth throwing on.
   */
  const getProfile = async (): Promise<UserProfileModel | null> => {
    try {
      const response = await api.get<UserProfileModel>('/users/me');
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  };

  /** Stores the officer number on the user's row. Throws on a validation rejection (400). */
  const saveOfficerNumber = async (officerNumber: string): Promise<UserProfileModel> => {
    const response = await api.put<UserProfileModel>('/users/me/officer-number', {
      officerNumber,
    });
    return response.data;
  };

  return {
    getProfile,
    saveOfficerNumber,
  };
}
