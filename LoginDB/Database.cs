using Npgsql;

namespace DatabazyApiStarter;

public class Database
{
    private readonly string _connectionString;

    public Database()
    {
        _connectionString = BuildConnectionString();
    }

    public NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string BuildConnectionString()
    {
        // Railway provides DATABASE_URL; fall back to individual vars for local dev
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            var uri      = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            var host     = uri.Host;
            var port     = uri.Port;
            var database = uri.AbsolutePath.TrimStart('/');
            var user     = userInfo[0];
            var password = Uri.UnescapeDataString(userInfo[1]);
            return $"Host={host};Port={port};Database={database};Username={user};Password={password};" +
                   "SSL Mode=Require;Trust Server Certificate=true;";
        }

        var h = GetRequiredVariable("DB_HOST");
        var p = GetRequiredVariable("DB_PORT");
        var db = GetRequiredVariable("DB_NAME");
        var u = GetRequiredVariable("DB_USER");
        var pw = GetRequiredVariable("DB_PASSWORD");
        return $"Host={h};Port={p};Database={db};Username={u};Password={pw};";
    }

    private static string GetRequiredVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Environment variable '{variableName}' is missing. Check your .env file."
            );
        }

        return value;
    }
}
