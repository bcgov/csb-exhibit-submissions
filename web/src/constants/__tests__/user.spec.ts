import {
  OFFICER_NUMBER_MAX_LENGTH,
  OFFICER_NUMBER_PATTERN,
  sanitizeOfficerNumber,
} from '@/constants/user';

describe('sanitizeOfficerNumber', () => {
  it('keeps letters, numbers, dashes and periods', () => {
    expect(sanitizeOfficerNumber('PC-12.34ab')).toBe('PC-12.34ab');
  });

  it('strips spaces', () => {
    expect(sanitizeOfficerNumber('AB 12 34')).toBe('AB1234');
  });

  it('strips other punctuation and symbols', () => {
    expect(sanitizeOfficerNumber('AB/12_34#')).toBe('AB1234');
  });

  it('clamps to the maximum length', () => {
    const result = sanitizeOfficerNumber('A'.repeat(OFFICER_NUMBER_MAX_LENGTH + 10));

    expect(result).toHaveLength(OFFICER_NUMBER_MAX_LENGTH);
  });

  it('clamps after stripping, so disallowed characters do not consume the budget', () => {
    // 30 valid characters interleaved with spaces must survive intact.
    const raw = 'A'.repeat(OFFICER_NUMBER_MAX_LENGTH).split('').join(' ');

    expect(sanitizeOfficerNumber(raw)).toBe('A'.repeat(OFFICER_NUMBER_MAX_LENGTH));
  });

  it('returns an empty string when nothing is allowed through', () => {
    expect(sanitizeOfficerNumber('   ')).toBe('');
  });

  it('produces a value the shared pattern accepts', () => {
    // The stripped output is what the API is asked to store, so it must satisfy the
    // allowlist the API validates against.
    expect(OFFICER_NUMBER_PATTERN.test(sanitizeOfficerNumber('PC 12/34'))).toBe(true);
  });
});
