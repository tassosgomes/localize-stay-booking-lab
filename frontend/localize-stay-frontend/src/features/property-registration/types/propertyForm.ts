export const PROPERTY_FORM_FIELDS = ['hostReferenceId', 'name', 'location'] as const;

export type PropertyFormField = (typeof PROPERTY_FORM_FIELDS)[number];

export interface PropertyFormValues {
  hostReferenceId: string;
  name: string;
  location: string;
}

export type PropertyFormErrors = Partial<Record<PropertyFormField, string>>;

export interface FieldProblem {
  field: string;
  message: string;
}

export const EMPTY_PROPERTY_FORM_VALUES: PropertyFormValues = {
  hostReferenceId: '',
  name: '',
  location: '',
};

export const PROPERTY_NAME_MAX_LENGTH = 120;
export const PROPERTY_LOCATION_MAX_LENGTH = 500;

export const PROPERTY_FIELD_INPUT_IDS: Record<PropertyFormField, string> = {
  hostReferenceId: 'property-host-reference-id',
  name: 'property-name',
  location: 'property-location',
};

export const PROPERTY_FIELD_ERROR_IDS: Record<PropertyFormField, string> = {
  hostReferenceId: 'property-host-reference-id-error',
  name: 'property-name-error',
  location: 'property-location-error',
};

export const PROPERTY_FIELD_HINT_IDS: Record<PropertyFormField, string> = {
  hostReferenceId: 'property-host-reference-id-hint',
  name: 'property-name-hint',
  location: 'property-location-hint',
};
