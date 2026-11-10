export type GenericTemplateType =
  | 'machine'
  | 'component'
  | 'delegation'
  | 'telemetry'
  | 'ticket'
  | 'asset'
  | 'custom'

export type TemplateFieldType =
  | 'string'
  | 'number'
  | 'boolean'
  | 'select'
  | 'multiselect'
  | 'textarea'
  | 'code'
  | 'json'
  | 'tags'

export interface TemplateFieldOption {
  label: string
  value: string
  description?: string
}

export interface TemplateFieldDefinition {
  key: string
  label: string
  fieldType: TemplateFieldType
  required?: boolean
  defaultValue?: any
  description?: string
  placeholder?: string
  unit?: string
  options?: TemplateFieldOption[]
  min?: number
  max?: number
  step?: number
  validationRegex?: string
  section?: string
  order?: number
  isInherited?: boolean
  inheritedFromCode?: string
}

export interface TemplateLineageItem {
  id: string
  code: string
  name: string
}

export interface GenericTemplate {
  id: string
  templateType: GenericTemplateType | string
  code: string
  name: string
  description?: string
  category: string
  icon?: string
  version: string
  extendsTemplateId?: string | null
  extendsTemplateCode?: string | null
  extendsTemplateName?: string | null
  tags: string[]
  schema: TemplateFieldDefinition[]
  defaultValues: Record<string, any>
  uiConfig?: Record<string, any> | null
  isActive: boolean
  isBuiltin: boolean
  organizationId?: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateGenericTemplateInput {
  templateType: GenericTemplateType | string
  code: string
  name: string
  description?: string
  category?: string
  icon?: string
  version?: string
  extendsTemplateId?: string | null
  tags?: string[]
  schema?: TemplateFieldDefinition[]
  defaultValues?: Record<string, any>
  uiConfig?: Record<string, any> | null
  isActive?: boolean
}

export interface UpdateGenericTemplateInput {
  code?: string
  name?: string
  description?: string
  category?: string
  icon?: string
  version?: string
  extendsTemplateId?: string | null
  tags?: string[]
  schema?: TemplateFieldDefinition[]
  defaultValues?: Record<string, any>
  uiConfig?: Record<string, any> | null
  isActive?: boolean
}

export interface DeriveTemplateInput {
  newCode: string
  newName: string
  description?: string
  category?: string
  additionalSchema?: TemplateFieldDefinition[]
  defaultOverrides?: Record<string, any>
}

export interface TemplateResolutionResult {
  templateId: string
  code: string
  name: string
  lineage: TemplateLineageItem[]
  effectiveSchema: TemplateFieldDefinition[]
  effectiveDefaultValues: Record<string, any>
}

export interface TemplateInstantiationResult {
  success: boolean
  instanceData: Record<string, any>
  errors: string[]
  appliedSchema: TemplateFieldDefinition[]
}

export interface TemplateValidationResult {
  isValid: boolean
  errors: string[]
  warnings: string[]
}

export interface TemplateTypeDefinition {
  type: string
  label: string
  description: string
  icon: string
  defaultCategories: string[]
}
