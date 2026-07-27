export const CLASSIFICATION_EDIT_WINDOW_SECONDS = 10;
export const SAVE_INDICATOR_FADE_SECONDS = 5;
// Maximum length of a single description entry. Mirrors backend
// ClassificationConstants.DescriptionMaxLength.
export const DESCRIPTION_MAX_LENGTH = 1000;
// Characters of the first description shown inline on a condensed exhibit-list row
// before it is truncated with an ellipsis.
export const DESCRIPTION_PREVIEW_MAX_LENGTH = 200;
// The inline description textarea starts at this many rows and auto-grows to the max
// before it scrolls.
export const DESCRIPTION_INPUT_MIN_ROWS = 1;
export const DESCRIPTION_INPUT_MAX_ROWS = 8;
export const MARKED_MIN = 'A';
export const MARKED_MAX = 'Z';
export const ENTERED_MIN = 1;
export const ENTERED_MAX = 50;

// Evidence source device options. `value` is stored/validated by the API; `label` is the display text.
export const EVIDENCE_SOURCE_TYPES = [
  { value: 'BodyCam', label: 'Body Cam' },
  { value: 'DashCam', label: 'Dash Cam' },
  { value: 'Other', label: 'Other' },
] as const;

// Content-type prefixes the browser can render inline (used to gate the View button)
export const VIEWABLE_CONTENT_TYPE_PREFIXES: string[] = [
  'image/',
  'video/',
  'audio/',
  'application/pdf',
];
