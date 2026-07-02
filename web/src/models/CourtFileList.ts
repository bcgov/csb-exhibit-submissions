export interface CourtFileList {
  appearanceId: string;
  appearanceDateTime: string;
  appearanceSequenceNumber: string;
  appearanceReasonCode: string;
  courtListType: string;
  fileNumberText: string;
  locationId: string;
  locationNameText: string;
  roomCode: string;
  roomText: string;
  accusedName: string;
  accusedDOB: string;
  appearanceDetails: AppearanceDetails[];
}

export interface AppearanceDetails {
  countPrintSequenceNumber: string;
  statuteDescription: string;
  appearanceReasonCode: string;
}
