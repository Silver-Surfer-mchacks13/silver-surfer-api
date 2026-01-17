########################################
# Service Account for Cloud Run
########################################

resource "google_service_account" "webapi_sa" {
  account_id   = "silver-surfer-webapi-sa"
  display_name = "Silver Surfer WebApi Service Account"
  description  = "Service account for Cloud Run WebApi service with Vertex AI access"
}

# Grant Vertex AI User role on the Gemini project (or main project if same)
resource "google_project_iam_member" "webapi_vertex_ai_user" {
  project = var.gemini_project_id != "" ? var.gemini_project_id : var.gcp_project_id
  role    = "roles/aiplatform.user"
  member  = "serviceAccount:${google_service_account.webapi_sa.email}"
}

########################################
# Cloud Run Service: WebApi
########################################

resource "google_cloud_run_service" "silver-surfer-webapi" {
  name     = "silver-surfer-webapi"
  location = local.cloud_run_location

  # lifecycle {
  #   prevent_destroy = true
  # }

  template {
    spec {
      service_account_name = google_service_account.webapi_sa.email

      containers {
        image = var.webapi_image != "" ? var.webapi_image : "gcr.io/${var.gcp_project_id}/silver-surfer-webapi:latest"

        ports {
          container_port = 8080
        }

        resources {
          limits = {
            cpu    = "0.5"   # Reduced from 1 to 0.5 vCPU for cost savings
            memory = "512Mi" # Reduced from 1Gi to 512Mi (max for 0.5 vCPU)
          }
        }

        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment == "prod" ? "Production" : "Development"
        }

        env {
          name  = "OTEL_SERVICE_NAME"
          value = "WebApi"
        }

        # Database connection string
        dynamic "env" {
          for_each = var.database_connection_string != "" ? [1] : []
          content {
            name  = "ConnectionStrings__DefaultConnection"
            value = var.database_connection_string
          }
        }

        # JWT Secret
        dynamic "env" {
          for_each = var.jwt_secret != "" ? [1] : []
          content {
            name  = "Jwt__Secret"
            value = var.jwt_secret
          }
        }

        # CORS - allow origins (comma-separated if multiple)
        # Set as a single string value (not array index)
        dynamic "env" {
          for_each = var.cors_allowed_origins != "" ? [1] : []
          content {
            name  = "Cors__AllowedOrigins"
            value = var.cors_allowed_origins
          }
        }

        # Email Resend API Key
        dynamic "env" {
          for_each = var.email_resend_api_key != "" ? [1] : []
          content {
            name  = "Email__Resend__ApiKey"
            value = var.email_resend_api_key
          }
        }

        # Email Resend Domain
        dynamic "env" {
          for_each = var.email_resend_domain != "" ? [1] : []
          content {
            name  = "Email__Resend__Domain"
            value = var.email_resend_domain
          }
        }

        # OAuth - Google
        dynamic "env" {
          for_each = var.oauth_google_client_id != "" ? [1] : []
          content {
            name  = "OAuth__Google__ClientId"
            value = var.oauth_google_client_id
          }
        }

        # OAuth - Microsoft
        dynamic "env" {
          for_each = var.oauth_microsoft_client_id != "" ? [1] : []
          content {
            name  = "OAuth__Microsoft__ClientId"
            value = var.oauth_microsoft_client_id
          }
        }

        dynamic "env" {
          for_each = var.oauth_microsoft_tenant_id != "" ? [1] : []
          content {
            name  = "OAuth__Microsoft__TenantId"
            value = var.oauth_microsoft_tenant_id
          }
        }

        # OAuth - GitHub
        dynamic "env" {
          for_each = var.oauth_github_client_id != "" ? [1] : []
          content {
            name  = "OAuth__GitHub__ClientId"
            value = var.oauth_github_client_id
          }
        }

        dynamic "env" {
          for_each = var.oauth_github_client_secret != "" ? [1] : []
          content {
            name  = "OAuth__GitHub__ClientSecret"
            value = var.oauth_github_client_secret
          }
        }

        # Gemini / Vertex AI - Project ID
        dynamic "env" {
          for_each = var.gemini_project_id != "" ? [1] : []
          content {
            name  = "Gemini__ProjectId"
            value = var.gemini_project_id
          }
        }
      }

      container_concurrency = 1  # Must be 1 when using < 1 vCPU
      timeout_seconds       = 60 # Reduced from 300s to 60s for cost savings
    }

    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "2" # Reduced from 10 to 2 for dev/test
        "autoscaling.knative.dev/minScale" = "0" # Scale to zero when idle (saves money)
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}

########################################
# IAM: Public access for WebApi service
########################################

resource "google_cloud_run_service_iam_member" "webapi_public" {
  service  = google_cloud_run_service.silver-surfer-webapi.name
  location = google_cloud_run_service.silver-surfer-webapi.location
  role     = "roles/run.invoker"
  member   = "allUsers"
}
