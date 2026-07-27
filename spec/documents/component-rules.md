# BC Gov Design System — Component Rules (CES adaptation)

> **Purpose.** A working reference for the BC Gov Design System component rules,
> rewritten for **how we actually build CES**: native HTML controls styled with
> SCSS + `@bcgov/design-tokens`, *not* the official React component library.
>
> **Why this exists.** The BC Gov component pages
> (<https://www2.gov.bc.ca/gov/content/digital/design-system/components>) document
> the official look, feel, and accessibility requirements — but ship as
> `@bcgov/design-system-react-components`. CES is Vue 3 + Vuetify, and we
> deliberately use **native HTML elements** (`<button>`, `<input>`, `<select>`,
> `<textarea>`, `<dialog>`) styled by hand. Vuetify is only used where a native
> element won't do (app shell, nav). So we cannot consume the components directly —
> we reimplement the **rules** they encode.
>
> **Scope (this revision).** Covers the controls currently present in the app:
> **Buttons, Text field, Text area, Select, Dialog, Tags/Chips**. Other components
> (Checkbox, Radio, Switch, Number field, Date picker, Alert banner, Inline alert,
> Tooltip, Accordion, etc.) are listed under [Not yet documented](#not-yet-documented)
> and should be added here before they're built.
>
> Tokens referenced below are the SCSS aliases in
> [`web/src/styles/_variables.scss`](../../web/src/styles/_variables.scss).

---

## Universal rules (apply to every control)

These are cross-cutting requirements repeated on nearly every BC Gov component page.
Treat them as the baseline checklist for **any** control we build.

1. **Use the semantic native element.** A button is a `<button>`, a select is a
   `<select>`, a dialog uses `<dialog>`/`role="dialog"`. Screen-reader role and
   keyboard behaviour come for free and satisfy WCAG 4.1.2. Never fake a control
   with a styled `<div>`.
2. **Visible focus must be an *offset* ring.** Every interactive control shows a
   blue, offset focus ring on keyboard focus (WCAG 2.4.7). Implement with
   `outline` + `outline-offset` — do **not** remove outlines without an equivalent
   replacement. Hover changes the cursor and (usually) a fill/border colour.
3. **Minimum target size.** WCAG 2.5.8 (AA):
   - Buttons, text field, text area, tags → **≥ 32 × 32 px** clickable area.
   - Select / dropdown → **≥ 40 × 40 px** selector area.
4. **Never rely on colour alone** to convey state or meaning (WCAG 1.4.1). Pair
   colour with shape, icon, or text — e.g. an error is red border **+** icon **+**
   message, not just a red border. Status chips carry a text label, not just a hue.
5. **Contrast ≥ 4.5:1** for fill vs text in all states except `disabled`
   (WCAG 1.4.11). The design tokens already satisfy this — stay within them.
6. **Every control needs a label.** A visible `<label>` associated via
   `for`/`id` (or `aria-labelledby`). If the visible label is hidden by design,
   you **must** supply an `aria-label`. Icon-only controls always need `aria-label`.
7. **No keyboard traps; predictable behaviour.** `Tab` always exits a control
   (WCAG 2.1.2). Focus alone never changes a value, opens/closes a menu, selects an
   option, or submits a form (WCAG 3.2.1).
8. **Standard interaction states.** Design and style for: `default`, `hover`,
   `focus`, `disabled`, plus control-specific states (`active`, `invalid`,
   `read-only`, `selected`, `opened`, `loading`). `disabled` controls are not
   focusable and show a not-allowed cursor.

---

## Buttons

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/buttons>

### When to use
- For **actions** that progress or complete a task ("Start", "Confirm", "Submit",
  "Delete") — **not** for navigation (use a link for that).
- Keep buttons sparse. Multiple competing buttons raise cognitive load; use
  **variants** to express hierarchy.

### Variants (express hierarchy, don't just recolour)
| Variant | Use for | Notes |
|---|---|---|
| **Primary** | The one key action on a screen | **Limit to one primary per page/screen.** |
| **Secondary** | Supporting actions ("Cancel", "Back") | May appear multiple times / beside a primary. |
| **Tertiary** | Low-profile / ghost actions | Alternative to secondary. |
| **Link** | Lowest-priority or navigation-like actions | Plain hyperlink style. |
| **Danger** | Destructive actions ("Delete", "Reject") | Red. Pair with a confirmation dialog. |

### Sizes
`Large` (prominent CTA when several buttons are shown) · `Medium` (default) ·
`Small` · `Extra-small` (tight spaces).

### States
- **Hover** — fill shifts to the lighter hover shade; cursor changes.
- **Active** — pressed.
- **Focus** — offset blue focus ring.
- **Disabled** — background + text go grey; not focusable; not-allowed cursor.
- (We should also define **loading** for async submits — currently undefined.)

### Rules / accessibility
- Render as a real `<button>` (WCAG 4.1.2).
- Visible text label **or** `aria-label`. Icon-only buttons **require** `aria-label`.
- Use **consistent label wording** for the same action across screens.
- Minimum **32 × 32 px** target (WCAG 2.5.8).
- Don't convey state with colour alone (WCAG 1.4.1); keep the shape/rounded corners.

### CES mapping
- **Shared `.btn` system** lives in
  [`_buttons.scss`](../../web/src/styles/_buttons.scss): a `.btn` base (offset
  focus ring, 32px min target, disabled handling) plus variants
  `--primary` / `--secondary` / `--success` / `--danger` / `--danger-outline` /
  `--primary-outline` / `--tertiary` / `--inverse`, a `--sm` size, and an `--icon`
  shape. Use `class="btn btn--primary"` etc. Icon-only buttons add `--icon` **and**
  an `aria-label`.
- **`--success` (green) is a CES extension, not a BC Gov variant.** BC Gov has no
  green button (green is a status colour) and ships no success-button hover token,
  so we derive the hover shade in `_buttons.scss`. It's used for Accept/Confirm to
  preserve the app's existing green semantic. For strict BC Gov compliance, those
  could move to `--primary`.
- **Still undefined:** a `loading` state for async submits, and a formal size scale
  beyond `--sm` (BC Gov defines Large/Medium/Small/XS).
- **Out of scope / known gap:** `LoginView.vue` still uses Bootstrap classes
  (`btn btn-primary`, `form-control`, `spinner-border`) while Bootstrap CSS is not
  imported — the whole view needs a separate migration onto the design system.

---

## Text field (`<input type="text|email|...">`)

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/text-field>

### When to use
- **Single line** of text (name, email, ticket number). Multi-line → **Text area**.
- Usually paired with a button to submit.

### Anatomy (each part optional except the input)
Text label · secondary label (required/optional) · bordered input · optional
left/right icons · optional helper text · error message slot.

### Sizes
`Medium` (default) · `Small` (reduced height).

### States
- **Hover** — border colour + cursor change.
- **Focus** — offset blue ring + caret.
- **Invalid** — red border, red error icon (right), red message below.
- **Disabled** — grey bg, not focusable. **Read-only** — grey bg, selectable but not editable.

### Rules / accessibility
- Associate label + input via `for`/`id` (or `aria-labelledby`); hidden label →
  `aria-label` (WCAG 1.3.1 / 1.3.5).
- Support HTML constraint validation (`type`, `minlength`/`maxlength`, `pattern`)
  plus custom validation; show custom messages in the error slot.
- Min **32 × 32 px** target; offset focus ring; no value change / submit on focus.

### CES mapping
- 13 native `<input>` usages today. **Gap:** no shared field/label/error styling
  partial — error styling exists only as the generic `.error-text`/`.required`
  helpers. Worth a shared `.field` pattern (label + input + `.error-text`) so the
  invalid/disabled/read-only states are consistent.

---

## Text area (`<textarea>`)

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/text-area>

### When to use
- **Multiple lines** of plain text (longer answers, notes). No rich-text support.

### Anatomy
Text label · secondary label · bordered input with resize handle (bottom-right) ·
optional helper text · optional **character counter** (when a max length exists).

### States
Same set as text field: `hover`, `focus` (offset ring), `invalid` (red border +
icon + message), `disabled`, `read-only`. Optional visible scrollbar.

### Rules / accessibility
- Same labelling, validation, focus-ring, and **32 × 32 px** min-target rules as the
  text field. Provide a character counter whenever `maxlength` is set.

### CES mapping
- Not clearly present yet; document the shared `.field` pattern to cover both
  `<input>` and `<textarea>` when one is added.

---

## Select / dropdown (`<select>`)

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/select>

### When to use
- **7–15** predefined options in limited space.
- **< 7 options → use radio group.** **> 15 options → reconsider** (use an
  autocomplete / search pattern — cf. our `AutocompleteSelect.vue`).

### Anatomy
Text label · selector (shows current value / placeholder, default "Select an item")
· chevron icon · list box (options, optionally grouped into sections, with optional
icons/descriptions) · error message below on invalid.

### Sizes
`Medium` (default) · `Small`.

### States
- **Selector:** default · hover · opened · selected · disabled · error.
- **List item:** default · hover · **danger** (red, for destructive options).

### Rules / accessibility
- Label associated via `aria-labelledby`; hidden label → `aria-label`.
- **Min 40 × 40 px** selector target (larger than other controls — note this).
- Offset focus ring; `Esc`/`Tab` exit the open menu (no trap); top-to-bottom focus
  order; menu does not open/close and options do not select on focus alone.
- Invalid → red message + warning icon.

### CES mapping
- 4 native `<select>` usages, plus `AutocompleteSelect.vue` for the ">15 / search"
  case. **Gaps:** confirm selectors meet **40 px** (not 32 px); standardise the
  chevron + error presentation.

---

## Dialog / modal (`AppModal.vue`)

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/dialogs>

### When to use
- When the user **must act or confirm** before proceeding, or to **warn** before a
  destructive action. Modals are **highly interruptive — use sparingly.**

### Anatomy (Alert Dialog)
Optional variant icon · optional close button · **title** · description · optional
**action buttons**. Two flavours: *Alert Dialog* (standard layout) and *Generic
Dialog* (empty container for custom content).

### Focus & dismissal
- On open, **focus moves into the dialog**; outside content is hidden from focus.
- Content mounts on open, unmounts on dismiss.
- Default dismissal: `Escape` **or** click outside (both configurable). The dialog
  is never opened/dismissed by focus change alone.

### Accessibility
- `role="dialog"` (or `role="alertdialog"` for confirmations); title associated via
  `aria-labelledby`; also supports `aria-label`.
- No keyboard trap (Escape available), logical focus order, WCAG AA throughout.

> ⚠️ The BC Gov page gives **no explicit button order** for dialog actions. Pick a
> CES convention and apply it everywhere — recommend **primary action on the right,
> secondary/cancel on its left**, and use the **Danger** button variant for
> destructive confirmations (ties into the admin Reject flow in
> [admin-listing-update.md](../admin-listing-update.md)).

### CES mapping
- `AppModal.vue` exists. **Verify:** focus moves in on open, `Escape` + backdrop
  dismiss, `role`/`aria-labelledby` set, focus returns to the trigger on close.

---

## Tags / Chips

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/tags>

### When to use
- **Label / categorise** content, and as a **secondary** filter/sort tool — **not**
  primary navigation.
- Default cap **~8 tags**; more than that means the content needs reorganising.

### Variants
- **Shape:** Rectangular (default) or Circular ("chip"/"pill").
- **Colour:** 8 schemes (Grey, Blue, Yellow, Green, Red, Dark, Theme Blue, Theme
  Gold). Colours are organisational — **no fixed semantic meaning**, so the **text
  label must carry the meaning** (don't encode status in colour alone).

### Anatomy
Coloured background · dark border · text label · optional left icon · optional
right close/remove icon (when "closeable").

### States
`default` · `selected` (border thickens/changes) · `focused/pressed` (offset ring)
· `disabled` (greyed, not focusable). Single or multi-select; selection disabled if
the tag is a hyperlink.

### Accessibility
- Min **32 × 32 px** target; offset focus ring; keyboard select/deselect; consistent
  label naming; no auto-select on focus; group via `aria-labelledby`.

### CES mapping
- Our status/classification "chips" (`%chip-base`, `.chip-*`, `.cl-*`,
  `.status-*` in [`_base.scss`](../../web/src/styles/_base.scss)) are the
  read-only, non-interactive subset of this — each already carries a **text label**
  (good — satisfies "no colour alone"). They use `$border-radius-circular`
  (pill/circular variant). If chips ever become interactive (clickable filters),
  add focus ring + keyboard handling per the rules above.

---

## Date picker (`<input type="date">`)

Source: <https://www2.gov.bc.ca/gov/content/digital/design-system/components/date-picker>

### When to use
- Entering a **single** date (or date/time) — DOBs, scheduling, filters.
- **No ranges:** the component takes one value. For a range, use **two** pickers
  (a from/to pair) — which is exactly what `SubmissionListing.vue` does.

### Anatomy
Text label · bordered input with individually editable date segments · calendar
button that opens a popover calendar · optional auto-generated format helper text ·
optional description · optional time / time-zone segments · error message below.

### Date format
- Default locale **en-CA**; value is an **ISO 8601** string.
- Calendar popover selects the **date only** — time defaults to midnight and must be
  edited manually. Collect date and time separately unless a full string is required.

### Sizes
`Medium` (default) · `Small`.

### States
`hover` (border + cursor) · `focus` (offset ring on input **and** calendar button
**and** each calendar cell) · `filled` · `placeholder` · `disabled` (grey
input+button, not focusable) · `invalid` (red border + icon + message) · `read-only`
(visible, not editable).

### Rules / accessibility
- Always show a **visible label** (associated via `aria-labelledby`); hidden →
  `aria-label`. Don't hide the calendar button for most use-cases.
- Fully keyboard operable: type or ↑/↓ to change a segment, ←/→ to move between
  segments, and open/navigate/close the calendar by keyboard; `Tab` exits (no trap).
- Min **32 × 32 px** target; offset focus ring; no change/submit on focus alone.

### CES mapping
- We use the **native `<input type="date">`**, which gives us the
  segmented-editing, keyboard, and label semantics for free — a deliberate, valid
  simplification of the full BC Gov picker. Current usages:
  [`CourtListing.vue:9`](../../web/src/components/officer/CourtListing.vue#L9) (required),
  [`SubmissionListing.vue:117`](../../web/src/components/admin/SubmissionListing.vue#L117) +
  [`:121`](../../web/src/components/admin/SubmissionListing.vue#L121) (from/to **range pair** — correct per "no native ranges"),
  and [`SubmissionForm.vue:199`](../../web/src/components/officer/SubmissionForm.vue#L199) (disabled/read-only).
- **Gaps / to verify:** every date input has an associated visible `<label>` (the
  filter inputs in `SubmissionListing` should be checked); inputs share the common
  `.field` styling so border/focus/invalid/disabled match other controls; the
  from/to pair enforces from ≤ to. The native control already meets the 32px target
  and offset-focus rules via the browser, but our SCSS must not strip the outline.

---

## Token quick-reference

Aliases from [`_variables.scss`](../../web/src/styles/_variables.scss) relevant to controls:

| Concern | Token(s) |
|---|---|
| Primary button | `$color-primary`, `$color-primary-hover`, `$color-primary-disabled` |
| Danger button | `$color-danger`, `$color-danger-hover` |
| Borders | `$color-border`, `$color-border-medium`, `$border-width-small/medium/large` |
| Focus / status colours | `$color-danger`, `$color-success`, `$color-warning-*`, `$color-info-*` |
| Radius | `$border-radius-small/default/large/circular` |
| Spacing | `$padding-xsmall…xlarge`, `$margin-xsmall…xlarge` |
| Type | `$font-size-label/small-body/body/large-body` |
| Icons | `$icon-size-xsmall…xlarge` (14 / 16 / 20 / 24 / 32 px) |

> Raw tokens not aliased here are reachable via `t.$<token-name>` (the package is
> `@use`-d as `t`). Per project rules, **never hardcode** a colour/size/number —
> add an alias or use the token.

---

## Not yet documented

Add a section here (with the official page rules + CES mapping) **before** building
any of these:

Header · Footer · Subheader · Menu · Toggle button · **Checkbox group** ·
**Radio group** · Switch · **Number field** · Time field · Calendar ·
**Alert banner** · **Inline alert** · Progress indicators · **Tooltip** ·
Accordion group · Callout.

(Bolded = most likely needed next for CES forms/admin work.)

---

*Compiled from the BC Gov Design System component pages. When a page is updated,
re-pull and reconcile the relevant section here.*
