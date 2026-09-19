namespace MemoryLab.Models;

public enum ScanMode
{
    ExactValue,
    UnknownInitialValue,
    ChangedValue,
    UnchangedValue,
    IncreasedValue,
    DecreasedValue,
    GreaterThan,
    LessThan,
    Between,
    IncreasedBy,
    DecreasedBy
}
