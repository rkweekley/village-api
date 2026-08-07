# Village Marketing Site — Mobile Responsiveness Plan

> **For Hermes:** Apply these changes directly with patch/write_file, rebuild, deploy.

**Goal:** Make all 5 marketing pages fully usable on phones (320px–428px wide). No horizontal scroll, no cut-off buttons, readable text, touch-friendly navigation.

**Architecture:** CSS-only hamburger menu (no JS needed for toggle — CSS `:target` + hidden checkbox), responsive grid tweaks, stacked layout at small breakpoints. Single shared `style.css` change affects all pages.

**Tech Stack:** HTML + CSS (no frameworks)

---

## Current Problems

| Issue | Where | Cause |
|---|---|---|
| Sign In button cut off | Nav on phones | `nav-links` is `display: flex` with no wrapping, 6 items overflow 320px |
| No hamburger menu | Nav | No mobile nav toggle exists |
| Cards overflow on small phones | Features/pricing grids | `minmax(280px, 1fr)` minimum is wider than 320px phone |
| Hero text too large | Homepage on phone | `font-size: 2rem` at mobile is OK but could be better at ~320px |
| Page header too large | Features/Pricing/etc | `font-size: 2.25rem` — fine but `1.75rem` at small phones |
| No touch-friendly tap targets | Nav links | Links are standard size but could use more padding on mobile |

---

### Task 1: Add hamburger menu to the nav (all 5 pages, shared CSS)

**Objective:** Replace the horizontal nav with a hamburger toggle on mobile.

**Files:**
- Modify: `marketing/css/style.css`
- Modify: `marketing/index.html`
- Modify: `marketing/features.html`
- Modify: `marketing/pricing.html`
- Modify: `marketing/about.html`
- Modify: `marketing/contact.html`

The hamburger uses a CSS-only trick: a hidden `<input type="checkbox">` that controls a `.nav-links` visibility via the `:checked` sibling selector. No JavaScript needed.

**Step 1: Update the nav HTML in ALL 5 pages**

Replace the nav block with this:

```html
  <nav>
    <div class="container">
      <a href="/" class="logo"><img src="/images/logo-nav.png" alt="Village" height="36"></a>
      <input type="checkbox" id="nav-toggle" class="nav-toggle">
      <label for="nav-toggle" class="nav-toggle-label">
        <span></span>
        <span></span>
        <span></span>
      </label>
      <ul class="nav-links">
        <li><a href="/features">Features</a></li>
        <li><a href="/pricing">Pricing</a></li>
        <li><a href="/about">About</a></li>
        <li><a href="/contact">Contact</a></li>
        <li><a href="https://my.villagefamily.app" class="cta-nav">Sign In</a></li>
      </ul>
    </div>
  </nav>
```

**Step 2: Add hamburger CSS**

Replace the entire nav section and mobile media query in `style.css`:

```css
/* ── Nav ── */
nav {
  border-bottom: 1px solid var(--border);
  background: var(--bg-elevated);
  position: sticky; top: 0; z-index: 100;
}
nav .container {
  display: flex; align-items: center; justify-content: space-between;
  height: 64px; flex-wrap: wrap;
}
.logo {
  display: flex; align-items: center; text-decoration: none;
  z-index: 101;
}
.logo img { display: block; }

/* Hamburger toggle — hidden on desktop, visible on mobile */
.nav-toggle { display: none; }
.nav-toggle-label {
  display: none;
  flex-direction: column; gap: 5px; cursor: pointer;
  padding: 8px; z-index: 101;
}
.nav-toggle-label span {
  display: block; width: 24px; height: 2px;
  background: var(--text); border-radius: 2px;
  transition: all 0.2s;
}

.nav-links {
  display: flex; gap: 32px; list-style: none; align-items: center;
}
.nav-links a {
  text-decoration: none; color: var(--text-muted); font-weight: 500;
  font-size: 0.95rem; transition: color 0.15s;
}
.nav-links a:hover { color: var(--primary); }
.nav-links a.cta-nav {
  background: var(--primary); color: white; padding: 10px 22px;
  border-radius: var(--radius-btn); font-weight: 600; font-size: 0.9rem;
  letter-spacing: -0.1px;
}
.nav-links a.cta-nav:hover { background: var(--primary-dark); color: white; }

/* ── Mobile ── */
@media (max-width: 768px) {
  .nav-toggle-label { display: flex; }

  .nav-links {
    display: none;
    flex-direction: column;
    position: absolute; top: 64px; left: 0; right: 0;
    background: var(--bg-elevated);
    border-bottom: 1px solid var(--border);
    padding: 16px 24px 24px;
    gap: 12px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.06);
  }
  .nav-links a { font-size: 1rem; padding: 8px 0; }
  .nav-links a.cta-nav {
    display: block; text-align: center; margin-top: 4px;
    padding: 12px 24px;
  }

  /* Show nav when checkbox is checked */
  .nav-toggle:checked ~ .nav-links { display: flex; }

  /* Animate hamburger to X */
  .nav-toggle:checked ~ .nav-toggle-label span:nth-child(1) {
    transform: rotate(45deg) translate(5px, 5px);
  }
  .nav-toggle:checked ~ .nav-toggle-label span:nth-child(2) {
    opacity: 0;
  }
  .nav-toggle:checked ~ .nav-toggle-label span:nth-child(3) {
    transform: rotate(-45deg) translate(5px, -5px);
  }

  /* ── General mobile sizing ── */
  .hero { padding: 64px 0 48px; }
  .hero h1 { font-size: 1.75rem; }
  .hero p { font-size: 0.95rem; }
  section { padding: 56px 0; }

  .section-title h2 { font-size: 1.5rem; }
  .section-title p { font-size: 0.95rem; }
  .page-header { padding: 40px 0 28px; }
  .page-header h1 { font-size: 1.75rem; }
  .page-header p { font-size: 0.95rem; }

  /* Tighter grids for phones */
  .feature-grid { grid-template-columns: 1fr; gap: 16px; }
  .pricing-grid { grid-template-columns: 1fr; gap: 20px; }

  /* Smaller cards */
  .feature-card { padding: 24px; }
  .pricing-card { padding: 32px 24px; }
  .pricing-card .price { font-size: 2rem; }

  /* Contact form full-width */
  .contact-form { padding: 0; }

  /* Footer */
  footer { padding: 32px 0; }
}

/* ── Very small phones (320-374px) ── */
@media (max-width: 374px) {
  .hero h1 { font-size: 1.5rem; }
  .page-header h1 { font-size: 1.5rem; }
  .btn { padding: 12px 20px; font-size: 0.9rem; }
  .hero .cta-group { flex-direction: column; align-items: center; }
}
```

**Verification:**
- Open https://villagefamily.app on a phone (or Chrome DevTools mobile view at 375px)
- Hamburger icon appears, nav links hidden
- Tap hamburger → nav links slide down
- Tap hamburger again → nav closes
- Sign In button is full-width inside the dropdown, not cut off
- No horizontal scroll on any page

---

### Task 2: Verify and fix grid card minimum widths

**Objective:** Ensure feature and pricing cards don't overflow on 320px-wide phones.

**Files:**
- Modify: `marketing/css/style.css`

The current grid uses `minmax(280px, 1fr)` which is wider than 320px. Already fixed in Task 1 by overriding to `1fr` at mobile. The desktop grid stays at `minmax(280px, 1fr)`.

No separate changes needed beyond Task 1's media queries.

---

### Task 3: Add viewport meta tag if missing

**Objective:** Ensure all pages have `<meta name="viewport" content="width=device-width, initial-scale=1.0">`.

Check all 5 pages. The index already has it. Verify the other 4 also have it. If missing, add it.

**Verification:**
```bash
grep -l 'viewport' marketing/*.html | wc -l
# Expected: 5
```

---

### Task 4: Rebuild and deploy

**Objective:** Build marketing image and deploy.

```bash
sudo docker build -t ghcr.io/rkweekley/village-marketing:latest marketing/
sudo docker push ghcr.io/rkweekley/village-marketing:latest
ssh cyberal@74.134.116.198 "cd ~/docker/village && docker compose pull marketing && docker compose up -d --force-recreate marketing"
```

**Verification:**
- Open Chrome DevTools, set to iPhone SE (375px)
- Navigate all 5 pages
- Confirm: no horizontal scroll, hamburger works, Sign In not cut off, text readable

---

## Risks

- **No JS hamburger means no auto-close on nav link click.** The menu stays open until the user taps the hamburger again. Acceptable for a 5-page site. If this becomes annoying, add a tiny JS snippet.
- **Logo size:** 36px height logo image may look small on phones. Acceptable — it matches the brand proportionally.
