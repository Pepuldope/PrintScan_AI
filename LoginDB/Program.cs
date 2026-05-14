using System.Text.RegularExpressions;
using DatabazyApiStarter;
using Npgsql;
using DatabazyApiStarter.Models;
using DatabazyApiStarter.Repositories;
using DatabazyApiStarter.Services;

// ── Load .env (local dev only — Railway injects env vars directly) ──────────
LoadEnv.LoadFromDefaultLocations();

// ── Services ───────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ChatRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<MessageImageRepository>();
builder.Services.AddScoped<UserProfileRepository>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── Seed test users ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var users = scope.ServiceProvider.GetRequiredService<UserRepository>();
    await DatabaseSeeder.SeedAsync(users);
}

// ── Migrate: message_images table ──────────────────────────────────────────
{
    var db = app.Services.GetRequiredService<Database>();
    await using var conn = db.CreateConnection();
    await using var cmd  = new NpgsqlCommand(@"
        CREATE TABLE IF NOT EXISTS message_images (
            id         SERIAL PRIMARY KEY,
            message_id INT NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
            file_path  TEXT NOT NULL,
            mime_type  VARCHAR(50) NOT NULL,
            sort_order INT NOT NULL DEFAULT 0
        );
    ", conn);
    await cmd.ExecuteNonQueryAsync();
}

// ── Helper ─────────────────────────────────────────────────────────────────
static IResult Unauthorized() =>
    Results.Json(new { success = false, message = "Unauthorized." }, statusCode: 401);

// ══════════════════════════════════════════════════════════════════════════
// AUTH
// ══════════════════════════════════════════════════════════════════════════

app.MapGet("/api/health", () => Results.Ok(new { success = true, message = "API is running." }));

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth) =>
    Results.Ok(await auth.LoginAsync(req)));

app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth) =>
    Results.Ok(await auth.RegisterAsync(req)));

// ══════════════════════════════════════════════════════════════════════════
// PROFILE
// ══════════════════════════════════════════════════════════════════════════

app.MapGet("/api/profile", async (HttpContext ctx, JwtService jwt, UserProfileRepository profiles) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var profile = await profiles.GetOrCreateAsync(userId.Value);
    return Results.Ok(new
    {
        printerName  = profile.PrinterName,
        filamentType = profile.FilamentType,
        slicer       = profile.Slicer
    });
});

app.MapPut("/api/profile", async (HttpContext ctx, JwtService jwt,
    UserProfileRepository profiles) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var body = await ctx.Request.ReadFromJsonAsync<ProfileUpdateRequest>();
    if (body is null) return Results.BadRequest(new { success = false, message = "Invalid body." });

    var profile = await profiles.UpsertAsync(userId.Value,
        body.PrinterName ?? string.Empty,
        body.FilamentType ?? string.Empty,
        body.Slicer ?? string.Empty);

    return Results.Ok(new
    {
        success      = true,
        printerName  = profile.PrinterName,
        filamentType = profile.FilamentType,
        slicer       = profile.Slicer
    });
});

// ══════════════════════════════════════════════════════════════════════════
// CHATS
// ══════════════════════════════════════════════════════════════════════════

app.MapGet("/api/chats", async (HttpContext ctx, JwtService jwt, ChatRepository chats) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var list = await chats.GetByUserAsync(userId.Value);
    return Results.Ok(list.Select(c => new
    {
        id         = c.Id,
        title      = c.Title,
        isPinned   = c.IsPinned,
        photoCount = c.PhotoCount,
        updatedAt  = c.UpdatedAt
    }));
});

app.MapPost("/api/chats", async (HttpContext ctx, JwtService jwt, ChatRepository chats) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var chat = await chats.CreateAsync(userId.Value);
    return Results.Ok(new
    {
        id         = chat.Id,
        title      = chat.Title,
        isPinned   = chat.IsPinned,
        photoCount = chat.PhotoCount,
        updatedAt  = chat.UpdatedAt
    });
});

app.MapDelete("/api/chats/{id:int}", async (int id, HttpContext ctx,
    JwtService jwt, ChatRepository chats) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var deleted = await chats.DeleteAsync(id, userId.Value);
    return deleted
        ? Results.Ok(new { success = true })
        : Results.NotFound(new { success = false, message = "Chat not found." });
});

app.MapPut("/api/chats/{id:int}/pin", async (int id, HttpContext ctx,
    JwtService jwt, ChatRepository chats) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var body = await ctx.Request.ReadFromJsonAsync<PinRequest>();
    var pin  = body?.IsPinned ?? true;

    var ok = await chats.SetPinnedAsync(id, userId.Value, pin);
    return ok
        ? Results.Ok(new { success = true, isPinned = pin })
        : Results.NotFound(new { success = false, message = "Chat not found." });
});

// ══════════════════════════════════════════════════════════════════════════
// MESSAGES
// ══════════════════════════════════════════════════════════════════════════

var webRootPath = app.Environment.WebRootPath;

app.MapGet("/api/chats/{id:int}/messages", async (int id, HttpContext ctx,
    JwtService jwt, ChatRepository chats, MessageRepository messages, MessageImageRepository messageImages) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var chat = await chats.GetByIdAsync(id, userId.Value);
    if (chat is null) return Results.NotFound(new { success = false, message = "Chat not found." });

    var msgs            = await messages.GetByChatAsync(id);
    var msgsWithPhotos  = msgs.Where(m => m.PhotoCount > 0).Select(m => m.Id).ToList();
    var imagesByMsgId   = await messageImages.GetByMessageIdsAsync(msgsWithPhotos);

    return Results.Ok(msgs.Select(m => new
    {
        id         = m.Id,
        role       = m.Role,
        content    = m.Content,
        photoCount = m.PhotoCount,
        createdAt  = m.CreatedAt,
        imageUrls  = imagesByMsgId.TryGetValue(m.Id, out var imgs)
            ? imgs
                .Where(img => File.Exists(img.FilePath))
                .Select(img => "/" + Path.GetRelativePath(webRootPath, img.FilePath).Replace('\\', '/'))
                .ToList()
            : (IEnumerable<string>)[]
    }));
});

// Multipart form: text field + up to 3 image files
app.MapPost("/api/chats/{id:int}/diagnose", async (int id, HttpContext ctx,
    JwtService jwt, ChatRepository chats, MessageRepository messages,
    MessageImageRepository messageImages, UserProfileRepository profiles, AiService ai) =>
{
    var userId = jwt.GetUserIdFromRequest(ctx.Request);
    if (userId is null) return Unauthorized();

    var chat = await chats.GetByIdAsync(id, userId.Value);
    if (chat is null) return Results.NotFound(new { success = false, message = "Chat not found." });

    if (!ctx.Request.HasFormContentType)
        return Results.BadRequest(new { success = false, message = "Expected multipart/form-data." });

    var form       = await ctx.Request.ReadFormAsync();
    var userText   = form["text"].FirstOrDefault() ?? string.Empty;
    var imageFiles = form.Files.Where(f => f.ContentType.StartsWith("image/")).Take(3).ToList();

    if (string.IsNullOrWhiteSpace(userText) && imageFiles.Count == 0)
        return Results.BadRequest(new { success = false, message = "Message or photo required." });

    // Enforce per-chat photo limit (6 total)
    var newPhotoCount = imageFiles.Count;
    if (chat.PhotoCount + newPhotoCount > 6)
        return Results.BadRequest(new
        {
            success = false,
            message = $"Photo limit reached. This chat has used {chat.PhotoCount}/6 photos."
        });

    // Read images into base64
    var photos = new List<(string Base64, string MimeType)>();
    foreach (var file in imageFiles)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        photos.Add((Convert.ToBase64String(ms.ToArray()), file.ContentType));
    }

    // Load history for context, re-attaching any stored images from disk
    var allMsgs         = await messages.GetByChatAsync(id);
    var msgsWithPhotos  = allMsgs.Where(m => m.PhotoCount > 0).Select(m => m.Id).ToList();
    var imagesByMsgId   = await messageImages.GetByMessageIdsAsync(msgsWithPhotos);

    var history = new List<HistoryMessage>();
    foreach (var m in allMsgs)
    {
        var imgs = new List<(string Base64, string MimeType)>();
        if (imagesByMsgId.TryGetValue(m.Id, out var fileList))
        {
            foreach (var (filePath, mimeType) in fileList)
            {
                if (File.Exists(filePath))
                    imgs.Add((Convert.ToBase64String(await File.ReadAllBytesAsync(filePath)), mimeType));
            }
        }
        history.Add(new HistoryMessage(m.Role, m.Content, imgs));
    }

    // Load user profile for context
    var profile = await profiles.GetOrCreateAsync(userId.Value);

    // Call AI
    string aiResponse;
    try
    {
        aiResponse = await ai.DiagnoseAsync(
            userText, profile.PrinterName, profile.FilamentType, profile.Slicer, photos, history);
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = $"AI error: {ex.Message}" }, statusCode: 502);
    }

    // Save user message and persist any uploaded images to disk
    var displayText = string.IsNullOrWhiteSpace(userText) ? "(photo only)" : userText;
    var userMsg     = await messages.AddAsync(id, "user", displayText, newPhotoCount);

    if (newPhotoCount > 0)
    {
        var uploadDir = Path.Combine(webRootPath, "uploads", id.ToString());
        Directory.CreateDirectory(uploadDir);
        for (int i = 0; i < imageFiles.Count; i++)
        {
            var ext = imageFiles[i].ContentType switch
            {
                "image/png"  => ".png",
                "image/webp" => ".webp",
                "image/gif"  => ".gif",
                _            => ".jpg"
            };
            var filePath = Path.Combine(uploadDir, $"{userMsg.Id}_{i}{ext}");
            await File.WriteAllBytesAsync(filePath, Convert.FromBase64String(photos[i].Base64));
            await messageImages.AddAsync(userMsg.Id, filePath, imageFiles[i].ContentType, i);
        }
    }

    await messages.AddAsync(id, "assistant", aiResponse, 0);

    // Update chat metadata
    if (chat.Title == "New Chat")
    {
        string title;
        if (!string.IsNullOrWhiteSpace(userText))
        {
            title = userText.Length > 60 ? userText[..57] + "..." : userText;
        }
        else
        {
            // Image-only: pull the bold diagnosis line from the AI response
            var m = Regex.Match(aiResponse, @"\*\*(.+?)\*\*");
            title = m.Success ? m.Groups[1].Value : "Photo diagnosis";
        }
        await chats.UpdateTitleAsync(id, title);
    }
    else
    {
        await chats.TouchAsync(id);
    }

    if (newPhotoCount > 0)
        await chats.IncrementPhotoCountAsync(id, newPhotoCount);

    return Results.Ok(new
    {
        success    = true,
        aiResponse,
        photoCount = chat.PhotoCount + newPhotoCount
    });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5171";
app.Run($"http://0.0.0.0:{port}");

// ── Local request DTOs ─────────────────────────────────────────────────────
record ProfileUpdateRequest(string? PrinterName, string? FilamentType, string? Slicer);
record PinRequest(bool IsPinned);
