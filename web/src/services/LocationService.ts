import type { LocationInfo } from "@/models/LocationInfo";
import api from "./apiClient"

export default function useLocationService() {
    
    const getLocations = async (includeChildRecords = true): Promise<LocationInfo[]> => {
        const response = await api.get<LocationInfo[]>(
            `location/getLocations?includeChildRecords=${includeChildRecords}`
        );
        return response.data;
    }

    return {
        getLocations
    }
}