namespace MapChooser.Contracts.Models;

public record VoteConfig(
    int MapsToShow = 6,
    int VoteDurationSeconds = 30,
    bool AllowExtend = false,
    int ExtendMinutes = 0,
    bool ChangeMapImmediately = false,
    string TriggerSource = "endofmap"
);
