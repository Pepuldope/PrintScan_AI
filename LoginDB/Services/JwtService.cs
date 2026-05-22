using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DatabazyApiStarter.Services;

public record JwtClaims(int UserId, string Role);

public record AnonClaims(string Sub, int Count);

public class JwtService
{
    private readonly byte[] _secret;

    public JwtService()
    {
        var raw = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? throw new InvalidOperationException("JWT_SECRET is not set in .env");
        _secret = Encoding.UTF8.GetBytes(raw);
    }

    public string GenerateToken(int userId, string role = "user")
    {
        var header  = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var exp     = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var payload = Base64UrlEncode($"{{\"sub\":\"{userId}\",\"role\":\"{role}\",\"exp\":{exp}}}");

        var data = $"{header}.{payload}";
        using var hmac = new HMACSHA256(_secret);
        var sig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        return $"{data}.{sig}";
    }

    public JwtClaims? ValidateToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var data = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_secret);
            var expectedSig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
            if (expectedSig != parts[2]) return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var exp = doc.RootElement.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

            var sub = doc.RootElement.GetProperty("sub").GetString();
            if (!int.TryParse(sub, out var id)) return null;

            // Role is optional for backward compatibility with old tokens
            var role = doc.RootElement.TryGetProperty("role", out var roleEl)
                ? (roleEl.GetString() ?? "user")
                : "user";

            return new JwtClaims(id, role);
        }
        catch { return null; }
    }

    public JwtClaims? GetClaimsFromRequest(HttpRequest request)
    {
        var auth = request.Headers.Authorization.FirstOrDefault();
        if (auth is null || !auth.StartsWith("Bearer ")) return null;
        return ValidateToken(auth["Bearer ".Length..]);
    }

    public int? GetUserIdFromRequest(HttpRequest request) =>
        GetClaimsFromRequest(request)?.UserId;

    public string GenerateAnonToken(string sub, int count)
    {
        var header  = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var exp     = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        var payload = Base64UrlEncode(
            $"{{\"sub\":\"{sub}\",\"kind\":\"anon\",\"count\":{count},\"exp\":{exp}}}");

        var data = $"{header}.{payload}";
        using var hmac = new HMACSHA256(_secret);
        var sig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        return $"{data}.{sig}";
    }

    public AnonClaims? ValidateAnonToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var data = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_secret);
            var expectedSig = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
            if (expectedSig != parts[2]) return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("kind", out var kindEl) || kindEl.GetString() != "anon")
                return null;

            var exp = root.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;

            var sub   = root.GetProperty("sub").GetString() ?? "";
            var count = root.TryGetProperty("count", out var cEl) ? cEl.GetInt32() : 0;
            return new AnonClaims(sub, count);
        }
        catch { return null; }
    }

    public AnonClaims? GetAnonClaimsFromRequest(HttpRequest request)
    {
        var hdr = request.Headers["X-Anon-Token"].FirstOrDefault();
        return string.IsNullOrEmpty(hdr) ? null : ValidateAnonToken(hdr);
    }

    private static string Base64UrlEncode(string input) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(input));

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        input = input.Replace('-', '+').Replace('_', '/');
        switch (input.Length % 4)
        {
            case 2: input += "=="; break;
            case 3: input += "=";  break;
        }
        return Convert.FromBase64String(input);
    }
}
