using DatabazyApiStarter.Models;
using Npgsql;

namespace DatabazyApiStarter.Repositories;

public class ChatRepository
{
    private const int MaxChats = 5;
    private readonly Database _db;

    public ChatRepository(Database db) { _db = db; }

    public async Task<List<Chat>> GetByUserAsync(int userId)
    {
        var list = new List<Chat>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, user_id, title, is_pinned, photo_count, created_at, updated_at
            FROM chats WHERE user_id = @uid
            ORDER BY is_pinned DESC, updated_at DESC;
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapChat(r));
        return list;
    }

    public async Task<Chat?> GetByIdAsync(int chatId, int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, user_id, title, is_pinned, photo_count, created_at, updated_at
            FROM chats WHERE id = @id AND user_id = @uid LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("id",  chatId);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapChat(r) : null;
    }

    /// <summary>
    /// Creates a new chat. If the user already has MaxChats non-pinned chats,
    /// deletes the oldest non-pinned one first.
    /// </summary>
    public async Task<Chat> CreateAsync(int userId)
    {
        await using var conn = _db.CreateConnection();

        // Count non-pinned chats
        await using (var countCmd = new NpgsqlCommand(@"
            SELECT COUNT(*) FROM chats WHERE user_id = @uid AND is_pinned = FALSE;
        ", conn))
        {
            countCmd.Parameters.AddWithValue("uid", userId);
            var count = (long)(await countCmd.ExecuteScalarAsync())!;

            if (count >= MaxChats)
            {
                // Delete oldest non-pinned
                await using var delCmd = new NpgsqlCommand(@"
                    DELETE FROM chats
                    WHERE id = (
                        SELECT id FROM chats
                        WHERE user_id = @uid AND is_pinned = FALSE
                        ORDER BY updated_at ASC
                        LIMIT 1
                    );
                ", conn);
                delCmd.Parameters.AddWithValue("uid", userId);
                await delCmd.ExecuteNonQueryAsync();
            }
        }

        await using var ins = new NpgsqlCommand(@"
            INSERT INTO chats (user_id, title)
            VALUES (@uid, 'New Chat')
            RETURNING id, user_id, title, is_pinned, photo_count, created_at, updated_at;
        ", conn);
        ins.Parameters.AddWithValue("uid", userId);

        await using var r = await ins.ExecuteReaderAsync();
        await r.ReadAsync();
        return MapChat(r);
    }

    public async Task<bool> DeleteAsync(int chatId, int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            DELETE FROM chats WHERE id = @id AND user_id = @uid;
        ", conn);
        cmd.Parameters.AddWithValue("id",  chatId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// Pins a chat. If another chat is already pinned, it gets unpinned first.
    /// Passing isPinned=false just unpins the current chat.
    /// </summary>
    public async Task<bool> SetPinnedAsync(int chatId, int userId, bool isPinned)
    {
        await using var conn = _db.CreateConnection();

        if (isPinned)
        {
            // Unpin any currently pinned chat for this user
            await using var unpinCmd = new NpgsqlCommand(@"
                UPDATE chats SET is_pinned = FALSE
                WHERE user_id = @uid AND is_pinned = TRUE;
            ", conn);
            unpinCmd.Parameters.AddWithValue("uid", userId);
            await unpinCmd.ExecuteNonQueryAsync();
        }

        await using var cmd = new NpgsqlCommand(@"
            UPDATE chats
            SET is_pinned = @pinned, updated_at = NOW()
            WHERE id = @id AND user_id = @uid;
        ", conn);
        cmd.Parameters.AddWithValue("pinned", isPinned);
        cmd.Parameters.AddWithValue("id",     chatId);
        cmd.Parameters.AddWithValue("uid",    userId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task UpdateTitleAsync(int chatId, string title)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE chats SET title = @title, updated_at = NOW() WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("title", title[..Math.Min(title.Length, 200)]);
        cmd.Parameters.AddWithValue("id",    chatId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task IncrementPhotoCountAsync(int chatId, int count)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE chats SET photo_count = photo_count + @count, updated_at = NOW()
            WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("count", count);
        cmd.Parameters.AddWithValue("id",    chatId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task TouchAsync(int chatId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE chats SET updated_at = NOW() WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("id", chatId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<List<Chat>> GetAllAsync()
    {
        var list = new List<Chat>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, user_id, title, is_pinned, photo_count, created_at, updated_at
            FROM chats ORDER BY updated_at DESC;
        ", conn);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapChat(r));
        return list;
    }

    // ── JOIN 1: Chat with owner info (chats JOIN users) ────────────────────
    /// <summary>Returns chats for a user with owner name/email joined from users (1:N).</summary>
    public async Task<List<(Chat Chat, string OwnerName, string OwnerEmail)>> GetByUserWithOwnerAsync(int userId)
    {
        var list = new List<(Chat, string, string)>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT c.id, c.user_id, c.title, c.is_pinned, c.photo_count,
                   c.created_at, c.updated_at,
                   u.name AS owner_name, u.email AS owner_email
            FROM chats c
            INNER JOIN users u ON u.id = c.user_id
            WHERE c.user_id = @uid
            ORDER BY c.is_pinned DESC, c.updated_at DESC;
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((MapChat(r), r.GetString(7), r.GetString(8)));
        return list;
    }

    // ── JOIN 2: Chat with message count (chats JOIN messages) ──────────────
    /// <summary>Returns each chat for a user with total message count.</summary>
    public async Task<List<(Chat Chat, int MessageCount)>> GetByUserWithMessageCountAsync(int userId)
    {
        var list = new List<(Chat, int)>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT c.id, c.user_id, c.title, c.is_pinned, c.photo_count,
                   c.created_at, c.updated_at,
                   COUNT(m.id) AS message_count
            FROM chats c
            LEFT JOIN messages m ON m.chat_id = c.id
            WHERE c.user_id = @uid
            GROUP BY c.id
            ORDER BY c.is_pinned DESC, c.updated_at DESC;
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((MapChat(r), (int)r.GetInt64(7)));
        return list;
    }

    private static Chat MapChat(NpgsqlDataReader r) => new()
    {
        Id         = r.GetInt32(0),
        UserId     = r.GetInt32(1),
        Title      = r.GetString(2),
        IsPinned   = r.GetBoolean(3),
        PhotoCount = r.GetInt32(4),
        CreatedAt  = r.GetDateTime(5),
        UpdatedAt  = r.GetDateTime(6)
    };
}
