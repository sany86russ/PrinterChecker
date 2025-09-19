namespace TonerWatch.Core.Models;

/// <summary>
/// Types of printer supplies/consumables
/// </summary>
public enum SupplyKind
{
    Unknown = 0,
    Black = 1,
    Cyan = 2,
    Magenta = 3,
    Yellow = 4,
    Drum = 5,
    Fuser = 6,
    TransferBelt = 7,
    Waste = 8,
    MaintenanceKit = 9,
    PhotoconductorUnit = 10,
    DeveloperUnit = 11,
    TransferUnit = 12,
    CleaningUnit = 13,
    TonerCollection = 14
}