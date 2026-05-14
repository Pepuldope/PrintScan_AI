using BC = BCrypt.Net.BCrypt;
using DatabazyApiStarter.Repositories;

namespace DatabazyApiStarter.Services;

/// <summary>
/// Creates test accounts at startup with properly hashed passwords.
/// Safe to run repeatedly — skips accounts that already have bcrypt hashes.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Name, string Email, string Password, string Role)[] TestUsers =
    [
        ("Peter",   "peter@test.com",   "peter123",   "user"),
        ("Anna",    "anna@test.com",    "anna123",    "user"),
        ("Admin",   "admin@test.com",   "admin123",   "admin"),
    ];

    public static async Task SeedAsync(UserRepository users)
    {
        foreach (var (name, email, password, role) in TestUsers)
        {
            var existing = await users.GetByEmailAsync(email);

            // Skip if already has a bcrypt hash
            if (existing is not null && existing.Password.StartsWith("$2"))
                continue;

            // Delete stale plain-text account if it exists
            if (existing is not null)
                await users.DeleteAsync(existing.Id);

            var hash = BC.HashPassword(password);
            await users.CreateAsync(name, email, hash, role);
        }
    }
}
