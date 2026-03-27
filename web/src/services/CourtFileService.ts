import type { LocationInfo } from "@/models/LocationInfo";
import api from "./apiClient"
import type { CourtFileList } from "@/models/CourtFileList";

export default function useCourtFileService() {
    
    const getCourtList = async (agencyId: string, roomCode: string, proceedingDate: string): Promise<CourtFileList[]> => {
        const response = await api.get<CourtFileList[]>(`files/getCourtList`, {
            params: { agencyId: agencyId, roomCode: roomCode,  proceedingDate:proceedingDate},
        }
        );
        return response.data;
    }

    return {
        getCourtList
    }
}