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

builder.Services.AddHttpClient();
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

// ── Migrate: message_images table + users.created_at column ───────────────
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

        ALTER TABLE users
            ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

        ALTER TABLE users
            ADD COLUMN IF NOT EXISTS google_id VARCHAR(50);

        CREATE UNIQUE INDEX IF NOT EXISTS users_google_id_unique
            ON users (google_id) WHERE google_id IS NOT NULL;

        ALTER TABLE messages
            ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'complete';

        -- Recover from server restart mid-AI-call: stale 'pending' rows become 'failed'.
        UPDATE messages
            SET status = 'failed',
                content = COALESCE(NULLIF(content, ''), 'AI request interrupted — please try again.')
            WHERE status = 'pending'
              AND created_at < NOW() - INTERVAL '5 minutes';
    ", conn);
    await cmd.ExecuteNonQueryAsync();
}

// ── Anonymous mode limits ──────────────────────────────────────────────────
const int AnonMaxDiagnoses = 3;

// ── Helpers ────────────────────────────────────────────────────────────────
static IResult Unauthorized() =>
    Results.Json(new { success = false, message = "Unauthorized." }, statusCode: 401);

static IResult RequireLogin(string message) =>
    Results.Json(new { success = false, requireLogin = true, message }, statusCode: 403);

// ══════════════════════════════════════════════════════════════════════════
// AUTH
// ══════════════════════════════════════════════════════════════════════════

app.MapGet("/api/health", () => Results.Ok(new { success = true, message = "API is running." }));

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth) =>
    Results.Ok(await auth.LoginAsync(req)));

app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth) =>
    Results.Ok(await auth.RegisterAsync(req)));

app.MapPost("/api/auth/google", async (GoogleLoginRequest req, AuthService auth) =>
    Results.Ok(await auth.GoogleLoginAsync(req.IdToken ?? string.Empty)));

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
        status     = m.Status,
        imageUrls  = imagesByMsgId.TryGetValue(m.Id, out var imgs)
            ? imgs
                .Where(img => File.Exists(img.FilePath))
                .Select(img => "/" + Path.GetRelativePath(webRootPath, img.FilePath).Replace('\\', '/'))
                .ToList()
            : (IEnumerable<string>)[]
    }));
});

// Multipart form: text field + up to 3 image files
// Fire-and-forget: persists user message + placeholder assistant message immediately,
// then runs the AI in a detached background task that survives client disconnects.
app.MapPost("/api/chats/{id:int}/diagnose", async (int id, HttpContext ctx,
    JwtService jwt, ChatRepository chats, MessageRepository messages,
    MessageImageRepository messageImages, UserProfileRepository profiles,
    IServiceScopeFactory scopeFactory, ILoggerFactory logFactory) =>
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

    // Read images into base64 — captured now so the request body can close
    var photos = new List<(string Base64, string MimeType)>();
    foreach (var file in imageFiles)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        photos.Add((Convert.ToBase64String(ms.ToArray()), file.ContentType));
    }

    // Persist user message + uploaded photos to disk
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
        await chats.IncrementPhotoCountAsync(id, newPhotoCount);
    }

    // Insert pending assistant message so client (and any other tab) can poll for it
    var pendingMsg = await messages.AddAsync(id, "assistant", "", 0, status: "pending");

    // Load history + profile NOW so the background task has self-contained inputs
    var allMsgs         = await messages.GetByChatAsync(id);
    var msgsWithPhotos  = allMsgs.Where(m => m.PhotoCount > 0).Select(m => m.Id).ToList();
    var imagesByMsgId   = await messageImages.GetByMessageIdsAsync(msgsWithPhotos);

    var history = new List<HistoryMessage>();
    foreach (var m in allMsgs.Where(m => m.Id != pendingMsg.Id))   // skip the empty placeholder
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

    var profile = await profiles.GetOrCreateAsync(userId.Value);

    // Capture immutable inputs for the background task
    var capturedText      = userText;
    var capturedPrinter   = profile.PrinterName;
    var capturedFilament  = profile.FilamentType;
    var capturedSlicer    = profile.Slicer;
    var capturedChatTitle = chat.Title;
    var capturedChatId    = id;
    var capturedMsgId     = pendingMsg.Id;
    var capturedHistory   = history;
    var capturedPhotos    = photos;

    // Fire-and-forget: AI completes regardless of client navigation.
    _ = Task.Run(async () =>
    {
        var log = logFactory.CreateLogger("DiagnoseBackground");
        using var scope = scopeFactory.CreateScope();
        var bgAi       = scope.ServiceProvider.GetRequiredService<AiService>();
        var bgMessages = scope.ServiceProvider.GetRequiredService<MessageRepository>();
        var bgChats    = scope.ServiceProvider.GetRequiredService<ChatRepository>();

        try
        {
            var aiResponse = await bgAi.DiagnoseAsync(
                capturedText, capturedPrinter, capturedFilament, capturedSlicer,
                capturedPhotos, capturedHistory);

            await bgMessages.UpdateContentAndStatusAsync(capturedMsgId, aiResponse, "complete");

            // Update chat title if still default, else just bump updated_at
            if (capturedChatTitle == "New Chat")
            {
                string title;
                if (!string.IsNullOrWhiteSpace(capturedText))
                {
                    title = capturedText.Length > 60 ? capturedText[..57] + "..." : capturedText;
                }
                else
                {
                    var m = Regex.Match(aiResponse, @"\*\*(.+?)\*\*");
                    title = m.Success ? m.Groups[1].Value : "Photo diagnosis";
                }
                await bgChats.UpdateTitleAsync(capturedChatId, title);
            }
            else
            {
                await bgChats.TouchAsync(capturedChatId);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Background AI diagnose failed for message {MsgId}", capturedMsgId);
            try
            {
                await bgMessages.UpdateContentAndStatusAsync(
                    capturedMsgId,
                    "AI error — please try again. (" + ex.Message + ")",
                    "failed");
            }
            catch (Exception inner)
            {
                log.LogError(inner, "Failed to mark message {MsgId} as failed", capturedMsgId);
            }
        }
    });

    return Results.Ok(new
    {
        success            = true,
        status             = "pending",
        userMessageId      = userMsg.Id,
        assistantMessageId = pendingMsg.Id,
        photoCount         = chat.PhotoCount + newPhotoCount
    });
});

// ══════════════════════════════════════════════════════════════════════════
// ANONYMOUS (no account) — single-turn diagnose with stateless usage counter
// ══════════════════════════════════════════════════════════════════════════

app.MapPost("/api/anon/diagnose", async (HttpContext ctx, JwtService jwt, AiService ai) =>
{
    var existing = jwt.GetAnonClaimsFromRequest(ctx.Request);
    var count    = existing?.Count ?? 0;
    var sub      = existing?.Sub ?? Guid.NewGuid().ToString("N");

    if (count >= AnonMaxDiagnoses)
        return RequireLogin($"You've used your {AnonMaxDiagnoses} free diagnoses. Sign in or register to keep going.");

    if (!ctx.Request.HasFormContentType)
        return Results.BadRequest(new { success = false, message = "Expected multipart/form-data." });

    var form       = await ctx.Request.ReadFormAsync();
    var userText   = form["text"].FirstOrDefault() ?? string.Empty;
    var imageFiles = form.Files.Where(f => f.ContentType.StartsWith("image/")).Take(3).ToList();

    if (string.IsNullOrWhiteSpace(userText) && imageFiles.Count == 0)
        return Results.BadRequest(new { success = false, message = "Message or photo required." });

    // Read images into memory only — never persisted to disk for anon traffic
    var photos = new List<(string Base64, string MimeType)>();
    foreach (var file in imageFiles)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        photos.Add((Convert.ToBase64String(ms.ToArray()), file.ContentType));
    }

    // Parse client-supplied history (anon mode has no server-side persistence).
    // Text-only — anon image bytes aren't preserved across turns.
    var history     = new List<HistoryMessage>();
    var historyJson = form["history"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(historyJson))
    {
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<AnonHistoryItem>>(
                historyJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed != null)
            {
                // Cap to the last 20 turns to keep prompts bounded
                foreach (var p in parsed.TakeLast(20))
                {
                    var role = p.Role == "assistant" ? "assistant" : "user";
                    history.Add(new HistoryMessage(role, p.Content ?? "", new List<(string, string)>()));
                }
            }
        }
        catch { /* malformed — proceed with empty history */ }
    }

    string aiResponse;
    try
    {
        aiResponse = await ai.DiagnoseAsync(
            userText, "", "", "", photos, history);
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = $"AI error: {ex.Message}" }, statusCode: 502);
    }

    var newToken = jwt.GenerateAnonToken(sub, count + 1);
    return Results.Ok(new
    {
        success    = true,
        aiResponse,
        anonToken  = newToken,
        usageCount = count + 1,
        usageLimit = AnonMaxDiagnoses
    });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5171";
app.Run($"http://0.0.0.0:{port}");

// ── Local request DTOs ─────────────────────────────────────────────────────
record ProfileUpdateRequest(string? PrinterName, string? FilamentType, string? Slicer);
record PinRequest(bool IsPinned);
record GoogleLoginRequest(string? IdToken);
record AnonHistoryItem(string? Role, string? Content);
