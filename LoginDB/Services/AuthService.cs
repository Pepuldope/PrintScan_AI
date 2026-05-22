using System.Text.Json;
using BC = BCrypt.Net.BCrypt;
using DatabazyApiStarter.Models;
using DatabazyApiStarter.Repositories;

namespace DatabazyApiStarter.Services;

public class AuthService
{
    private readonly UserRepository _users;
    private readonly JwtService _jwt;
    private readonly IHttpClientFactory _httpFactory;

    public AuthService(UserRepository users, JwtService jwt, IHttpClientFactory httpFactory)
    {
        _users       = users;
        _jwt         = jwt;
        _httpFactory = httpFactory;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Error("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return Error("Password is required.");

        if (!request.Email.Contains('@'))
            return Error("Invalid email format.");

        var user = await _users.GetByEmailAsync(request.Email.Trim());
        if (user is null)
            return Error("No account found with that email.");

        if (!user.IsActive)
            return Error("This account is inactive.");

        if (!BC.Verify(request.Password, user.Password))
            return Error("Incorrect password.");

        return new LoginResponse
        {
            Success     = true,
            Message     = "Login successful.",
            DisplayName = user.Name,
            Email       = user.Email,
            Role        = user.Role,
            Token       = _jwt.GenerateToken(user.Id, user.Role)
        };
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error("Name is required.");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return Error("Valid email is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Error("Password must be at least 6 characters.");

        var existing = await _users.GetByEmailAsync(request.Email.Trim());
        if (existing is not null)
            return Error("An account with this email already exists.");

        var hash = BC.HashPassword(request.Password);
        var user = await _users.CreateAsync(request.Name.Trim(), request.Email.Trim(), hash, "user");

        return new LoginResponse
        {
            Success     = true,
            Message     = "Account created successfully.",
            DisplayName = user.Name,
            Email       = user.Email,
            Role        = user.Role,
            Token       = _jwt.GenerateToken(user.Id, user.Role)
        };
    }

    public async Task<LoginResponse> GoogleLoginAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return Error("Missing Google ID token.");

        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(clientId))
            return Error("Google sign-in is not configured on the server.");

        // Verify the token via Google's tokeninfo endpoint
        var http = _httpFactory.CreateClient();
        var url  = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
        HttpResponseMessage resp;
        try { resp = await http.GetAsync(url); }
        catch { return Error("Could not reach Google to verify sign-in."); }

        if (!resp.IsSuccessStatusCode)
            return Error("Invalid Google sign-in token.");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        if (!root.TryGetProperty("aud", out var audEl) || audEl.GetString() != clientId)
            return Error("Google token audience mismatch.");

        if (!root.TryGetProperty("email_verified", out var verEl) || verEl.GetString() != "true")
            return Error("Your Google account email is not verified.");

        var sub   = root.GetProperty("sub").GetString();
        var email = root.GetProperty("email").GetString();
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
            return Error("Google token is missing required fields.");

        var name = root.TryGetProperty("name", out var nameEl)
            ? (nameEl.GetString() ?? email)
            : email;

        // Find or create the user
        var user = await _users.GetByGoogleIdAsync(sub);
        if (user is null)
        {
            // First-time Google sign-in — link if email already exists, else create new
            var byEmail = await _users.GetByEmailAsync(email);
            if (byEmail is not null)
            {
                await _users.LinkGoogleAsync(byEmail.Id, sub);
                user = byEmail;
            }
            else
            {
                user = await _users.CreateGoogleUserAsync(name, email, sub);
            }
        }

        if (!user.IsActive)
            return Error("This account is inactive.");

        return new LoginResponse
        {
            Success     = true,
            Message     = "Google sign-in successful.",
            DisplayName = user.Name,
            Email       = user.Email,
            Role        = user.Role,
            Token       = _jwt.GenerateToken(user.Id, user.Role)
        };
    }

    private static LoginResponse Error(string message) =>
        new() { Success = false, Message = message };
}
