namespace DatabazyApiStarter.Models;

public class UserProfile
{
    public int UserId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string FilamentType { get; set; } = string.Empty;
    public string Slicer { get; set; } = string.Empty;
}
