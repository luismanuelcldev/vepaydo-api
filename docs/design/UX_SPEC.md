# VepayDO — UX Specification

**Status:** authoritative for visual and interaction decisions. `AGENTS.md` section 12 defines behavior and scope; this file defines how it looks, feels, and reads. If they conflict, **this file wins for UI and copy; `AGENTS.md` wins for business rules and security.**
**Language:** this document is in English. User-facing copy is Spanish (es-DO) and lives in i18n resources; every string below has an i18n key in `snake.case`.
**Positioning rule (copy and visuals):** VepayDO **alerts, it does not certify.** Never use "verified by VepayDO", "fraud", "fake", or "guaranteed". Say what the system noticed and let the person decide.

---

## 1. Users, context, and goals

| User | Context | What they need |
|---|---|---|
| **Cashier / Verifier** | Android phone, one hand, standing, interruptions, patchy connectivity, receipts arriving through WhatsApp all day | Clear the queue fast, avoid mistakes, never lose a receipt |
| **Owner** | Phone during the day, desktop or laptop at the end of the day, accountability-focused | See what needs attention, trust the numbers, export the daily reconciliation |

**UX goals (initial targets, hypotheses to validate in the pilot):**

1. A strong-match receipt is resolved in **two taps** after it appears (open → confirm), or **one tap** with bulk confirm.
2. Median time from opening a receipt to a decision **≤ 10 seconds** for strong matches.
3. A first-time cashier completes upload → review → confirm **without help** (see section 16).
4. No receipt is ever lost: every upload has a visible state until it is stored on the server.
5. A person can always tell **why** a receipt is in its state (plain-language reason).

---

## 2. Brand and visual identity

**Name and casing:** `VePayDO` in the logo lockup and marketing; `VepayDO` is acceptable in running text. Keep the brand name in one constant (`BRAND_NAME`); the final spelling is pending name clearance.
**Tagline (draft):** "Verifica tus comprobantes digitales".
**Logo concept:** a ribbon-style **V** in blue, a document with text lines, and a circular **check badge** at the document's lower right. Wordmark in bold geometric sans: "VePay" in dark navy, "DO" in the vivid brand blue.

**Logo usage rules**

- Provide `<Logo variant="full | mark | mono | inverse" size />` as the only way to render the logo.
- Minimum sizes: full lockup 120 px wide, mark 24 px.
- Clear space around the logo ≥ the height of the "D" in the wordmark.
- Do not stretch, recolor outside the variants, add shadows to the wordmark, or place it on low-contrast backgrounds.
- Backgrounds: light (`surface`), navy (`primary-900`) with the inverse variant.

**Brand assets** live in `docs/design/brand/` and are **provided by the owner** (SVG master, PNG exports). The reference presentation board is `docs/design/brand/reference-board.png`. Until final SVG assets exist, use a text wordmark plus a simple placeholder mark behind the single `<Logo>` component so the final assets can be swapped in one place. Required exports before the pilot: SVG (full, mark, mono, inverse), PWA icons 192 and 512 px (plus a **maskable** 512), favicon, Apple touch icon 180, splash artwork, and an Open Graph image.

---

## 3. Design tokens

Implement as CSS variables plus the Tailwind theme (`tailwind.config`), single source of truth in `packages/config`. The five brand colors are fixed.

### 3.1 Color

**Brand (provided):**

| Token | Hex | Role |
|---|---|---|
| `primary-900` (`color-1`) | `#1D2F65` | Primary text, wordmark, dark surfaces, pressed state |
| `primary-700` (`color-2`) | `#2F4CA2` | Links, hover, secondary text on tinted backgrounds |
| `primary-500` (`color-3`) | `#4169E1` | Primary buttons, active tab, accents |
| `primary-300` (`color-4`) | `#96ACEE` | Decorative strokes, borders and secondary text **on dark backgrounds only** |
| `primary-100` (`color-5`) | `#D9E1F9` | Tinted fills, chips, dividers, selected rows |

**Neutrals and semantic colors:**

| Token | Hex | Use |
|---|---|---|
| `surface` | `#FFFFFF` | Cards, sheets, inputs |
| `surface-subtle` | `#F4F7FE` | App background |
| `border` | `#D9E1F9` | Dividers, input borders (use `primary-300` for focused/hover borders) |
| `text-primary` | `#1D2F65` | Main text |
| `text-secondary` | `#4A5A8C` | Supporting text |
| `text-disabled` | `#8091C0` | Disabled text (exempt from contrast requirements) |
| `success-bg` / `success-text` / `success-solid` | `#E3F5EA` / `#0F6B3A` / `#157A44` | Confirmed |
| `warning-bg` / `warning-text` / `warning-solid` | `#FCEFD2` / `#7A4E00` / `#B25E00` | Needs review |
| `danger-bg` / `danger-text` / `danger-solid` | `#FBE0E0` / `#9B2222` / `#C62828` | Critical alert, rejected |
| `info-bg` / `info-text` | `#D9E1F9` / `#1D2F65` | Informational |

**Verified contrast ratios (WCAG, computed):**

| Pair | Ratio | Verdict |
|---|---|---|
| `text-primary` `#1D2F65` on `#FFFFFF` | 12.75:1 | Pass |
| `text-secondary` `#4A5A8C` on `#FFFFFF` / `#F4F7FE` | 6.70:1 / 6.25:1 | Pass |
| `primary-700` `#2F4CA2` on `#FFFFFF` | 7.85:1 | Pass (use for links) |
| White on `primary-500` `#4169E1` (primary button) | 4.85:1 | Pass (normal text) |
| `primary-500` text on `#F4F7FE` | 4.52:1 | Borderline: **do not use `primary-500` for small text; use `primary-700`** |
| `primary-300` `#96ACEE` on `#FFFFFF` | 2.23:1 | **Fail for text.** Dark backgrounds only |
| `primary-300` on `primary-900` | 5.73:1 | Pass |
| `primary-100` on `primary-900` | 9.77:1 | Pass |
| `success-text` on `success-bg` | 5.81:1 | Pass |
| `warning-text` on `warning-bg` | 6.31:1 | Pass |
| `danger-text` on `danger-bg` | 6.37:1 | Pass |
| White on `success-solid` / `warning-solid` / `danger-solid` | 5.39 / 4.67 / 5.62 | Pass |

Add an automated test that recomputes these ratios from the tokens and fails the build if any text pair drops below 4.5:1 (3:1 for large text and UI components).

### 3.2 Typography

- **Poppins** (600, 700) for the wordmark, display, and H1–H2. **Plus Jakarta Sans** (400, 500, 700) for UI text, forms, and amounts.
- **Self-host** the fonts (Fontsource packages), Latin subset, `font-display: swap`, preload only the two critical weights. No runtime requests to external font CDNs (offline PWA, privacy, CSP).
- Fallback stack: `system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`.
- Amounts and table numbers use `font-variant-numeric: tabular-nums`. Verify that the chosen font supports tabular figures; if not, use the fallback stack for numeric columns.

| Style | Mobile (size/line) | Desktop | Weight |
|---|---|---|---|
| Display | 28/34 | 36/44 | Poppins 700 |
| H1 | 24/30 | 28/36 | Poppins 600 |
| H2 | 20/26 | 22/28 | Poppins 600 |
| H3 | 17/24 | 18/26 | Jakarta 700 |
| Body | 16/24 | 16/24 | Jakarta 400 |
| Body strong | 16/24 | 16/24 | Jakarta 700 |
| Small | 14/20 | 14/20 | Jakarta 500 |
| Caption | 12/16 (minimum text size in the app) | 12/16 | Jakarta 500 |
| Amount (large) | 28/32 | 32/36 | Jakarta 700, tabular |
| Amount (list) | 16/24 | 16/24 | Jakarta 700, tabular |

Respect user font scaling up to 200% without clipping or horizontal scroll.

### 3.3 Spacing, radii, elevation, motion

- **Spacing scale (4 px base):** 4, 8, 12, 16, 20, 24, 32, 40, 48, 64. Screen horizontal padding 16 (mobile), 24 (tablet), 32 (desktop).
- **Radii:** 8 inputs and chips, 12 buttons, 16 cards, 24 sheets and hero cards, 999 pills and avatars.
- **Elevation:** `e0` none (flat), `e1` cards (`0 2px 8px rgb(29 47 101 / .08)`), `e2` floating elements (`0 8px 24px rgb(29 47 101 / .16)`), `e3` overlays (`0 16px 40px rgb(29 47 101 / .24)`). Shadows are navy-tinted, never pure black.
- **Motion:** 120 ms (micro feedback), 200 ms (transitions), 320 ms (sheets). Ease-out for entering, ease-in for leaving. Honor `prefers-reduced-motion`: replace movement with fades, no parallax, no auto-playing animation.
- **Themes:** light only in the MVP. Structure tokens as CSS variables so a dark theme can be added later without refactoring.

---

## 4. Layout and navigation

**Breakpoints:** base 360 (design target), 768 (tablet), 1024 (desktop), 1280 (wide). Everything must work at **360 px width** and at 200% zoom.

### 4.1 Mobile (primary for the Verifier)

Bottom tab bar, 5 slots, height 64 plus safe-area inset:

| Slot | Label (es-DO) | Destination | Notes |
|---|---|---|---|
| 1 | Bandeja | `/inbox` | Badge = receipts needing a decision; badge turns red when any critical alert exists |
| 2 | Cobros | `/charges` | |
| 3 (center) | **+** Subir | `/upload` | Raised round button (`primary-500`, 56 px). Opens camera/gallery chooser |
| 4 | Cuadre | `/reconciliation` | |
| 5 | Más | Sheet | Customers, Accounts (Owner), Alerts, Audit (Owner), Settings (Owner), Usage (Owner), Profile |

The Verifier sees the same bar; owner-only entries are absent from "Más", never disabled-and-visible. Alerts are also reachable from the Inbox filter chip "Alertas".

### 4.2 Tablet and desktop

- ≥ 768: left rail (icons) that expands at ≥ 1024 into a sidebar with labels. Top bar with search and a primary "Subir comprobante" button.
- Review screen at ≥ 1024: **two panes**, image on the left (sticky, zoom/pan), form and decision on the right.
- Tables (charges, audit, reconciliation detail) use `DataTable` on desktop and collapse to **card lists** on mobile. Never horizontal-scroll a table on mobile.

### 4.3 Navigation rules

- One primary action per screen. The system back gesture always works; unsaved edits trigger a "Discard changes?" dialog only if the form is dirty.
- Deep links from push notifications land on the exact receipt and, on back, return to the Inbox.
- The active tab keeps its scroll position and filter state when switching tabs.
- Forced redirects are defined in `AGENTS.md` 12.2.

---

## 5. Ergonomics and interaction rules

- **Touch targets:** minimum **44 × 44 px** (48 preferred), at least 8 px between adjacent targets. WCAG 2.2 AA requires 24 px; we exceed it deliberately for one-handed use.
- **Thumb zone:** primary actions live in the bottom third (sticky action bar, bottom sheets, tab bar). Destructive actions are never the nearest thumb target.
- **No swipe-to-confirm** or any gesture that moves money state. Swipe may be used for non-destructive navigation only. Confirmations are explicit button presses.
- **Pull to refresh** on lists. **Long press** on an inbox row enters multi-select.
- **Keyboards and inputs:** `inputmode="decimal"` for amounts, `inputmode="numeric"` for last-4 digits and codes, native date picker with a text fallback, `autocomplete` and `autocapitalize` set correctly, `enterkeyhint` on forms.
- **Feedback within 100 ms** of any tap (pressed state), and a visible result within 1 s or a progress indicator.
- **Consistency:** the same action has the same label, icon, and position everywhere (Confirm is always the primary button at the bottom right on desktop, full-width at the bottom on mobile).
- **Desktop shortcuts (optional, discoverable via `?`):** `J`/`K` move through the inbox, `Enter` opens, `C` confirms and `R` rejects on the review screen **only when focus is not inside a field**.

---

## 6. Forms and validation

- Validate **on blur** and on submit; clear the error as soon as the value becomes valid. Errors appear below the field with an icon and text (never color alone).
- **Do not disable the submit button** to signal an invalid form; allow submit and focus the first invalid field with a clear message. Exception: when a role or rule makes an action impossible (for example a Verifier on a receipt with a critical alert), disable it **and** show the reason next to it.
- **Preserve input** on any error, including network errors. Drafts of long forms (new charge, CSV mapping) autosave locally.
- **Money input:** `MoneyInput` shows the currency prefix (`RD$` or `US$`), formats with thousands separators on blur, accepts both `1500.5` and `1,500.50`, stores a decimal string, and never uses floating point.
- **Dates:** show `dd/MM/yyyy`; store in ISO. Warn (do not block) for future dates and dates older than the stale threshold; the server raises the matching alert.
- **Selection of customers and charges:** searchable combobox with recent items first; show distinguishing info (code, amount, customer) to avoid picking the wrong one.
- **Extracted fields (`FieldWithConfidence`):**
  - Confidence ≥ 0.90: normal field, no marker.
  - 0.80 to < 0.90: amber outline and the hint "Revisa este dato".
  - < 0.80 or missing: amber fill, focus order puts it first, hint "No pudimos leerlo con seguridad".
  - A field the person edited shows a subtle "Editado" marker and keeps the original OCR value one tap away ("Ver lo que leyó el sistema").
  - Never show numeric confidence scores to users.

---

## 7. Component catalogue

Build on shadcn/ui and Radix primitives; style with the tokens above. Provide a development-only route `/dev/components` that renders every component in every state (used for visual QA and accessibility checks).

| Component | Notes |
|---|---|
| `Button` | Variants: `primary`, `secondary` (outline), `ghost`, `danger`; sizes `md` (48), `sm` (40, desktop only); loading state keeps width |
| `Logo` | Variants from section 2 |
| `StatusBadge` | Status → color, icon, label (table below). Icon + text always |
| `AlertBanner` | Severity styles with distinct icons (info circle, warning triangle, critical octagon) and a one-line plain message |
| `ReceiptRow` | Avatar/initials, sender, amount (tabular), relative time, status badge, risk marker |
| `FieldWithConfidence`, `MoneyInput`, `DateField`, `Combobox`, `Select`, `Textarea` | See section 6 |
| `ImageViewer` | Pinch zoom, pan, double-tap to zoom, rotate, full-screen, keyboard support on desktop |
| `BottomSheet` / `Dialog` | Bottom sheet on mobile, centered dialog on desktop; focus trap, `Esc` to close |
| `Toast` | Bottom, above the tab bar; supports an action ("Deshacer") and a timer ring |
| `Skeleton` | Matches the final layout (rows, cards); no generic spinners for lists |
| `EmptyState` | Illustration-free: icon, one-line explanation, single call to action |
| `ProgressStepper` | Onboarding and upload batches ("Procesando 3 de 8") |
| `OfflineBanner` | Persistent, non-blocking, states what still works |
| `DataTable` / `CardList` | Desktop table, mobile card list, same data and actions |

**Status presentation**

| Status | Label (es-DO) | Colors | Icon (lucide) |
|---|---|---|---|
| `RECEIVED` / `PROCESSING` | Procesando | info | `loader` (static when reduced motion) |
| `FAILED` | No se pudo leer | warning | `file-warning` |
| `PENDING_REVIEW` | Por revisar | warning | `clock` |
| `CONFIRMED` | Confirmado | success | `check-circle` |
| `PARTIAL` | Pago parcial | info | `circle-dashed` |
| `OVERPAID` | Pagó de más | info | `plus-circle` |
| `DUPLICATE` | Duplicado | danger | `copy` |
| `SUSPICIOUS` | Con alerta | danger | `alert-octagon` |
| `UNMATCHED` | Sin cobro | warning | `link-2-off` |
| `REJECTED` | Rechazado | neutral | `x-circle` |

---

## 8. Key screen specifications

### 8.1 Inbox (`/inbox`)

```
┌──────────────────────────────┐
│ [mark] Bandeja        [🔍]   │  App bar
│ Hoy, 13 de octubre           │
│ ┌──────────────────────────┐ │
│ │ Cuadre de hoy            │ │  Summary card (primary gradient)
│ │ RD$ 184,250.00           │ │
│ │ 12 Confirm · 3 Revisar · 1 Alerta │
│ └──────────────────────────┘ │
│ (Todos)(Por decidir)(Alertas)(Procesando) │  Filter chips
│ Necesita tu decisión         │
│ ┌──────────────────────────┐ │
│ │ MP María Pérez  RD$4,500 │ │  ReceiptRow + StatusBadge
│ │ hace 4 min · Match sugerido │
│ └──────────────────────────┘ │
│ Procesando (2) …             │
│ Resuelto hoy …               │
│ [Bandeja][Cobros] (+) [Cuadre][Más] │
└──────────────────────────────┘
```

- Sections in this order: **Needs your decision** (critical alerts first, then oldest first), **Processing**, **Resolved today** (collapsed by default after 5 items).
- Summary card shows today's confirmed total and counts; tapping a count applies the filter.
- Multi-select (long press): contextual bar "Confirmar N". Enabled only when **all** selected receipts are strong matches without open alerts; otherwise show why in a tooltip or inline text.
- Pull to refresh; polling only while items are processing.
- Empty state: "Todo al día." plus the "Subir comprobante" action.

### 8.2 Upload (`/upload`) and share target

- Entry options as large tiles: **Cámara**, **Galería**, **Archivo (PDF)**. On Android the share target lands here with the files preloaded.
- Camera guidance overlay: frame outline, "Que se vea todo el comprobante", glare and blur hints are optional later.
- Queue list with per-item state: `En cola` → `Subiendo` → `Procesando` → `Listo` / `Error` with retry. The tray stays visible across navigation (mini progress above the tab bar).
- Offline: items stay in the queue with "Se enviará cuando haya conexión".
- iOS: the share-from-WhatsApp path is unavailable; show the Camera and Gallery tiles prominently and the install guide link.

### 8.3 Review (`/receipts/:id`)

```
┌──────────────────────────────┐
│ ←  Comprobante         ⋮     │
│ [ image card, tap to zoom ]  │
│ ▲ Banner: reason / alerts    │  AlertBanner(s), most severe first
│ ┌ Posible cobro ───────────┐ │
│ │ Pedido #A123 · M. Pérez  │ │  Match card + "Cambiar"
│ │ Coincide en monto y fecha│ │
│ └──────────────────────────┘ │
│ Monto      RD$ 4,500.00      │  FieldWithConfidence list
│ Fecha      12/10/2026        │
│ Referencia 0048213377        │
│ Banco      Banreservas       │
│ Cuenta destino •••• 1234     │
│ Remitente  María Pérez       │
│ [  Confirmar  ]   Rechazar   │  Sticky action bar
└──────────────────────────────┘
```

- Order on mobile: image (collapsible to a thumbnail), alert banners, match card, fields, sticky action bar.
- The match card explains **why** in one line ("Coincide en código y monto") using `match_signals`.
- Tapping a field highlights where it was read on the image when box data exists.
- Verifier with an open critical alert: "Confirmar" is disabled with the inline reason "Una alerta crítica necesita al Dueño"; "Rechazar" stays available.
- Desktop: two panes; the action bar sits at the bottom of the right pane.

### 8.4 Confirm sheet

- Lists candidate charges (suggested first) with remaining balance; supports selecting several; offers **"Registrar como ingreso libre"** and **"Crear cobro con estos datos"**.
- Shows the outcome **before** committing: "Resultado: Confirmado" / "Pago parcial: quedará un saldo de RD$ X" / "Pagó de más: sobran RD$ X".
- Primary button "Confirmar pago". On success: a toast "Pago confirmado" with **Deshacer** for 5 seconds; the list updates immediately (optimistic), reverting if the undo is used or the server rejects.
- Errors from the server (cap exceeded, open critical alert, concurrent confirmation) show a clear message inside the sheet and keep the selection.

### 8.5 Reject and revert

- Dialog with a required reason (quick-pick chips: "Monto incorrecto", "Comprobante duplicado", "No es de este negocio", "Otro") plus optional text. Revert (Owner only) additionally warns that applied amounts will be released.

### 8.6 Alerts (`/alerts`) and resolution

- List grouped by severity, each item showing the plain-language message and the receipt. Resolution actions: **Descartar** (reason required), **Confirmar de todos modos** (Owner, reason required), **Rechazar comprobante**.

### 8.7 Daily reconciliation (`/reconciliation`)

- Date picker (defaults to today). Totals by status as stat cards, then by bank and account (card list on mobile, table on desktop), then open charges.
- "Exportar" opens a dialog with format choice (Excel, CSV, PDF) and a clear note of the date and filters used. Download feedback via toast.
- The Verifier sees only today; the date picker is hidden for them.

### 8.8 Charges, customers, accounts

- Lists with search, filters as chips, and the `+` action in the app bar. Detail screens show a balance header (amount, applied, remaining) and a history list.
- CSV import: 3 steps (choose file, preview with row errors highlighted, import). The result screen shows imported, skipped (with reasons), and a downloadable error file.

### 8.9 Access, onboarding, install

- **Welcome:** logo on `primary-900` or light gradient, tagline, one primary button "Comenzar", a secondary "Ya tengo cuenta".
- **Register / Login:** one column, large fields, show/hide password, clear errors; no CAPTCHA in the MVP.
- **Onboarding:** a 3-step wizard with progress (business details → main bank account → sample receipt). The final step shows the extraction and review flow on the sample. The Owner can skip the sample.
- **Install guide:** shown after the first successful confirmation, not on first load. Android: native install prompt when available; iOS: "Compartir → Añadir a pantalla de inicio" instructions with a visual. Notification opt-in is requested **in context** (after the first alert is shown or from Profile), with a one-sentence explanation first and never on a cold start.

### 8.10 Settings, members, usage, profile

- Settings: grouped cards with plain-language descriptions and examples ("Ventana de fecha: cuántos días de diferencia aceptamos entre el cobro y el pago").
- Members: temporary password shown **once**, with a copy button and a warning; forced change on first login.
- Usage: a simple progress bar of receipts this month versus the plan (informational).
- Profile: language, change password, notifications permission, install guide, send feedback, sign out.

### 8.11 Account security, legal, support, and failure flows

- **Forgot and reset password:** one field and one primary button each. The confirmation after requesting a link is always the same neutral message. The reset screen shows the new-password field with the policy as helper text and a show/hide toggle; success sends the person to `/login` with a toast. Expired or used links show a clear message with "Pedir otro enlace".
- **Legal acceptance:** at registration, a checkbox with links to the terms and the privacy policy opens readable in-app pages. When a version changes, a full-screen blocking page titled "Actualizamos nuestros términos" shows a short summary, links to the documents, and one button "Aceptar y continuar". No dark patterns: the decline path is "Cerrar sesión".
- **Sessions (`/profile/sessions`):** a list of devices (icon by device type, label, "Activa hace 5 min", a badge "Este dispositivo"); each row has "Cerrar sesión"; a secondary button "Cerrar sesión en los demás dispositivos" asks for confirmation. Owners also get "Cerrar sesiones" on each member row.
- **Support access (`/settings/support-access`, Owner):** an explanatory card (what Support can and cannot see: audit events, receipt metadata, and alerts; never images; read-only), a segmented control for 1 hour, 24 hours, or 72 hours, and "Permitir acceso de soporte". While active, a card with a countdown and "Revocar acceso". Every Support access appears in the audit log.
- **Support console (`/support`):** functional and table-first, desktop-oriented; a persistent banner "Modo soporte (solo lectura)" whenever tenant data is shown; the empty state "El Dueño no ha otorgado acceso." appears when no grant is active.
- **Failed receipt (UC-RCP-05):** in the review screen the banner explains the reason (copy in 9.2) and offers three actions in this order: **Completar a mano**, **Volver a tomar la foto**, **Descartar**. Retaking opens the camera with three tips (the whole receipt in view, no glare, steady hand). Discard asks for the reason code through the standard reject dialog.
- **Remember this sender (UC-DATA-02):** in the confirm sheet, when the sender does not match the selected customer, an inline row "¿{sender} paga por {customer}?" with a toggle "Recordar este remitente" (default off). If the alias already belongs to another customer, show "Este remitente ya está asociado a {otherCustomer}." with no pre-selected change.

---

## 9. Content and microcopy (es-DO)

**Voice:** clear, calm, respectful, direct. Address the user as **tú**. Short sentences. Buttons start with a verb. No technical jargon (no "OCR", "hash", "RLS", "token"). Never blame the user. Explain what happened and what to do next. Amounts as `RD$ 1,500.00`; dates `dd/MM/yyyy`; relative time for recent events ("hace 4 min").

### 9.1 Alert messages (parameters in braces)

| Rule | i18n key | Copy |
|---|---|---|
| `DUPLICATE_REFERENCE` | `alerts.duplicate_reference` | "Esta referencia ya se usó el {date} en un pago de RD$ {amount}." |
| `IDENTICAL_IMAGE` | `alerts.identical_image` | "Esta imagen es idéntica a otra que ya subiste el {date}." |
| `SIMILAR_IMAGE` | `alerts.similar_image` | "Esta imagen se parece mucho a otra del {date}. Compáralas antes de confirmar." |
| `FOREIGN_DESTINATION_ACCOUNT` | `alerts.foreign_destination_account` | "La cuenta destino (•••• {last4}) no es una de las cuentas registradas de tu negocio." |
| `FUTURE_DATE` | `alerts.future_date` | "La fecha del pago es posterior a hoy." |
| `DATE_BEFORE_CHARGE` | `alerts.date_before_charge` | "El pago tiene una fecha anterior a la del cobro." |
| `STALE_DATE` | `alerts.stale_date` | "El pago tiene más de {days} días de antigüedad." |
| `INVALID_REFERENCE_FORMAT` | `alerts.invalid_reference_format` | "La referencia no tiene el formato habitual de {bank}." |
| `AMOUNT_MISMATCH` | `alerts.amount_mismatch` | "El monto (RD$ {amount}) no coincide con el saldo del cobro (RD$ {balance})." |
| `SENDER_MISMATCH` | `alerts.sender_mismatch` | "El remitente no coincide con el cliente del cobro." |
| `LOW_CONFIDENCE` | `alerts.low_confidence` | "No pudimos leer con seguridad {field}. Revísalo en la imagen." |

### 9.2 Common messages

| Situation | i18n key | Copy |
|---|---|---|
| Offline | `system.offline` | "Sin conexión. Tus comprobantes se guardan y se enviarán cuando vuelva la conexión." |
| Server error | `system.server_error` | "Algo salió mal de nuestro lado. Intenta de nuevo en un momento." |
| No permission | `system.forbidden` | "No tienes permiso para hacer esto. Pídeselo al Dueño del negocio." |
| Session expired | `auth.session_expired` | "Tu sesión venció. Inicia sesión otra vez." |
| File too large | `upload.too_large` | "Este archivo pesa más de 10 MB. Toma la foto otra vez o elige uno más ligero." |
| Unsupported file | `upload.unsupported` | "Solo podemos leer fotos (JPG, PNG o WebP) y archivos PDF." |
| Processing failed | `receipt.failed` | "No pudimos leer este comprobante. Complétalo a mano o intenta de nuevo." |
| Confirmed | `receipt.confirmed_toast` | "Pago confirmado." (action: "Deshacer") |
| Update available | `pwa.update_available` | "Hay una versión nueva. Actualizar" |
| Concurrent change | `receipt.conflict` | "Otra persona acaba de modificar este comprobante. Revisa los cambios." |
| Not a receipt | `receipt.failed.not_a_receipt` | "Esto no parece un comprobante de transferencia. Si sí lo es, complétalo a mano o toma otra foto." |
| Unreadable image | `receipt.failed.unreadable` | "No pudimos leer la imagen. Prueba con una foto más clara, sin reflejos y con todo el comprobante a la vista." |
| Reading error | `receipt.failed.ocr_error` | "Tuvimos un problema leyendo este comprobante. Intenta de nuevo o complétalo a mano." |
| Reading timeout | `receipt.failed.timeout` | "La lectura tardó demasiado. Intenta de nuevo o complétalo a mano." |
| Reset requested | `auth.reset_requested` | "Si ese correo está registrado, te enviamos un enlace para cambiar tu contraseña. Revisa también el correo no deseado." |
| Reset done | `auth.reset_done` | "Contraseña cambiada. Inicia sesión con la nueva." |
| Reset link invalid | `auth.reset_invalid` | "Este enlace venció o ya se usó. Pide uno nuevo." |
| Legal update | `legal.accept_title` | "Actualizamos nuestros términos" |
| Remember sender | `alias.remember_prompt` | "¿{sender} paga por {customer}?" (toggle: "Recordar este remitente") |
| Alias conflict | `alias.conflict` | "Este remitente ya está asociado a {otherCustomer}." |
| Revoke other sessions | `sessions.revoke_others_confirm` | "¿Cerrar sesión en los demás dispositivos?" |
| Support banner | `support.banner` | "Modo soporte (solo lectura)" |
| No support grant | `support.no_grant` | "El Dueño no ha otorgado acceso." |

### 9.3 Empty states

| Screen | Copy | Action |
|---|---|---|
| Inbox | "Todo al día. Cuando llegue un comprobante, lo verás aquí." | Subir comprobante |
| Charges | "Aún no tienes cobros. Crea uno para que podamos sugerir coincidencias." | Crear cobro |
| Customers | "Aún no tienes clientes. Se crean solos al registrar un cobro." | Crear cliente |
| Alerts | "Sin alertas. Si algo no cuadra, te avisamos aquí." | — |
| Reconciliation | "No hay pagos confirmados en esta fecha." | Cambiar fecha |

### 9.4 Push notifications

| Trigger | Copy |
|---|---|
| Critical alert | "Alerta en un comprobante: {reason}. Revísalo." |
| Reviews pending | "Tienes {n} comprobantes por revisar." |
| Processing failed | "No pudimos leer un comprobante. Complétalo a mano." |

One notification per alert; batch bursts into a single summary; never notify for routine confirmations.

### 9.5 Words to avoid

"Verificado por VepayDO", "fraude", "falso", "garantizado", "seguro al 100 %", "error del usuario". Prefer "Confirmado", "Con alerta", "Revisa", "No coincide".

---

## 10. Feedback, loading, and motion

- **Lists:** skeletons that match the final layout. **Actions:** inline button loading state that preserves size. **Batches:** "Procesando 3 de 8". No blocking spinner longer than 300 ms without context text.
- **Optimistic updates** for confirm, reject, and reading state, with rollback and a clear message on failure.
- **Success:** a toast plus a subtle check animation (200 ms). **Error:** inline message at the point of failure plus a toast only if the user left the screen.
- **Haptics:** short vibration on confirm and on critical errors via the Vibration API, feature-detected, never required, and disabled by a profile toggle.
- **Offline:** persistent banner stating what still works (upload and review of loaded items) and what does not. Never display stale data without a "Actualizado hace X" indicator.
- **App update:** non-blocking toast; apply on the next navigation or when the user taps update; never reload while an upload or a form is in progress.

---

## 11. High-stakes and destructive actions

Prefer **undo** over confirmation dialogs for reversible actions; use dialogs for irreversible or high-impact ones.

| Action | Pattern |
|---|---|
| Confirm payment | Outcome preview + 5 s undo toast |
| Reject receipt | Dialog with required reason (reversible by Owner) |
| Revert final status (Owner) | Dialog with required reason and a warning about released allocations |
| Resolve critical alert / "confirm anyway" (Owner) | Dialog with required reason |
| Void charge | Dialog with required reason |
| Delete customer alias | Simple confirmation |
| Cancel account / delete data | Type-to-confirm (the business name) |
| Sign out | No confirmation |

---

## 12. Accessibility

- Target **WCAG 2.2 AA**: contrast per section 3.1; focus visible on every interactive element (2 px `primary-700` ring with offset); logical focus order; focus management on route change, sheet open/close, and after actions.
- Semantics: real `button`, `a`, `input` with `label`; never clickable `div`s. Icon-only buttons have `aria-label` in Spanish. Decorative icons are `aria-hidden`.
- **Live regions:** processing progress and toasts use `aria-live="polite"`; critical errors use `assertive`.
- Color is never the only signal: statuses and severities combine icon, text, and shape.
- Support zoom to 200%, text scaling, landscape, and `prefers-reduced-motion`.
- Image content has alternative text: "Imagen del comprobante" plus the extracted fields as real text next to it, so the information is available without the image.
- Automated: `axe` in component and e2e tests; manual: a keyboard-only walkthrough and a TalkBack pass on the five key screens before the pilot.

---

## 13. PWA experience

- **Manifest:** `name` "VepayDO", `short_name` "VepayDO", `theme_color` `#1D2F65`, `background_color` `#F4F7FE`, `display: standalone`, `orientation: portrait` preferred (landscape still usable), maskable icon, `share_target` as defined in `AGENTS.md`.
- **Splash and status bar:** navy status bar with light content; splash with the mark on the brand background.
- **Install prompt timing:** after the first successful confirmation, never on first load; dismissible and not repeated for 14 days.
- **Notification opt-in:** contextual, with a one-sentence benefit; if denied, offer the option again only from Profile.
- **Update flow:** section 10. **Platform notes:** Web Share Target is not available on iOS Safari (verify current support before promising it); iOS push requires the installed PWA. The UI must degrade gracefully, never showing a feature that cannot work.

---

## 14. Onboarding and first-run guidance

- Checklist visible on the Inbox until complete: "Registra tu cuenta bancaria", "Crea tu primer cobro (opcional)", "Sube tu primer comprobante", "Invita a tu verificador". Items are skippable and the checklist is dismissible.
- Maximum **three** coach marks in total, each dismissible and shown once: the Upload button, the match card, the Undo toast.
- Never block the main flow with tours or modals.

---

## 15. UX metrics and instrumentation

Add an `Analytics` port with a first-party implementation that writes to a tenant-scoped `ux_events` table (`tenant_id`, `user_id`, `name`, `props jsonb`, `created_at`, row level security, 12-month retention). **No personal or financial data in events:** no amounts, names, references, or account digits.

| Event | Properties |
|---|---|
| `receipt_uploaded` | `source`, `count`, `offline` |
| `receipt_opened` | `status`, `risk` |
| `field_edited` | `field`, `confidence_band` |
| `receipt_confirmed` | `one_tap`, `bulk`, `time_to_decision_ms`, `used_undo` |
| `receipt_rejected` | `reason_code` |
| `alert_resolved` | `rule`, `resolution` |
| `share_target_used` | `files` |
| `upload_queue_flushed` | `items`, `attempts` |
| `onboarding_step_completed` | `step` |
| `install_prompt` | `outcome` |
| `offline_session` | `duration_ms` |

**Derived metrics:** one-tap rate; median and p90 time to decision; edit rate per field (a proxy for extraction quality per bank); alert usefulness (share of alerts ending in rejection versus "confirm anyway"); upload failure and retry rate; onboarding completion; undo usage (a high rate signals accidental confirmations).

---

## 16. Usability testing plan (pilot)

**Participants:** at least 5 people per round: cashiers and owners from the pilot businesses. Test on their own phones.

**Tasks**

| # | Task | Success criterion (initial target) |
|---|---|---|
| T1 | Send a receipt from WhatsApp and confirm it | Completed unaided by ≥ 4 of 5; median ≤ 30 s from share to confirmation |
| T2 | Handle a duplicate-reference alert as Verifier, then as Owner | Understands why it is flagged; Verifier does not get stuck |
| T3 | Create a charge and see it matched | Completed unaided by ≥ 4 of 5 |
| T4 | Correct a wrongly read amount | Finds and edits the field in ≤ 20 s |
| T5 | Use the app with no connection, then reconnect | Understands receipts were saved; no duplicates |
| T6 | Find today's reconciliation and export it (Owner) | Completed unaided by all owners |

**Protocol:** think-aloud, a neutral facilitator, no hints before a 60-second struggle, record time and errors, then the **System Usability Scale** (10 items, translated into Spanish) and three open questions ("¿Qué fue lo más confuso?", "¿Qué usarías todos los días?", "¿Qué te faltó?"). Target SUS ≥ 75 as an initial goal.

**Cadence:** a round before the pilot opens and one every two weeks during it. Rate issues by severity (blocker, major, minor), fix blockers before the next round, and keep a changelog of UX decisions in `docs/design/ux-decisions.md`. Provide an in-app "Enviar comentario" link in Profile.

---

## 17. UX Definition of Done (apply to every screen)

- [ ] Works at 360 px and 200% zoom; no horizontal scroll on mobile.
- [ ] Loading skeleton, empty state, error state, and offline behavior implemented and visible in `/dev/components` or the screen's stories.
- [ ] Every string uses an i18n key; es-DO copy follows section 9; no forbidden words.
- [ ] Tokens only (no raw hex or ad-hoc spacing); contrast test green.
- [ ] Touch targets ≥ 44 px; primary action reachable with the thumb on mobile.
- [ ] Keyboard path and focus management verified; `axe` clean; icon buttons labeled.
- [ ] Destructive actions follow section 11; undo or reason capture is present where required.
- [ ] Analytics events from section 15 are emitted, with no personal data.
- [ ] Role differences verified (Owner and Verifier) including disabled-with-reason states.
- [ ] Reviewed against the brand rules in section 2.

---

## 18. Open decisions and assumptions

- Final logo SVG assets and the exact spelling of the brand name are pending (name clearance).
- Font pairing (Poppins and Plus Jakarta Sans) and tabular figure support must be verified at implementation; fall back as described in 3.2 if needed.
- Dark theme, illustrations, and localization beyond es-DO and en are out of scope for the MVP.
- Usability targets and SUS thresholds are initial hypotheses to be recalibrated with pilot data.