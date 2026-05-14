using BC = BCrypt.Net.BCrypt;
using DatabazyApiStarter.Models;
using DatabazyApiStarter.Repositories;

namespace DatabazyApiStarter.Services;

public class AuthService
{
    private readonly UserRepository _users;
    private readonly JwtService _jwt;

    public AuthService(UserRepository users, JwtService jwt)
    {
        _users = users;
        _jwt   = jwt;
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
            Token       = _jwt.GenerateToken(user.Id)
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
            Token       = _jwt.GenerateToken(user.Id)
        };
    }

    private static LoginResponse Error(string message) =>
        new() { Success = false, Message = message };
}
