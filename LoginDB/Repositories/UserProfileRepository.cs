using DatabazyApiStarter.Models;
using Npgsql;

namespace DatabazyApiStarter.Repositories;

public class UserProfileRepository
{
    private readonly Database _db;

    public UserProfileRepository(Database db) { _db = db; }

    public async Task<UserProfile> GetOrCreateAsync(int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO user_profiles (user_id, printer_name, filament_type, slicer)
            VALUES (@uid, '', '', '')
            ON CONFLICT (user_id) DO NOTHING;

            SELECT user_id, printer_name, filament_type, slicer
            FROM user_profiles WHERE user_id = @uid;
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new UserProfile
        {
            UserId       = r.GetInt32(0),
            PrinterName  = r.GetString(1),
            FilamentType = r.GetString(2),
            Slicer       = r.GetString(3)
        };
    }

    // ── CREATE / UPDATE (Upsert) ───────────────────────────────────────────
    public async Task<UserProfile> UpsertAsync(int userId, string printerName, string filamentType, string slicer)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO user_profiles (user_id, printer_name, filament_type, slicer)
            VALUES (@uid, @printer, @filament, @slicer)
            ON CONFLICT (user_id) DO UPDATE
                SET printer_name  = EXCLUDED.printer_name,
                    filament_type = EXCLUDED.filament_type,
                    slicer        = EXCLUDED.slicer
            RETURNING user_id, printer_name, filament_type, slicer;
        ", conn);
        cmd.Parameters.AddWithValue("uid",      userId);
        cmd.Parameters.AddWithValue("printer",  printerName);
        cmd.Parameters.AddWithValue("filament", filamentType);
        cmd.Parameters.AddWithValue("slicer",   slicer);

        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new UserProfile
        {
            UserId       = r.GetInt32(0),
            PrinterName  = r.GetString(1),
            FilamentType = r.GetString(2),
            Slicer       = r.GetString(3)
        };
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<List<UserProfile>> GetAllAsync()
    {
        var list = new List<UserProfile>();
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT user_id, printer_name, filament_type, slicer
            FROM user_profiles ORDER BY user_id;
        ", conn);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new UserProfile
            {
                UserId       = r.GetInt32(0),
                PrinterName  = r.GetString(1),
                FilamentType = r.GetString(2),
                Slicer       = r.GetString(3)
            });
        }
        return list;
    }

    // ── GET BY ID ──────────────────────────────────────────────────────────
    public async Task<UserProfile?> GetByIdAsync(int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT user_id, printer_name, filament_type, slicer
            FROM user_profiles WHERE user_id = @uid LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("uid", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new UserProfile
        {
            UserId       = r.GetInt32(0),
            PrinterName  = r.GetString(1),
            FilamentType = r.GetString(2),
            Slicer       = r.GetString(3)
        };
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int userId)
    {
        await using var conn = _db.CreateConnection();
        await using var cmd  = new NpgsqlCommand(
            "DELETE FROM user_profiles WHERE user_id = @uid;", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
