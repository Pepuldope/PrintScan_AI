namespace DatabazyApiStarter.Models;

public class Message
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public string Role { get; set; } = string.Empty;  // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public int PhotoCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "complete";  // "pending" | "complete" | "failed"
}
