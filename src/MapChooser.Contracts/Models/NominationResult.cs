namespace MapChooser.Contracts.Models;

public enum NominationResult
{
    Success,
    MapOnCooldown,
    MapNotInPool,
    AlreadyNominated,
    MaxNominationsReached,
    VoteInProgress,
    IsCurrentMap
}
