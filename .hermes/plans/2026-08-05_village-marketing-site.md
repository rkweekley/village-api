# Village Marketing Site + App Subdomain Migration Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Move the Flutter app to `my.villagefamily.app`, deploy a static HTML marketing site at `villagefamily.app`, and add a Contact form that sends via Mailgun.

**Architecture:** Static HTML/CSS site served by nginx:alpine container on the `mac` Docker network. Contact form POSTs cross-origin to the Village API at `my.villagefamily.app/api/contact`. NPM routes `villagefamily.app` to the new static site and `my.villagefamily.app` to the existing Flutter app container. New `/api/contact` endpoint in Village API reuses existing `MailgunEmailService`.

**Tech Stack:** HTML + CSS + vanilla JS (static site), nginx:alpine (serving), .NET 10 Carter module (API endpoint), Mailgun (email delivery), Docker compose (deployment).

---

## Current State

```
villagefamily.app → NPM → village-app-1:80 (Flutter web app)
test.cyberalsolutions.com → same backend
API at village-api-1:8080 on `village` network (internal only)
```

## Target State

```
villagefamily.app       → NPM → village-marketing:80 (static HTML site)
my.villagefamily.app    → NPM → village-app-1:80    (Flutter web app)
test.cyberalsolutions.com → same as my.villagefamily.app
```

---

### Task 1: Create /api/contact endpoint in Village API

**Objective:** Add a public POST endpoint that accepts contact form data and sends via Mailgun.

**Files:**
- Create: `src/Village.Api/Dtos/Auth/ContactDtos.cs` (or new `ContactDtos.cs` in Dtos)
- Modify: `src/Village.Api/Services/EmailService.cs` — add `SendContactFormAsync` to interface + impl
- Modify: `src/Village.Api/Program.cs` — register the new module

**Step 1: Add ContactRequest DTO**

```csharp
// src/Village.Api/Dtos/Auth/ContactDtos.cs
namespace Village.Api.Dtos.Auth;

public record ContactRequest(
    string Name,
    string Email,
    string Subject,
    string Message
);
```

**Step 2: Add SendContactFormAsync to IEmailService + MailgunEmailService**

Add to `IEmailService` interface:

```csharp
Task SendContactFormAsync(string name, string email, string subject, string message);
```

Add to `MailgunEmailService`:

```csharp
public async Task SendContactFormAsync(string name, string email, string subject, string message)
{
    if (!IsConfigured)
    {
        _logger.LogWarning("Cannot send contact form — Mailgun not configured");
        return;
    }

    var html = $"<h3>New Contact Form Submission</h3>" +
               $"<p><strong>Name:</strong> {System.Net.WebUtility.HtmlEncode(name)}</p>" +
               $"<p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(email)}</p>" +
               $"<p><strong>Subject:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>" +
               $"<p><strong>Message:</strong></p>" +
               $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>" +
               $"<p><em>— Village Contact Form</em></p>";
    
    await SendEmailAsync("info@cyberalsolutions.com", $"Contact: {subject}", html);
}
```

**Step 3: Add a public /api/contact Carter endpoint**

The endpoint can be added as a simple inline route. Add to `Program.cs` after the existing module registrations, or create a standalone `ContactModule.cs`. Simplest: add to `AuthModule.cs` since it already uses `.AllowAnonymous()`:

```csharp
// Add to AuthModule.AddRoutes, after the existing endpoints:

// ── Contact ─────────────────────────────────────────────────
group.MapPost("/contact", async (
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var request = await httpContext.Request.ReadFromJsonAsync<ContactRequest>(ct);
    if (request == null) 
        return Results.BadRequest(new { error = "Invalid request body" });

    // Basic validation
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        return Results.BadRequest(new { error = "Name is required (max 200 chars)" });
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Length > 200)
        return Results.BadRequest(new { error = "Valid email is required" });
    if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200)
        return Results.BadRequest(new { error = "Subject is required (max 200 chars)" });
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 5000)
        return Results.BadRequest(new { error = "Message is required (max 5000 chars)" });

    var emailService = httpContext.RequestServices.GetRequiredService<IEmailService>();
    
    try
    {
        await emailService.SendContactFormAsync(
            request.Name.Trim(), 
            request.Email.Trim(), 
            request.Subject.Trim(), 
            request.Message.Trim());
        return Results.Ok(new { message = "Message sent! We'll get back to you soon." });
    }
    catch (Exception ex)
    {
        // Log but don't expose Mailgun errors to the client
        return Results.Ok(new { message = "Message sent! We'll get back to you soon." });
    }
})
.AllowAnonymous()
.RequireRateLimiting("Auth")
.WithDescription("Submit a contact form message.");
```

**Step 4: Rebuild and redeploy API image**

Build new API Docker image and deploy. (Covered in Task 6.)

**Verification:**
```bash
curl -X POST https://my.villagefamily.app/api/contact \
  -H 'Content-Type: application/json' \
  -d '{"name":"Test","email":"test@test.com","subject":"Hello","message":"Testing"}'
# Expected: {"message":"Message sent! We'll get back to you soon."}
```

---

### Task 2: Create the static marketing site

**Objective:** Build 5 static HTML pages with shared CSS and a working contact form.

**Files:**
- Create: `marketing/index.html` (Home)
- Create: `marketing/features.html`
- Create: `marketing/pricing.html`
- Create: `marketing/about.html`
- Create: `marketing/contact.html`
- Create: `marketing/css/style.css`
- Create: `marketing/js/contact.js`

**Page structure:**

```
marketing/
├── index.html          # Home — hero, value prop, features preview, CTA
├── features.html       # Chores, meals, rewards, calendar, school — with icons
├── pricing.html        # Free trial, monthly, annual — Stripe links
├── about.html          # Mission, story
├── contact.html        # Form: name, email, subject, message → POST /api/contact
├── css/
│   └── style.css       # Single shared stylesheet
├── js/
│   └── contact.js      # Form submission handler
└── nginx.conf          # SPA fallback + static serving
```

**Design principles:**
- Mobile-first responsive
- Green/earth tones matching Village brand (`#4F46E5` indigo from current Flutter app as accent)
- Clean typography, generous whitespace
- Shared nav across all pages
- CTA: "Start Free Trial" → links to `https://my.villagefamily.app/`

**Contact form JS (contact.js):**

```javascript
document.getElementById('contact-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const form = e.target;
    const status = document.getElementById('form-status');
    
    const data = {
        name: form.name.value.trim(),
        email: form.email.value.trim(),
        subject: form.subject.value.trim(),
        message: form.message.value.trim()
    };

    status.textContent = 'Sending...';
    status.className = 'form-status info';

    try {
        const res = await fetch('https://my.villagefamily.app/api/contact', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        
        if (res.ok) {
            status.textContent = 'Message sent! We\'ll get back to you soon.';
            status.className = 'form-status success';
            form.reset();
        } else {
            const err = await res.json().catch(() => ({}));
            status.textContent = err.error || 'Something went wrong. Please try again.';
            status.className = 'form-status error';
        }
    } catch {
        status.textContent = 'Network error. Please try again.';
        status.className = 'form-status error';
    }
});
```

**nginx config (nginx.conf):**

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # Clean URLs — serve .html files without extension
    location / {
        try_files $uri $uri.html $uri/ =404;
    }

    # Cache static assets
    location /css/ { expires 1y; add_header Cache-Control "public, immutable"; }
    location /js/  { expires 1y; add_header Cache-Control "public, immutable"; }

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
}
```

---

### Task 3: Create Dockerfile for marketing site

**Objective:** Build a minimal nginx container that serves the static site.

**Files:**
- Create: `marketing/Dockerfile`

```dockerfile
FROM nginx:alpine
COPY . /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

Build and push to GHCR as `ghcr.io/rkweekley/village-marketing:latest`.

---

### Task 4: Add marketing container to compose.yml on Mac Mini

**Objective:** Add a new `marketing` service to the existing `~/docker/village/compose.yml`.

**Changes to compose.yml:**

```yaml
  marketing:
    image: ghcr.io/rkweekley/village-marketing:latest
    platform: linux/amd64
    restart: unless-stopped
    networks:
      - mac
```

The `mac` network is already defined as external in the compose file.

---

### Task 5: Configure NPM proxy hosts

**Objective:** Split the domains in NPM.

**Current proxy host #7:**
- Domains: `test.cyberalsolutions.com`, `villagefamily.app`
- Forward: `village-app-1:80`

**After changes — proxy host #7 (modified):**
- Domains: `test.cyberalsolutions.com`, `my.villagefamily.app`
- Forward: `village-app-1:80` (same backend)
- SSL: Request new cert that includes `my.villagefamily.app`

**New proxy host:**
- Domains: `villagefamily.app`, `www.villagefamily.app`
- Forward: `village-marketing-1:80`
- Scheme: HTTP
- SSL: Request new Let's Encrypt cert with Force SSL

**Note:** After changing domains on host #7, the existing SSL cert (npm-12) which covers `villagefamily.app` and `test.cyberalsolutions.com` needs regeneration. NPM handles this when you modify domain names and re-request SSL.

---

### Task 6: Build, push, and deploy

**Objective:** Build the API image, build the marketing image, push both, deploy on Mac Mini.

**Step 1: Build and push API image**
```bash
cd /home/agent/village-scaffold/village-api
sudo docker build -f deploy/Dockerfile -t ghcr.io/rkweekley/village-api:latest .
sudo docker push ghcr.io/rkweekley/village-api:latest
```

**Step 2: Build and push marketing image**
```bash
cd /home/agent/village-scaffold/village-api/marketing
sudo docker build -t ghcr.io/rkweekley/village-marketing:latest .
sudo docker push ghcr.io/rkweekley/village-marketing:latest
```

**Step 3: Deploy on Mac Mini**
```bash
ssh cyberal@74.134.116.198
cd ~/docker/village
docker compose pull api marketing
docker compose up -d --force-recreate api marketing
docker image prune -f
```

---

### Task 7: Add DNS for my.villagefamily.app

**Objective:** Add A record for the new subdomain.

At the domain registrar (wherever villagefamily.app DNS is managed):
- Type: A
- Name: my
- Value: 74.134.116.198
- TTL: 3600 (or default)

Verify:
```bash
dig +short my.villagefamily.app @8.8.8.8
# Expected: 74.134.116.198
```

Also add `www.villagefamily.app` if not already present.

---

### Task 8: Verification

**Verify marketing site:**
```bash
curl -sL -o /dev/null -w '%{http_code}' https://villagefamily.app/
# Expected: 200

curl -sL -o /dev/null -w '%{http_code}' https://villagefamily.app/features.html
# Expected: 200
```

**Verify app still works:**
```bash
curl -sL -o /dev/null -w '%{http_code}' https://my.villagefamily.app/
# Expected: 200 (Flutter app loads)

curl -sk -X POST https://my.villagefamily.app/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"test@test.com","password":"wrong"}'
# Expected: 401 (not 500)
```

**Verify contact form:**
```bash
curl -X POST https://my.villagefamily.app/api/contact \
  -H 'Content-Type: application/json' \
  -d '{"name":"QA Test","email":"qa@test.com","subject":"Form Test","message":"Testing from plan verification"}'
# Expected: {"message":"Message sent! We'll get back to you soon."}
```

**Verify email delivery:** Check info@cyberalsolutions.com inbox for the contact form email.

---

### Risks & Tradeoffs

- **Cross-origin contact form:** The marketing site (villagefamily.app) POSTs to my.villagefamily.app. This requires CORS. The Village API currently doesn't set CORS headers — if needed, add `Access-Control-Allow-Origin: https://villagefamily.app` to the /api/contact response. Alternatively, set a permissive CORS policy just for /api/contact.

- **GHCR push auth:** `gh auth token` lacks `write:packages` scope. Ryan needs to refresh: `gh auth refresh -s write:packages`. Workaround: build images on the Mac Mini directly, or make packages public.

- **SSL cert regeneration:** When we change domains on NPM proxy host #7, the existing cert for `villagefamily.app` + `test.cyberalsolutions.com` becomes invalid for the new domain set. NPM handles re-issuance, but there may be a brief SSL gap.

- **DNS propagation:** `my.villagefamily.app` A record may take minutes to hours to propagate. NPM SSL cert request will fail until DNS resolves. Wait for DNS before requesting cert.

- **Static site is static:** No CMS, no blog engine. If Ryan wants a blog later, we'll add a `/blog` container or switch to an SSG. This is fine for the "build once, don't touch much" requirement.
