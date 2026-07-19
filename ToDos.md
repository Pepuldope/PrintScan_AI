# PrintScan AI — External-user readiness ToDos

**Repo:** [Pepuldope/PrintScan_AI](https://github.com/Pepuldope/PrintScan_AI)  
**Last audited:** 2026-07-19  
**Maturity rating:** School PoC → early alpha (~1.5 / 5 external readiness)  
**Live URL check:** `https://printscan-ai.up.railway.app` → **404 Application not found** (not currently deployed)

This document turns the full gap analysis into an actionable backlog.  
Priority legend:

| Priority | Meaning |
|----------|---------|
| **P0** | Blockers — do not invite strangers until fixed |
| **P1** | Needed for a real usable MVP |
| **P2** | Reliability / ops / quality |
| **P3** | Polish, growth, differentiation |

Status: `[ ]` open · `[~]` in progress · `[x]` done

---

## Ownership split (who does what)

Two lanes. **Agent-solo** = code/docs/repo changes I can implement without your accounts. **Needs you** = accounts, money, legal identity, secrets, product decisions, or console clicks only you can do. Many features need **both**: you set up X, then I wire the code.

### Lane A — Agent can do alone (code / repo)

#### A1. Repo hygiene & docs (start anytime)
- [ ] Remove tracked sample uploads from git; add `wwwroot/uploads/` to `.gitignore`
- [ ] Stop shipping `testing-credentials.txt` / gate or delete plaintext SQL seed for prod mindset
- [ ] Gate `DatabaseSeeder` behind Development / `SEED_TEST_USERS=true` (never default-on in prod)
- [ ] Root `README.md`: local run, env var list, deploy notes
- [ ] Fix outdated docs (`Dokumentacia.txt` still wrong about photo storage)
- [ ] Dockerfile: drop `chmod 777`; don’t copy test junk into image
- [ ] `.dockerignore` hygiene

#### A2. Security hardening in app code
- [ ] CORS from env allowlist (not `AllowAnyOrigin`)
- [ ] `AllowedHosts` from config (not `*`)
- [ ] Rate limiting middleware (login/register/google/anon diagnose/diagnose)
- [ ] Unify auth error messages (anti-enumeration)
- [ ] Stronger email validation + max field lengths + password policy (>6 chars)
- [ ] Check `IsActive` on every authenticated request
- [ ] Sanitize AI/API errors (no `ex.Message` / raw upstream to clients)
- [ ] Upload: max bytes, magic-byte / decode validation, MIME allowlist (jpeg/png/webp)
- [ ] Downscale images before base64 + AI
- [ ] Auth-gated image download endpoint (stop public static `/uploads`)
- [ ] In-memory or DB-backed **server** anon quota (not only client JWT counter) — works without Redis for single instance
- [ ] Transactional photo-count + chat create/delete oldest
- [ ] Replace hand-rolled JWT with standard library **or** harden current one (exp, optional jti store later)
- [ ] Shorten access token TTL; structure for future refresh
- [ ] Postgres connection string via `NpgsqlConnectionStringBuilder`; make cert trust env-flagged (default verify in prod)
- [ ] Auto-create full schema on boot (or single migrate path) so fresh DB works
- [ ] Bound background diagnose work (channel/queue + concurrency cap + shutdown cancel) instead of naked `Task.Run`
- [ ] Remove or ignore dead admin seed until real admin exists
- [ ] OpenRouter referer/title from public site URL env var
- [ ] Model IDs + temperature from env (code ready for paid models once key exists)

#### A3. Product / UX code (no third parties required)
- [ ] Landing page (value prop, how it works, CTA, disclaimer stub)
- [ ] Empty chat-list state; first-run guidance
- [ ] Anon usage meter UI (“2 of 3”)
- [ ] Carry guest history across login/register (stop wiping on wall; optional merge after auth)
- [ ] Confirm dialogs (delete chat / clear anon)
- [ ] Pin/delete success/error toast
- [ ] Retry failed AI message
- [ ] Pending timeout / still-working messaging
- [ ] Consistent API error/retry on chat list + profile load
- [ ] Photo quota UX (block staging when over remaining)
- [ ] Explain discarded extra files
- [ ] Image lightbox
- [ ] Copy / share / feedback controls on AI replies
- [ ] Stronger markdown / structured diagnosis layout
- [ ] In-UI AI disclaimer + footer links placeholders (Privacy / Terms)
- [ ] Profile onboarding copy improvements
- [ ] Mobile: safe-area, keyboard/`100vh` fixes, touch-visible pin/delete
- [ ] A11y: dialog roles, focus trap, keyboard chat rows/upload, aria-live, focus-visible, contrast tweaks
- [ ] Optional SK/EN string externalization (if you want bilingual — decision is yours, implementation is mine)

#### A4. Feature code that stays dark until you configure services
I can implement these fully against env vars / test doubles; they only go live after Lane B items:
- [ ] Password reset **flow** (token table + pages) — needs email provider (B)
- [ ] Email verification **flow** — needs email provider (B)
- [ ] Delete account + data export endpoints/UI
- [ ] Change password
- [ ] Object-storage adapter (S3-compatible API) behind interface — needs bucket creds (B)
- [ ] Stripe Checkout + webhooks + entitlement checks — needs Stripe account (B)
- [ ] Usage metering tables + enforce free/Pro limits (limits numbers need your decision)
- [ ] Sentry/OpenTelemetry hooks — needs DSN (B)
- [ ] Captcha verify endpoint — needs Turnstile/hCaptcha keys (B)
- [ ] Health check: DB ping + config presence

#### A5. AI quality (offline / code)
- [ ] Prompt + structured-output schema improvements
- [ ] History/token budget caps in `AiService`
- [ ] Model fallback chain in code
- [ ] Eval harness script + folder for labeled photos (you may need to supply real failure photos)
- [ ] Printer/filament/slicer dropdown catalogs (static JSON is fine to start)

---

### Lane B — Needs you to set up / decide

#### B1. Hosting & access (required for any public users)
- [ ] Railway (or other host) project alive again; billing OK
- [ ] Managed Postgres plugin/instance
- [ ] Set production secrets: `JWT_SECRET`, `DATABASE_URL`, `AI_API_KEY`, `GOOGLE_CLIENT_ID`, public `APP_URL` / CORS origins
- [ ] Custom domain + DNS (optional but recommended)
- [ ] Google Cloud console: OAuth client authorized JS origins + redirect URIs for production domain
- [ ] Confirm whether any old prod DB/users exist; if test accounts were public, wipe or rotate
- [ ] Merge/deploy permissions (approve PRs; who deploys)

#### B2. AI budget & vendor
- [ ] OpenRouter account with **billing** (or alternative provider)
- [ ] Choose paid vision + text models (or approve my recommendation)
- [ ] Hard monthly budget / kill-switch preference
- [ ] Decide free-tier diagnose limits (anon + signed-in free) and Pro limits

#### B3. Storage
- [ ] S3-compatible bucket (Cloudflare R2 / AWS S3 / etc.) + access key + public/signed URL strategy preference
- [ ] Retention preference (delete photos after N days?)

#### B4. Email (password reset / verify)
- [ ] Provider account (Resend, SendGrid, Postmark, …)
- [ ] Sending domain DNS (SPF/DKIM)
- [ ] From-address you own (e.g. `noreply@yourdomain`)

#### B5. Payments (only if real freemium)
- [ ] Stripe account (or region-appropriate PSP), KYC as required
- [ ] Decide price (e.g. €6/mo), trial, what Pro includes
- [ ] Create Product/Price; hand webhook secret + publishable/secret keys (via env, not chat if possible)
- [ ] Business identity for invoices/customer support

#### B6. Legal / trust (you own the words & identity)
- [ ] Operator identity (you personally vs company) and contact email for support + privacy
- [ ] Approve Privacy Policy + Terms (I can draft; you must approve / adapt for SK/EU)
- [ ] AI disclaimer wording approval
- [ ] Cookie/analytics decision (none vs Plausible/etc.)
- [ ] If taking money in EU: imprint / consumer rules awareness (not legal advice — you may want a human check)

#### B7. Optional third parties
- [ ] Bot protection: Cloudflare Turnstile or hCaptcha site keys
- [ ] Error tracking: Sentry (or similar) DSN
- [ ] Uptime monitor (Better Stack, UptimeRobot, …)
- [ ] Status page (optional)

#### B8. Product decisions only you should make
- [ ] Ship target: “safe public demo” vs “paid MVP” vs “class demo only”
- [ ] Language: EN only vs SK/EN
- [ ] Whether Google login stays mandatory-optional
- [ ] Whether admin panel is in scope
- [ ] Brand name, logo, tone for landing copy final sign-off
- [ ] Supply ~50–100 real failed-print photos for eval (or approve synthetic/public dataset use)

---

### Dependency map (feature → what you must provide)

| Feature | Agent does | You provide |
|---------|------------|-------------|
| Live site | deploy config in repo, health, Dockerfile fixes | Railway/Postgres up + secrets |
| Safe free demo | seeder off, rate limits, anon quota, private images local/S3 adapter, landing, disclaimer | Host + `AI_API_KEY` + budget; domain; legal contact |
| Durable private photos | storage interface + auth URLs | R2/S3 bucket + keys |
| Password reset / verify email | tokens, pages, API | Email provider + domain DNS |
| Paid Pro | Stripe code + entitlements | Stripe account + price decision + keys |
| Google login in prod | keep GSI wiring | Google Console origins for real domain |
| Monitoring | SDK wiring | Sentry DSN + uptime account |
| Captcha | verify API + UI | Turnstile/hCaptcha keys |
| “AI is good” claims | harness + prompts | Eval photos + paid model choice |

---

### Recommended sequence

**You first (unblocks almost everything):**  
1) Host + Postgres back up · 2) Fresh secrets · 3) OpenRouter billing + model choice · 4) Support/privacy email · 5) Say “demo only” vs “paid MVP”.

**I first (parallel, no waiting):**  
A1 hygiene → A2 security → A3 UX/landing → storage interface + quota + seeder gate.

**Then together:**  
Wire S3/email/Stripe only after B items exist; deploy Milestone A (safe public demo) before inviting anyone.

---

## Snapshot: what exists today

### Product intent
“Shazam for 3D printers” — upload a photo of a failed print → plain-language diagnosis + slicer fixes. Business materials model freemium SaaS (~€6/mo Pro).

### Tech stack
| Layer | Reality |
|--------|---------|
| Backend | ASP.NET Core **.NET 10** minimal APIs (`LoginDB/Program.cs`) |
| DB | PostgreSQL via Npgsql; partial schema + startup SQL “migrations” |
| Auth | Email/password (BCrypt) + Google Sign-In + hand-rolled HS256 JWT |
| AI | OpenRouter, **free** NVIDIA models (vision describe → text diagnose) |
| Frontend | Static HTML/CSS/JS: `index`, `register`, `chat`, `profile` |
| Deploy | Dockerfile + `railway.toml` (health `/api/health`) |

### Working user journey (when hosted)
1. Try anon on `/chat.html` (3 free diagnoses via `X-Anon-Token`), or register / login / Google  
2. Optional profile: printer / filament / slicer  
3. New chat → photos (≤3/msg, ≤6/chat) + text → AI diagnose  
4. Signed-in: history in Postgres, pending AI polled every 2s, auto titles, pin 1 chat, max 5 non-pinned chats  

### API surface
| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/health` | Public |
| `POST` | `/api/auth/login` | Public |
| `POST` | `/api/auth/register` | Public |
| `POST` | `/api/auth/google` | Public |
| `GET`/`PUT` | `/api/profile` | JWT |
| `GET`/`POST` | `/api/chats` | JWT |
| `DELETE` | `/api/chats/{id}` | JWT + owner |
| `PUT` | `/api/chats/{id}/pin` | JWT + owner |
| `GET` | `/api/chats/{id}/messages` | JWT + owner |
| `POST` | `/api/chats/{id}/diagnose` | JWT + owner |
| `POST` | `/api/anon/diagnose` | Optional `X-Anon-Token` |

### Positive controls already in place
- [x] Parameterized SQL (no obvious SQLi)
- [x] BCrypt password hashing
- [x] Chat/message routes scoped by `user_id`
- [x] FK cascades on user/profile/chat/message
- [x] Google token audience + verified-email checks
- [x] Core diagnose loop (photo + multi-turn chat) is real
- [x] Mobile sidebar pattern exists
- [x] Docker + Railway shape exists

---

## Scorecard (audit)

| Area | Score (0–5) | Note |
|------|-------------|------|
| Core demo loop | **4** | Chat + photo + AI is real |
| Auth basics | **3** | Login/register/Google work; lifecycle missing |
| Security for public internet | **1.5** | Lab defaults, open abuse paths, public uploads |
| Reliability / hosting | **1** | App down; ephemeral files; free AI |
| Product packaging | **1.5** | No landing, legal, support, onboarding |
| Business / payments | **0** | Plan only on paper |
| AI quality process | **2** | Good prompts; free models; no evals |
| Ops / observability | **1** | Health check only |
| **Overall external readiness** | **~1.5 / 5** | Great school PoC; not shippable SaaS |

---

## Minimum path to “strangers can use this safely”

Do roughly in this order before marketing:

1. Redeploy + secrets + kill production seeder + remove credentials/uploads from git  
2. Object storage + auth-gated (or signed) image URLs  
3. Paid AI model + hard quotas + rate limits  
4. Landing + AI disclaimer + privacy/ToS + support contact  
5. Password reset + delete account  
6. Stripe freemium (one Pro tier) so cost has a ceiling  
7. Monitoring (uptime + errors) + image size limits  
8. Eval pack of ~50–100 labeled failure photos before quality claims  

---

# P0 — Blockers

## Hosting & production posture

- [ ] **Redeploy** the app (Railway or equivalent) so a public URL works again  
  - Evidence: `printscan-ai.up.railway.app` returned 404 Application not found (2026-07-19)
- [ ] Confirm production env vars: `DATABASE_URL` (or `DB_*`), `JWT_SECRET`, `AI_API_KEY`, `GOOGLE_CLIENT_ID`, `PORT`
- [ ] Custom domain + TLS (proxy OK) documented in a short runbook
- [ ] **Disable DatabaseSeeder in production**  
  - Evidence: `LoginDB/Program.cs` runs seeder every startup; `Services/DatabaseSeeder.cs` always creates:
    - `peter@test.com` / `peter123`
    - `anna@test.com` / `anna123`
    - `admin@test.com` / `admin123`
- [ ] Remove or stop shipping `LoginDB/testing-credentials.txt` in public repo / prod images
- [ ] Remove or scrub `LoginDB/sql/02_seed_users.sql` plaintext passwords; keep seed **dev-only**
- [ ] If any environment was ever public with default test accounts: **rotate** those credentials / wipe DB

## AI cost & reliability (demo-grade today)

- [ ] Stop depending solely on free OpenRouter models for public traffic  
  - Evidence: `LoginDB/Services/AiService.cs` hardcodes:
    - `nvidia/nemotron-3-super-120b-a12b:free`
    - `nvidia/nemotron-nano-12b-v2-vl:free`
- [ ] Configure **paid / stable** vision + text models via env (not hardcoded free IDs only)
- [ ] Per-user **quotas** for signed-in diagnose (free tier N/day; paid higher) — today only anon has a limit
- [ ] Server-side rate limits on `/api/auth/*`, `/api/anon/diagnose`, `/api/chats/{id}/diagnose`
- [ ] Cap concurrent background AI jobs (queue + max parallelism); avoid unbounded `Task.Run`
- [ ] Fix OpenRouter referer/title for production (today `HTTP-Referer: http://localhost:5171`)

## Abuse-open APIs

- [ ] Restrict CORS (not `AllowAnyOrigin` + any method/header)  
  - Evidence: `LoginDB/Program.cs` CORS default policy
- [ ] Rate-limit login/register/Google (brute-force + spam)
- [ ] **Hard anon quota** that cannot be reset by omitting `X-Anon-Token`  
  - Evidence: `/api/anon/diagnose` mints a fresh subject when header missing (`Program.cs`)
  - Prefer IP + cookie + server counter / Redis; optional captcha after N tries
- [ ] Captcha or equivalent on register + anon diagnose after threshold
- [ ] Unify auth error messages to reduce email enumeration  
  - Evidence: distinct “no account” vs “incorrect password” in `AuthService.cs`

## Uploads: private, durable, bounded

- [ ] Stop serving user photos as anonymous static files under `wwwroot/uploads/**`  
  - Evidence: write path `Program.cs` + `UseStaticFiles()`; predictable `/uploads/{chatId}/...` URLs
- [ ] Serve images via **authenticated endpoint** or **signed object-storage URLs** (S3/R2/etc.)
- [ ] Move storage off ephemeral container disk (Railway disk dies on redeploy)
- [ ] Enforce max file size, max dimensions, and decode/validate real images (not only `ContentType.StartsWith("image/")`)
- [ ] Allowlist MIME/types (JPEG/PNG/WebP); reject SVG/GIF/HEIC unless intentionally supported
- [ ] Downscale / compress before base64 + AI call (cost + memory)
- [ ] **Untrack** committed sample uploads from git  
  - Evidence: `LoginDB/wwwroot/uploads/67|68|70|73|75/...` tracked
- [ ] Add `wwwroot/uploads/` (or equivalent) to `.gitignore`
- [ ] Stop `chmod 777` on uploads in `Dockerfile` if local disk remains for anything

## Session / token security (minimum bar)

- [ ] Check `IsActive` (and optionally role) on **every** authenticated request, not only at login  
  - Evidence: JWT validation only checks sig/exp + `sub`
- [ ] Shorten access-token lifetime; add refresh or re-login; plan for revocation on password change
- [ ] Prefer standard JWT library over hand-rolled HS256 (`JwtService.cs`)
- [ ] Do not leak `ex.Message` / raw upstream bodies to clients  
  - Evidence: anon 502 returns exception text; signed-in failures persist exception text into message content; `AiService` can include raw API bodies
- [ ] Revisit JWT-in-`localStorage` vs httpOnly cookie (XSS impact) — document decision

## DB / TLS hygiene

- [ ] Production Postgres: require TLS **and** validate server certificates  
  - Evidence: `Database.cs` uses `Trust Server Certificate=true` for `DATABASE_URL`
- [ ] Use `NpgsqlConnectionStringBuilder`; fix password-with-colon parsing risk on `UserInfo.Split(':')`
- [ ] Local `DB_*` path: document whether TLS is required

---

# P1 — Usable MVP product

## Product packaging / UX shell

- [ ] Real **landing page** (value prop, demo, how it works, pricing teaser, FAQ) — `/` is currently login-only
- [ ] Clear first-run onboarding after register (profile setup value + skip path)
- [ ] Signed-in **empty chat list** state (“Create your first diagnosis”) instead of blank sidebar
- [ ] Anonymous **usage meter** (“2 of 3 free diagnoses used”)
- [ ] Preserve or migrate guest conversation when user hits login wall  
  - Evidence: login-wall buttons call `localStorage.clear()` and drop anon history
- [ ] Confirm before destructive actions (delete chat, clear anon conversation)
- [ ] Feedback on pin/delete success/failure
- [ ] Retry / resend on failed AI messages (not red text only)
- [ ] Pending AI: user-visible timeout, cancel, or “still working…” after N seconds
- [ ] Consistent error/retry UI when chat list, profile card, or diagnose API fails
- [ ] Photo limit UX: block staging when remaining quota &lt; selected files (don’t fail only after submit)
- [ ] Show when extra dropped files are discarded (silent truncate today)
- [ ] Fullscreen / lightbox for uploaded photos (`cursor: zoom-in` without viewer)
- [ ] AI result actions: copy, share, export/print, thumbs up/down feedback
- [ ] Structured diagnosis presentation (diagnosis / why / steps / “start here”) not only weak markdown
- [ ] AI disclaimer in UI (“can be wrong; not manufacturer guidance”)
- [ ] Support contact (email or form)
- [ ] Optional: SK/EN i18n if targeting bilingual market (UI is hard-coded `lang="en"`)

## Auth & accounts lifecycle

- [ ] Email verification (at least for password accounts)
- [ ] Forgot / reset password
- [ ] Change password / change email
- [ ] Delete account + data export (GDPR baseline)
- [ ] Logout-all / session invalidation story
- [ ] Drive Google client ID from config/env consistently; lock OAuth origins to production domain  
  - Evidence: hardcoded `data-client_id` in `index.html` / `register.html`
- [ ] Explicit confirm when linking Google to an existing password account
- [ ] Stronger password policy than 6 chars (and real email validation beyond `Contains('@')`)
- [ ] Max lengths on name, email, password, profile fields, message text (avoid DB 500s)

## Payments / business (plan vs code)

Business model (school financials): freemium, ~€6/mo Pro, ~€0.02/diagnosis COGS. **None of this is in code.**

- [ ] Define free vs Pro entitlements (diagnoses/day, photo limits, history retention)
- [ ] Integrate Stripe (or equivalent) Checkout + Customer Portal
- [ ] Webhook-driven entitlement updates
- [ ] Usage metering stored server-side
- [ ] Enforce entitlements on diagnose endpoints
- [ ] Pricing page + upgrade CTAs in chat when quota hit
- [ ] Basic invoice/receipt emails (via Stripe)

## Legal / trust

- [ ] Privacy policy
- [ ] Terms of service
- [ ] Cookie / tracking notice (if any analytics later)
- [ ] AI / liability disclaimer
- [ ] Data retention policy (how long photos + chats live)
- [ ] Registration consent checkboxes where required
- [ ] Imprint / operator identity if taking EU payments
- [ ] Document third parties: OpenRouter, Google OAuth, host, email provider

## Data isolation / multi-user correctness

- [ ] Transactional photo-count enforcement (avoid concurrent exceed of 6-photo limit)
- [ ] Transactional chat create + auto-delete oldest non-pinned
- [ ] Transactional file write + `message_images` insert + photo_count update
- [ ] Ensure no future admin/`GetAll*` endpoints ship without authz  
  - Note: repos have `GetAllAsync` etc. unused by routes today; admin role seeded but unused
- [ ] Decide admin product surface or remove dead `admin` seed/role for now

---

# P2 — Reliability & ops

## Platform

- [ ] Versioned migrations (e.g. FluentMigrator / EF migrations / dbmate) instead of only boot-time SQL snippets  
  - Evidence: base tables still need manual `sql/01_*.sql` + `sql/02_create_tables.sql`; `message_images` / `status` / `google_id` added in `Program.cs` only
- [ ] Fresh deploy creates **all** tables automatically (or documented one-command migrate)
- [ ] Replace fire-and-forget `Task.Run` with hosted queue / background service + graceful shutdown  
  - Startup only marks `pending` → `failed` after 5 minutes
- [ ] Postgres backup + restore runbook tested once
- [ ] Health check deeper than process-up (DB ping; optional AI key present)
- [ ] Uptime monitor on public URL
- [ ] Error tracking (Sentry or similar)
- [ ] Structured logging (request id, user id hash, diagnose latency, model, token/cost estimates)
- [ ] CI: build + basic smoke tests on PR
- [ ] Automated tests: auth, ownership isolation, quota, upload validation
- [ ] Production `AllowedHosts` not `*`
- [ ] Document HTTPS/HSTS ownership (app vs Railway proxy)
- [ ] README at repo root (setup, env vars, run local, deploy)
- [ ] Update `Dokumentacia.txt` / docs — photos **are** stored on disk + `message_images` now (docs still claim otherwise in places)
- [ ] `.dockerignore` / image hygiene: no test credentials, no sample uploads, no `.claude` secrets if any

## Frontend robustness / mobile / a11y (MVP bar)

- [ ] Fix mobile `100vh` + virtual keyboard composer occlusion
- [ ] `env(safe-area-inset-*)` for notched phones
- [ ] Touch-discoverable chat pin/delete (not hover-only)
- [ ] Keyboard-operable chat rows and upload zones (not clickable divs only)
- [ ] Modal dialog semantics: `role="dialog"`, focus trap, restore focus
- [ ] `aria-live` for errors, typing, completed AI replies
- [ ] Focus-visible styles (inputs currently weak outline)
- [ ] Contrast pass on gray/orange text (several pairs below WCAG AA)
- [ ] Escape closes mobile sidebar
- [ ] Prefer buttons/links with `href` on login wall (anchors without href today)

---

# P3 — AI quality & differentiation

- [ ] Build labeled eval set (~50–100 real failure photos: stringing, warping, under-extrusion, layer shift, etc.)
- [ ] Track quality metrics before/after prompt or model changes
- [ ] Structured model output (defect type, confidence, slicer setting diffs) + UI binding
- [ ] Printer / filament / slicer catalogs (dropdowns) instead of free text only
- [ ] Multi-angle guidance (“take photo from X”)
- [ ] Before/after comparison support
- [ ] Stronger jailbreak / off-topic resistance beyond system prompt text
- [ ] Cost controls: max tokens, history token budget, image token budget
- [ ] Model fallback chain when primary fails or 429s
- [ ] Optional: community feedback loop to improve diagnoses

---

# Nice-to-have / later

- [ ] PWA installability
- [ ] Team / org accounts
- [ ] Admin console (users, abuse, cost dashboard) — only if role is real
- [ ] OEM / API product surface (mentioned in school materials)
- [ ] Status page
- [ ] Marketing site CMS
- [ ] Full design system / component framework (currently monolithic HTML pages)

---

# Critical findings index (evidence)

Quick reference for the most dangerous configs/bugs:

| # | Finding | Where |
|---|---------|--------|
| 1 | Test accounts seeded every startup | `LoginDB/Services/DatabaseSeeder.cs`, `Program.cs` startup |
| 2 | Test credentials file in repo | `LoginDB/testing-credentials.txt` |
| 3 | SQL seed with plaintext passwords | `LoginDB/sql/02_seed_users.sql` |
| 4 | Uploads publicly readable via static files | `Program.cs` `UseStaticFiles` + `wwwroot/uploads` |
| 5 | Sample user images committed | `LoginDB/wwwroot/uploads/**` tracked in git |
| 6 | Anon quota bypass by omitting token | `POST /api/anon/diagnose` |
| 7 | No rate limits / size limits | diagnose + auth endpoints |
| 8 | CORS allow-all | `Program.cs` |
| 9 | Free AI models + no signed-in quotas | `AiService.cs` |
| 10 | Unbounded memory base64 of uploads | diagnose handlers |
| 11 | Fire-and-forget AI tasks | `Program.cs` `Task.Run` |
| 12 | JWT 30d, no revocation; inactive users still valid | `JwtService.cs` |
| 13 | Error detail leakage to clients | anon catch; failed message content; AiService |
| 14 | DB `Trust Server Certificate=true` | `Database.cs` |
| 15 | Live deployment missing | Railway 404 |
| 16 | No payments despite freemium plan | entire codebase |
| 17 | No privacy/ToS/disclaimer | frontend pages |
| 18 | Docs outdated on photo storage | `LoginDB/Dokumentacia.txt` |
| 19 | Uploads not in `.gitignore` | root `.gitignore` |
| 20 | Guest history wiped at login wall | `chat.html` `localStorage.clear()` |

---

# Suggested milestone checklist

## Milestone A — “Safe public demo”
- [ ] Live URL up
- [ ] No prod test users / no credentials in git
- [ ] Private durable images
- [ ] Rate limits + non-bypassable free tier
- [ ] Paid AI model or hard global budget kill-switch
- [ ] Privacy + ToS + AI disclaimer + support email
- [ ] Landing page

## Milestone B — “Real MVP users”
- [ ] Password reset + delete account
- [ ] Quotas + Stripe Pro
- [ ] Monitoring + backups
- [ ] Upload validation + mobile/a11y fixes for core path
- [ ] Retry + empty states + usage meters

## Milestone C — “Trust the AI”
- [ ] Eval set + quality bar
- [ ] Structured diagnoses
- [ ] Catalogs for printer/filament/slicer
- [ ] Cost/latency dashboards

---

# Notes

- School business docs (financials, GTM, pitch) live outside this repo (vault / course materials). Product code does not implement the freemium model yet.
- This backlog was produced from static code audit + live URL check. Full runtime e2e was not run in the audit environment (`dotnet` SDK not available there).
- Clone used during audit: local path may be `/opt/data/projects/PrintScan_AI` on the agent host — not necessarily the user’s machine.

---

*End of ToDos — keep this file updated as items ship.*
