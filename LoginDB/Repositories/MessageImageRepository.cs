using DatabazyApiStarter.Models;
using Npgsql;

namespace DatabazyApiStarter.Repositories;

public class MessageImageRepository
{
    private readonly Database _db;

    public MessageImageRepository(Database db) { _db = db; }

    public async Task AddAsync(int messageId, string filePath, string mimeType, int sortOrder = 0)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO message_images (message_id, file_path, mime_type, sort_order)
            VALUES (@mid, @path, @mime, @sort);
        ", conn);
        cmd.Parameters.AddWithValue("mid",  messageId);
        cmd.Parameters.AddWithValue("path", filePath);
        cmd.Parameters.AddWithValue("mime", mimeType);
        cmd.Parameters.AddWithValue("sort", sortOrder);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Returns a dictionary of messageId → list of (FilePath, MimeType) ordered by sort_order.
    /// Only includes message IDs that actually have stored images.
    /// </summary>
    public async Task<Dictionary<int, List<(string FilePath, string MimeType)>>> GetByMessageIdsAsync(
        IEnumerable<int> messageIds)
    {
        var result = new Dictionary<int, List<(string, string)>>();
        var ids    = messageIds.ToArray();
        if (ids.Length == 0) return result;

        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT message_id, file_path, mime_type
            FROM message_images
            WHERE message_id = ANY(@ids)
            ORDER BY message_id, sort_order;
        ", conn);
        cmd.Parameters.AddWithValue("ids", ids);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var mid = r.GetInt32(0);
            if (!result.ContainsKey(mid)) result[mid] = [];
            result[mid].Add((r.GetString(1), r.GetString(2)));
        }
        return result;
    }
}
