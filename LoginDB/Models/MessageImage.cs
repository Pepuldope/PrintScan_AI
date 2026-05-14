namespace DatabazyApiStarter.Models;

public class MessageImage
{
    public int    Id        { get; set; }
    public int    MessageId { get; set; }
    public string FilePath  { get; set; } = string.Empty;
    public string MimeType  { get; set; } = string.Empty;
    public int    SortOrder { get; set; }
}
