export function pad2(value: number): string {
  return value.toString().padStart(2, '0');
}

export function pad4(value: number): string {
  return value.toString().padStart(4, '0');
}

/** Parses an <input type="number"> value, treating an empty string as "cleared" (null) rather
 * than 0 - matching MainViewModel's nullable decimal fields (e.g. TargetX), which distinguish
 * "no value entered" from an actual zero. */
export function parseOptionalNumber(raw: string): number | null {
  if (raw === '') {
    return null;
  }
  const parsed = Number(raw);
  return Number.isNaN(parsed) ? null : parsed;
}
