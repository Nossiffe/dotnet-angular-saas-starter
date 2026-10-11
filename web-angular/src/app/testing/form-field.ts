/**
 * What a Material form field shows of its errors, read from the DOM. mat-form-field projects into
 * its subscript, under the field, a mat-error that is its direct child or the single root of a
 * block; a mat-error nested deeper (inside @for inside @if, say) lands in the field's box. In error
 * state, the input's aria-describedby names every mat-error of the field, wherever it landed.
 */
export interface FieldErrors {
  /** The messages under the field. */
  under: string[];
  /** The messages elsewhere in the field, inside its box. */
  elsewhere: string[];
  /** The messages the input names in aria-describedby. */
  describing: string[];
  /** The field's error state (its red outline). */
  invalid: boolean;
}

export function fieldErrors(input: HTMLElement): FieldErrors {
  const field = input.closest('mat-form-field');
  if (!field) throw new Error('The input is not in a mat-form-field.');
  const errors = Array.from(field.querySelectorAll('mat-error'));
  const text = (error: Element) => error.textContent?.trim() ?? '';
  const isUnder = (error: Element) =>
    error.closest('.mat-mdc-form-field-subscript-wrapper') !== null;
  const describedBy = (input.getAttribute('aria-describedby') ?? '').split(/\s+/);
  return {
    under: errors.filter(isUnder).map(text),
    elsewhere: errors.filter((error) => !isUnder(error)).map(text),
    describing: errors.filter((error) => describedBy.includes(error.id)).map(text),
    invalid: field.classList.contains('mat-form-field-invalid'),
  };
}

/** The errors of a field that shows the given messages, under it and as its description. */
export function showing(...messages: string[]): FieldErrors {
  return {
    under: messages,
    elsewhere: [],
    describing: messages,
    invalid: messages.length > 0,
  };
}

/** Types a value as the user would: the input event, then leaving the field. */
export function typeAndLeave(input: HTMLInputElement, value: string): void {
  input.dispatchEvent(new Event('focus'));
  input.value = value;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('blur'));
}

/** Enters the field and leaves it without typing. */
export function touch(input: HTMLInputElement): void {
  input.dispatchEvent(new Event('focus'));
  input.dispatchEvent(new Event('blur'));
}
