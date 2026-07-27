# BC Gov Design System — Typography Rules (CES adaptation)

> **Purpose.** A working reference for the BC Gov typography standards, mapped to
> **how CES actually styles text**: native HTML + SCSS + `@bcgov/design-tokens`.
> Read this before setting any `font-size`, `font-weight`, `line-height`, or
> adding a heading.
>
> **Source.** BC Gov Typography foundation
> (<https://www2.gov.bc.ca/gov/content/digital/design-system/foundations/typography>).
> Tokens referenced below are the SCSS aliases in
> [`web/src/styles/_variables.scss`](../../web/src/styles/_variables.scss); see also
> the [design-tokens section in CLAUDE.md](../../CLAUDE.md).

---

## The standard (numeric)

**Typeface:** BC Sans (a Noto Sans variant; supports Indigenous characters &
syllabics). Loaded via `@bcgov/bc-sans` in [`main.ts`](../../web/src/main.ts) and
applied globally in [`_base.scss`](../../web/src/styles/_base.scss). CES fallback
stack (`$font-family-base`): `"BC Sans", "Noto Sans", Verdana, Arial, sans-serif`.

**Type scale** — the *only* sanctioned text sizes. Each has a design token.

Heading levels get their size, weight (700), and line-height (1.5) automatically
from the global `h1`–`h6` rules in `_base.scss` — you rarely set these by hand.

| Style | Weight | rem | px | Line height | CES alias |
|---|---|---|---|---|---|
| Heading 1 | 700 | 2.25 | 36 | 1.5 | `$font-size-h1` |
| Heading 2 | 700 | 2 | 32 | 1.5 | `$font-size-h2` |
| Heading 3 | 700 | 1.75 | 28 | 1.5 | `$font-size-h3` |
| Heading 4 | 700 | 1.5 | 24 | 1.5 | `$font-size-h4` |
| Heading 5 | 700 | 1.25 | 20 | 1.5 | `$font-size-h5` |
| Heading 6 | 700 | 1.125 | 18 | 1.5 | `$font-size-h6` *(no h6 token upstream; aliased to Large Body's 1.125rem)* |
| Large Body | 400 | 1.125 | 18 | 1.5 | `$font-size-large-body` |
| Body (default) | 400 | 1 | 16 | 1.5 | `$font-size-body` |
| Small Body | 400 | 0.875 | 14 | 1.25 | `$font-size-small-body` |
| Label | 400 | 0.75 | 12 | 1.25 | `$font-size-label` |

Weight/line-height aliases: `$font-weight-regular` (400), `$font-weight-bold` (700),
`$line-height-base` (1.5), `$line-height-tight` (1.25).

**Font weights:** BC Sans ships **only** Light `300`, Regular `400`, Bold `700`
(all with italics). The typescale uses **400** (body) and **700** (headings) only.
Any other weight (`500`, `600`, …) is *synthesized* by the browser and renders
inconsistently — **do not use it.**

## Rules

1. **Only use scale sizes, via tokens.** Never invent a `font-size`. If a value
   isn't in the table above, it's wrong — pick the nearest scale step and use its
   token/alias. This is also the project's no-magic-numbers rule.
2. **Always `rem`, never `px`, for text.** Required for user text-resize / a11y.
3. **Only weights 400 or 700.** No `500`/`600`. Use `700` for emphasis/headings.
4. **Body minimum 16px (1rem).** The baseline body size is `$font-size-body`.
   Below that, only the sanctioned `small-body` (14px) and `label` (12px) tokens
   are allowed, and only for genuinely secondary/label text — never main content.
5. **Line height:** `1.5` for body and headings; `1.25` for small-body and label.
6. **Heading hierarchy is semantic and sequential.** One `<h1>` per page (the
   page title). Never skip levels (no `<h1>` → `<h4>`). Choose the heading level
   for document structure, then style its size with the matching `h*` token — do
   **not** pick a heading level for its default size.
7. **Never use size or weight alone to convey meaning** (WCAG). Pair with text,
   colour *and* another cue.
8. **Contrast:** ≥ 4.5:1 for text < 18pt (body, H3–H6); ≥ 3:1 for large text
   (H1–H2). Verify pairs against the tokens in `_variables.scss`.

---

## Audit history

The deviations below were found on **2026-07-08** and **fixed the same day**
(build + all 104 frontend tests green). Kept as a record; the rules above are
binding for new work.

### ✅ Now conforming
- BC Sans applied globally (`body` + `body .v-application`) in `_base.scss`.
- Global `h1`–`h6` styles apply the BC Gov type scale (size per level, weight 700,
  line-height 1.5); `body` gets `line-height: 1.5`.
- All `font-size` / `font-weight` / `line-height` values go through the
  `$font-size-*`, `$font-weight-*`, `$line-height-*` aliases — **no numeric text
  literals remain** in `web/src/styles/*`.

### Resolved deviations
- **Off-scale font sizes** (27 values: `0.95`/`0.9`/`0.85`/`0.82`/`0.8`/`0.72`/`0.7`/`0.68rem`,
  one `18px`) → snapped to the nearest token. Rule of thumb used:
  `0.95→body`, `0.9/0.85/0.82→small-body`, `0.8/0.72/0.7/0.68→label`; interactive
  control text (classification/description inputs) went to `small-body` (14px) for
  usability rather than `label`; the dropzone `.icon` `18px` → `$icon-size-medium`.
- **Off-scale font weights** (`600`×9, `500`×3, plus stray `400`/`700` literals) →
  all now `$font-weight-bold` / `$font-weight-regular`. Note: both 500 and 600
  collapse to **700**, so some semibold text is now visibly bolder — intended.
- **Heading typescale not applied** → global `h1`–`h6` rules added; `$font-size-h1..h6`
  aliases created (h6 aliased to Large Body, no upstream token).
- **Skipped / non-page-title headings** → fixed:
  - `SubmissionForm.vue`: `h1` → `h4` **→ now `h1` → `h2`** ("Prior Exhibits").
  - `SubmissionReview.vue`: `h1` → `h3`×2 **→ now `h1` → `h2`×2** (also a skip, found during the fix).
  - `DevDashboard.vue`: `h1` → `h3` **→ now `h1` → `h2`** (dev-only tool).
  - `CourtListing.vue` (`h2`→`h1`) and `LoginView.vue` (`h3`→`h1`): promoted the
    lone page/card title to `h1` (one h1 per page).

### Judgment calls (revisit if the visuals aren't right)
- **Larger headings.** Section headers that had no explicit size now render at the
  full scale — e.g. `SubmissionReview` "Tickets" / "Submitted Evidence" at **32px**
  (h2), page `h1`s at **36px**, `ExhibitDetailModal` title at 32px / its 5 section
  `h3`s at 28px. This is the BC Gov scale; if any read as too heavy, the fix is a
  lower heading level (without skipping) or an on-scale compact override — **not**
  an off-scale size.
- **Compact override precedent.** `SubmissionForm`'s "Prior Exhibits" h2 is pinned
  to `$font-size-large-body` (18px) rather than the full 32px, matching the
  existing `.modal-title` (AppModal) and `.exhibit-history-dialog h3` pattern: a
  correct semantic level sized down with an **on-scale token** where a full heading
  is too heavy for a minor in-flow label.
