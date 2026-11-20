namespace PandaPet.Models;

public sealed class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Time { get; set; } = "20:00";
    public bool Enabled { get; set; } = true;
    public List<DayOfWeek> AllowedDays { get; set; } = [];
    public string LastCompletedDate { get; set; } = string.Empty;
}
