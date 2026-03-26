
export interface LocationInfo {
  name: string;
  shortName: string;
  code: string;
  locationId: string;
  active?: boolean;
  agencyIdentifierCd: string;
  courtRooms: CourtRoomsInfo[];
  infoLink: string;
  regionCd: string;
}


export interface CourtRoomsInfo {
  room: string;
  locationId: string;
  type: string;
}