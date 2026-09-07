# ClinicFlow SaaS Design System & UI Architecture Standard

> **Authority:** This document is the single source of truth for all UI decisions in this project.
> Every Razor view in `Views/` must comply with these standards. No exceptions.

---

## 1. Color Palette Tokens & CSS Variables

All colors are centrally declared in `wwwroot/css/site.css` under the `:root` block
prefixed with `--clinic-*`. **Never use arbitrary hex codes directly in views.**

| Purpose | Hex | CSS Variable |
|---|---|---|
| Page Background | `#F6F7F5` | `var(--clinic-bg)` |
| Surface / Cards | `#FFFFFF` | `var(--clinic-surface)` |
| Primary Brand & Active State | `#49665A` | `var(--clinic-primary)` |
| Button Hover State | `#3D574D` | `var(--clinic-primary-hover)` |
| Dark Text / Primary Headings | `#202725` | `var(--clinic-text)` |
| Muted Text / Subtitles | `#727A76` | `var(--clinic-muted)` |
| Borders & Dividers | `#DDE3DF` | `var(--clinic-border)` |

### Semantic Status Badge Colors

| Semantic | Background | Text |
|---|---|---|
| Emerald - Completed / Active / Paid | `#EAF5F0` | `#2E7D5B` |
| Amber - Pending / Partial | `#FDF6EC` | `#B47318` |
| Rose - Cancelled / Overdue / Delete | `#FCEEEE` | `#B83A38` |

Use the pre-built `.clinic-badge-*` classes -- do NOT inline these colors.

---

## 2. Standard Component Classes

These classes are registered globally in `wwwroot/css/site.css`.
Use them consistently across all Razor views.

### Buttons

| Class | Usage |
|---|---|
| `btn-clinic-primary` | Primary CTA - Save, Create, Submit |
| `btn-clinic-secondary` | Ghost / cancel actions |
| `btn-clinic-danger` | Destructive - Delete, Remove |

### Cards

`.clinic-card` -> `background-color: var(--clinic-surface); border: 1px solid var(--clinic-border); border-radius: 0.75rem; box-shadow: 0 1px 2px rgba(0,0,0,0.03);`

### Form Controls

`.clinic-input` -> `background-color: #FFFFFF; border: 1px solid var(--clinic-border); border-radius: 0.5rem; padding: 0.5rem 0.75rem; font-size: 0.875rem; color: var(--clinic-text); outline: none; width: 100%;`
Focus ring: `border-color: var(--clinic-primary); box-shadow: 0 0 0 3px rgba(73,102,90,0.12);`

### Status Badges

```html
<span class="clinic-badge clinic-badge-emerald">Completed</span>
<span class="clinic-badge clinic-badge-amber">Pending</span>
<span class="clinic-badge clinic-badge-rose">Cancelled</span>
```

### Tables

- **Header:** `bg-[#F6F7F5] text-[#727A76] text-xs font-semibold uppercase tracking-wider border-b border-[#DDE3DF] px-4 py-3 text-right`
- **Body rows:** `border-b border-[#DDE3DF] hover:bg-[#F6F7F5]/50 text-sm text-[#202725] px-4 py-3`

---

## 3. Layout Architecture

```
cf-layout
  cf-sidebar       (fixed, 240px, white, border-e)
  cf-main          (margin-start: 240px)
    cf-topbar      (sticky, 64px, white, border-b)
    page-content   (padding: 24px, bg: var(--clinic-bg))
```

---

## 4. Tab Navigation Standard

All tabbed pages must use plain JS tab switching (no Bootstrap Tab.show(), no page jumps).

```javascript
function switchTab(targetId) {
  document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('show','active'));
  document.querySelectorAll('[data-bs-target]').forEach(b => b.classList.remove('active'));
  var pane = document.querySelector(targetId);
  if (pane) pane.classList.add('show','active');
  var btn  = document.querySelector('[data-bs-target="' + targetId + '"]');
  if (btn)  btn.classList.add('active');
  history.replaceState(null, null, targetId);
}
document.addEventListener('DOMContentLoaded', function () {
  document.querySelectorAll('[data-bs-toggle="tab"]').forEach(function (btn) {
    btn.addEventListener('click', function (e) {
      e.preventDefault();
      switchTab(this.getAttribute('data-bs-target'));
    });
  });
  var hash = window.location.hash;
  if (hash && document.querySelector('[data-bs-target="' + hash + '"]')) switchTab(hash);
});
```

---

## 5. Strict Development Rules

1. **Never break Razor bindings.** All `@model`, `asp-for`, `asp-action`, `asp-controller`, and `@Html.AntiForgeryToken()` must remain intact.
2. **Use CSS variables only.** Reference `--clinic-*` tokens; raw hex in views is forbidden.
3. **Wrap all external calls** in try/catch with fallback UI state.
4. **Build after every change.** Run `dotnet build` — bar is 0 errors.
5. **Bilingual labels.** Use `@T("arabic", "english")` for every user-visible string.
6. **Admin-only gates.** User/role mutations require `[Authorize(Roles = "Admin")]`.

---

## 6. File Ownership Map

| File | Purpose |
|---|---|
| `wwwroot/css/site.css` | Single source for CSS variables, dark theme tokens, and global component classes |
| `Views/Shared/_Layout.cshtml` | Master shell - topbar, theme toggle switcher, anti-flicker script, sidebar |
| `Views/Settings/Index.cshtml` | Settings dashboard hub |
| `Views/Settings/General.cshtml` | Dedicated clinic profile settings |
| `Views/Settings/Hours.cshtml` | Dedicated working hours settings |
| `Views/Settings/Automation.cshtml` | Dedicated WhatsApp & n8n settings |
| `Views/Settings/Security.cshtml` | Dedicated backups & password security |
| `Views/Account/StaffList.cshtml` | Dedicated staff management table |
| `Views/Account/Register.cshtml` | Admin-only new staff form |
| `Controllers/AccountController.cs` | Register + StaffList + DeleteUser actions |
| `Controllers/SettingsController.cs` | Settings sub-views GET + POST actions |

---

## 7. Dark Mode Architecture & Standards

All dark mode adjustments are driven by CSS variables under `[data-theme="dark"]` with zero per-element hardcoding:

### Dark Theme Tokens:
```css
[data-theme="dark"] {
    --clinic-bg:            #121816;
    --clinic-surface:       #1B2421;
    --clinic-primary:       #5E8273;
    --clinic-primary-hover: #4E6E60;
    --clinic-text:          #EAEFEA;
    --clinic-muted:         #9CA8A3;
    --clinic-border:        #2B3833;

    /* Semantic Badges */
    --clinic-emerald-bg:    rgba(46, 125, 91, 0.2);
    --clinic-emerald-text:  #4ade80;
    --clinic-amber-bg:      rgba(180, 115, 24, 0.2);
    --clinic-amber-text:    #fbbf24;
    --clinic-rose-bg:       rgba(184, 58, 56, 0.2);
    --clinic-rose-text:     #f87171;
}
```

### Persistence & Anti-Flicker:
- Theme choice is saved in `localStorage.setItem('clinic_theme', 'dark' | 'light')`.
- Anti-flicker inline script in `<head>` executes before initial layout rendering.
- Topbar includes the `#themeToggleBtn` interactive switcher with automatic Sun/Moon icon toggle.

---

*Last updated: 2026-08-31 -- ClinicFlow v1.0 Design System*

