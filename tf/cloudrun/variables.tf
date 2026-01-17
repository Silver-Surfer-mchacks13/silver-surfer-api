variable "gcp_project_id" {
  description = "GCP Project ID"
  type        = string
  default     = "YOUR_PROJECT_ID"
}

variable "gcp_region" {
  description = "GCP region for resources"
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}


########################################
# Container Images
########################################

variable "webapi_image" {
  description = "Docker image for WebApi (e.g., gcr.io/PROJECT_ID/asptemplate-webapi:latest)"
  type        = string
  default     = ""
}

########################################
# App configuration / secrets
########################################

variable "database_connection_string" {
  description = "Database connection string"
  type        = string
  sensitive   = true
  default     = ""
}

variable "jwt_secret" {
  description = "JWT secret key"
  type        = string
  sensitive   = true
  default     = ""
}

variable "cors_allowed_origins" {
  description = "CORS allowed origins (single origin, or comma-separated)"
  type        = string
  default     = ""
}

variable "email_resend_api_key" {
  description = "Resend API key for email service"
  type        = string
  sensitive   = true
  default     = ""
}

variable "email_resend_domain" {
  description = "Resend domain for email service"
  type        = string
  default     = ""
}

########################################
# OAuth configuration
########################################

variable "oauth_google_client_id" {
  description = "Google OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_microsoft_client_id" {
  description = "Microsoft OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_microsoft_tenant_id" {
  description = "Microsoft OAuth Tenant ID (default: 'common')"
  type        = string
  sensitive   = false
  default     = "common"
}

variable "oauth_github_client_id" {
  description = "GitHub OAuth Client ID"
  type        = string
  sensitive   = false
  default     = ""
}

variable "oauth_github_client_secret" {
  description = "GitHub OAuth Client Secret"
  type        = string
  sensitive   = true
  default     = ""
}
