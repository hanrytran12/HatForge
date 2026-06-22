namespace HatForge.Domain.Enums;

public enum BatchStatus
{
    Created = 0,
    Assigned = 1,
    InProduction = 2,
    UnderQCReview = 3,
    ReadyForTransfer = 4,
    PendingLeadReview = 6,   
    PendingGateQC = 7,       
    Completed = 5
}
