using DatabazyApiStarter.Models;
using Npgsql;

namespace DatabazyApiStarter.Repositories;

public class MessageRepository
{
    private readonly Database _db;

    public MessageRepository(Database db) { _db = db; }

    public async Task<List<Message>> GetByChatAsync(int chatId)
    {
        var list = new List<Message>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, chat_id, role, content, photo_count, created_at, status
            FROM messages WHERE chat_id = @cid ORDER BY created_at ASC;
        ", conn);
        cmd.Parameters.AddWithValue("cid", chatId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapMessage(r));
        return list;
    }

    public async Task<Message> AddAsync(int chatId, string role, string content, int photoCount = 0, string status = "complete")
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO messages (chat_id, role, content, photo_count, status)
            VALUES (@cid, @role, @content, @photos, @status)
            RETURNING id, chat_id, role, content, photo_count, created_at, status;
        ", conn);
        cmd.Parameters.AddWithValue("cid",    chatId);
        cmd.Parameters.AddWithValue("role",   role);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("photos", photoCount);
        cmd.Parameters.AddWithValue("status", status);

        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return MapMessage(r);
    }

    public async Task<bool> UpdateContentAndStatusAsync(int id, string content, string status)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE messages SET content = @content, status = @status WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("status",  status);
        cmd.Parameters.AddWithValue("id",      id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<List<Message>> GetAllAsync()
    {
        var list = new List<Message>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, chat_id, role, content, photo_count, created_at, status
            FROM messages ORDER BY created_at ASC;
        ", conn);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapMessage(r));
        return list;
    }

    // ── GET BY ID ──────────────────────────────────────────────────────────
    public async Task<Message?> GetByIdAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, chat_id, role, content, photo_count, created_at, status
            FROM messages WHERE id = @id LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapMessage(r) : null;
    }

    // ── UPDATE ─────────────────────────────────────────────────────────────
    public async Task<bool> UpdateContentAsync(int id, string content)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE messages SET content = @content WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("id",      id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand("DELETE FROM messages WHERE id = @id;", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── COUNT USER MESSAGES (for guest limits) ─────────────────────────────
    /// <summary>Counts how many messages with role='user' belong to the given user across all their chats.</summary>
    public async Task<int> CountUserMessagesByUserAsync(int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM messages m
            INNER JOIN chats c ON c.id = m.chat_id
            WHERE c.user_id = @uid AND m.role = 'user';
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    // ── JOIN 3: Messages with chat title and owner name (3-table JOIN) ─────
    /// <summary>
    /// Returns messages for a chat enriched with the chat title and owner's name.
    /// Demonstrates: messages → chats → users (3-table JOIN).
    /// </summary>
    public async Task<List<(Message Message, string ChatTitle, string OwnerName)>>
        GetByChatWithContextAsync(int chatId)
    {
        var list = new List<(Message, string, string)>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT m.id, m.chat_id, m.role, m.content, m.photo_count, m.created_at, m.status,
                   c.title AS chat_title,
                   u.name  AS owner_name
            FROM messages m
            INNER JOIN chats c ON c.id  = m.chat_id
            INNER JOIN users  u ON u.id = c.user_id
            WHERE m.chat_id = @cid
            ORDER BY m.created_at ASC;
        ", conn);
        cmd.Parameters.AddWithValue("cid", chatId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((MapMessage(r), r.GetString(7), r.GetString(8)));
        return list;
    }

    private static Message MapMessage(NpgsqlDataReader r) => new()
    {
        Id         = r.GetInt32(0),
        ChatId     = r.GetInt32(1),
        Role       = r.GetString(2),
        Content    = r.GetString(3),
        PhotoCount = r.GetInt32(4),
        CreatedAt  = r.GetDateTime(5),
        Status     = r.GetString(6)
    };
}
