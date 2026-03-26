import type { LocationInfo } from "@/models/LocationInfo";
import api from "./apiClient"

export default function useLocationService() {
    
    const getLocations = async (includeChildRecords = false): Promise<LocationInfo[]> => {
        const response = await api.get<LocationInfo[]>(
            `api/location?includeChildRecords=${includeChildRecords}`
        );
        return response.data;
    }

    return {
        getLocations
    }
}