namespace DatabazyApiStarter.Models;

public class Chat
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "New Chat";
    public bool IsPinned { get; set; }
    public int PhotoCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
