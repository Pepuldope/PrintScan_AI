using DatabazyApiStarter.Models;
using Npgsql;

namespace DatabazyApiStarter.Repositories;

public class UserRepository
{
    private readonly Database _database;

    public UserRepository(Database database)
    {
        _database = database;
    }

    // ── CREATE ─────────────────────────────────────────────────────────────
    public async Task<User> CreateAsync(string name, string email, string passwordHash, string role = "user")
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO users (name, email, password, is_active, role)
            VALUES (@name, @email, @password, TRUE, @role)
            RETURNING id, name, email, password, is_active, role;
        ", conn);
        cmd.Parameters.AddWithValue("name",     name);
        cmd.Parameters.AddWithValue("email",    email);
        cmd.Parameters.AddWithValue("password", passwordHash);
        cmd.Parameters.AddWithValue("role",     role);

        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return MapUser(r);
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<List<User>> GetAllAsync()
    {
        var list = new List<User>();
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, name, email, password, is_active, role
            FROM users ORDER BY id;
        ", conn);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapUser(r));
        return list;
    }

    // ── GET BY ID ──────────────────────────────────────────────────────────
    public async Task<User?> GetByIdAsync(int id)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, name, email, password, is_active, role
            FROM users WHERE id = @id LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapUser(r) : null;
    }

    // ── GET BY EMAIL ───────────────────────────────────────────────────────
    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, name, email, password, is_active, role
            FROM users WHERE email = @email LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("email", email);

        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapUser(r) : null;
    }

    // ── UPDATE ─────────────────────────────────────────────────────────────
    public async Task<bool> UpdateAsync(int id, string name, string email, bool isActive, string role)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            UPDATE users
            SET name = @name, email = @email, is_active = @active, role = @role
            WHERE id = @id;
        ", conn);
        cmd.Parameters.AddWithValue("id",     id);
        cmd.Parameters.AddWithValue("name",   name);
        cmd.Parameters.AddWithValue("email",  email);
        cmd.Parameters.AddWithValue("active", isActive);
        cmd.Parameters.AddWithValue("role",   role);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── GET BY GOOGLE ID ───────────────────────────────────────────────────
    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, name, email, password, is_active, role
            FROM users WHERE google_id = @gid LIMIT 1;
        ", conn);
        cmd.Parameters.AddWithValue("gid", googleId);

        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapUser(r) : null;
    }

    // ── CREATE GOOGLE USER ─────────────────────────────────────────────────
    /// <summary>Creates a user signed in via Google. Password is a random unusable hash.</summary>
    public async Task<User> CreateGoogleUserAsync(string name, string email, string googleId)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomBytes(16)));
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO users (name, email, password, is_active, role, google_id)
            VALUES (@name, @email, @password, TRUE, 'user', @gid)
            RETURNING id, name, email, password, is_active, role;
        ", conn);
        cmd.Parameters.AddWithValue("name",     name);
        cmd.Parameters.AddWithValue("email",    email);
        cmd.Parameters.AddWithValue("password", hash);
        cmd.Parameters.AddWithValue("gid",      googleId);

        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return MapUser(r);
    }

    // ── LINK GOOGLE TO EXISTING USER ───────────────────────────────────────
    /// <summary>Attaches a Google sub to an existing user (found by email).</summary>
    public async Task LinkGoogleAsync(int userId, string googleId)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(
            "UPDATE users SET google_id = @gid WHERE id = @id;", conn);
        cmd.Parameters.AddWithValue("id",  userId);
        cmd.Parameters.AddWithValue("gid", googleId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand("DELETE FROM users WHERE id = @id;", conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── JOIN: User with their profile (1:1) ────────────────────────────────
    /// <summary>Returns all users with their printer/filament info joined from user_profiles.</summary>
    public async Task<List<(User User, string PrinterName, string FilamentType)>> GetAllWithProfileAsync()
    {
        var list = new List<(User, string, string)>();
        await using var conn = _database.CreateConnection();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT u.id, u.name, u.email, u.password, u.is_active, u.role,
                   COALESCE(p.printer_name,  '') AS printer_name,
                   COALESCE(p.filament_type, '') AS filament_type
            FROM users u
            LEFT JOIN user_profiles p ON p.user_id = u.id
            ORDER BY u.id;
        ", conn);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add((
                new User
                {
                    Id       = r.GetInt32(0),
                    Name     = r.GetString(1),
                    Email    = r.GetString(2),
                    Password = r.GetString(3),
                    IsActive = r.GetBoolean(4),
                    Role     = r.GetString(5)
                },
                r.GetString(6),
                r.GetString(7)
            ));
        }
        return list;
    }

    private static User MapUser(NpgsqlDataReader r) => new()
    {
        Id       = r.GetInt32(0),
        Name     = r.GetString(1),
        Email    = r.GetString(2),
        Password = r.GetString(3),
        IsActive = r.GetBoolean(4),
        Role     = r.GetString(5)
    };
}
