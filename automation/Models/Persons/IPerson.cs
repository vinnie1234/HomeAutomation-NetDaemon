namespace Automation.Models.Persons;

public interface IPerson
{
    public bool IsSleeping { get; set; }
    public bool IsDriving { get; set; }
    public bool IsHome { get; set; }
    public string? DirectionOfTravel { get; set; }
    public string? State { get; set; }
}