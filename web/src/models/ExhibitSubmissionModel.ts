export interface SubmissionTicketModel {
  appearanceId: string;
  appearanceDateTime: string;
  appearanceSequenceNumber: string;
  appearanceReasonCode: string;
  courtListType: string;
  fileNumberText: string;
  accusedName: string;
  accusedDOB: string;
}

export interface ExhibitSubmissionModel {
  tickets: SubmissionTicketModel[];
  shortDate: string;
  appearanceDateTime: string;
  locationId: string;
  locationNameText: string;
  roomCode: string;
  roomText: string;
  officerNumber: string;
}
