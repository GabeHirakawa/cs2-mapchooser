namespace MapChooser.Contracts.Models;

public record VoteResult(
    Map? Winner,
    float WinnerVotes,
    int TotalVoters,
    bool IsExtend,
    string TriggerSource
);
