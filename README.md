# WhatsApp Business Inbox (ASP.NET Core 8 MVC)

A multi-tenant WhatsApp Cloud API inbox: live chat, contacts, templates,
bulk campaigns, post-call feedback and role-based administration.

## Features

| Area | Where | Notes |
|------|-------|-------|
| Webhook (GET verify / POST receive) | `Controllers/WebhookController.cs` | Reads the raw body and verifies `X-Hub-Signature-256` (HMAC-SHA256) per tenant |
| WhatsApp service | `Services/WhatsAppService.cs` | Send text / template / interactive buttons; receive text, image, document, audio, video, button & list replies; statuses |
| Inbox | `Controllers/ChatController.cs`, `Views/Chat/*` | WhatsApp-style two-pane UI with AJAX polling every 3s |
| Contacts CRUD + CSV import | `Controllers/ContactsController.cs` | Header-aware, de-duplicating import |
| Templates | `Controllers/TemplatesController.cs` | Sync from Meta Graph API + create/delete |
| Campaigns | `Controllers/CampaignsController.cs`, `Services/CampaignBackgroundService.cs` | Bulk template sends via an in-memory `Channel` + `BackgroundService` |
| Feedback | `Controllers/FeedbacksController.cs` | Sends Yes/No quick-reply buttons; replies applied by the webhook |
| Multi-tenancy | `Services/TenantProvider.cs`, `Models/Tenant*.cs` | `TenantId` scoping; per-tenant credentials encrypted with Data Protection |
| Rate limiting | `Program.cs` | Fixed-window policy `whatsapp-send` on outbound endpoints |
| Admin dashboard | `Controllers/AdminController.cs` | `[Authorize(Roles="Admin")]` stats, tenants, credentials, users |
| Auth | `Controllers/AccountController.cs` | Custom MVC login/logout; roles `Admin` / `Agent` |

## Getting started

1. Set the SQL Server connection string in `appsettings.json` (`ConnectionStrings:DefaultConnection`).
2. Run the app — migrations are applied and roles/admin are seeded automatically at startup:
   ```powershell
   dotnet run --project WhatsAppBusinessInbox
   ```
3. Log in with the seeded administrator:
   - **Email:** `admin@example.com`
   - **Password:** `Admin@123`
   (change via the `Seed:AdminEmail` / `Seed:AdminPassword` configuration keys)

## Configuring WhatsApp

Two options:

1. **Single tenant (config):** fill in the `WhatsApp` section of `appsettings.json`
   (`Token`, `PhoneNumberId`, `BusinessAccountId`, `VerifyToken`, `AppSecret`, `ApiVersion`).
2. **Per tenant (recommended):** sign in as an Admin → **Admin → Credentials**, then
   enter the phone number id, business account id, verify token, access token and app
   secret. Secrets are encrypted at rest with the Data Protection API (keys stored in
   `keys/`).

Webhook URL to register with Meta:

```
https://<your-host>/Webhook
```

The GET handshake compares `hub.verify_token` against the configured value or any
tenant's stored verify token. POST requests are rejected with `401` unless the
signature matches the tenant's app secret.

## Notes

- Outbound sends to Meta are rate limited (`RateLimit:PerMinute`, default 60/min per user).
- The campaign worker is in-memory: campaigns left `running` when the process stops are
  not automatically resumed.
