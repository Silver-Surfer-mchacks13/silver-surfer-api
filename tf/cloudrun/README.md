# Terraform Infrastructure for AspTemplate (GCP)

Simple Terraform configuration for deploying AspTemplate to Google Cloud Platform using Cloud Run.

## Architecture

- **Compute**: Cloud Run (fully managed container service with configurable CPU/memory)
- **Container Registry**: Artifact Registry for Docker images
- **Networking**: Cloud Run handles all networking automatically

## Prerequisites

1. **Google Cloud SDK** installed and configured
2. **Terraform** >= 1.0 installed
3. **Docker** installed (for building and pushing images)
4. GCP project with billing enabled
5. Enable required APIs:
   ```bash
   gcloud services enable run.googleapis.com
   gcloud services enable artifactregistry.googleapis.com
   gcloud services enable storage.googleapis.com
   ```

## Setup

### 0. Set Up Remote State Backend (GCS)

Terraform state is stored remotely in Google Cloud Storage for team collaboration and state management.

#### Create a GCS Bucket for State

```bash
# Replace YOUR_PROJECT_ID and YOUR_BUCKET_NAME with your values
# Bucket names must be globally unique
gsutil mb -p YOUR_PROJECT_ID -l us-central1 gs://YOUR_PROJECT_NAME-terraform-state

# Enable versioning for state file history
gsutil versioning set on gs://YOUR_PROJECT_NAME-terraform-state
```

#### Configure Backend

You have two options:

**Option A: Update providers.tf directly**
- Edit `tf/cloudrun/providers.tf` and replace `YOUR_PROJECT_NAME-terraform-state` with your actual bucket name

**Option B: Use a backend config file (recommended)**
- Copy `backend.hcl.example` to `backend.hcl`:
  ```bash
  cp backend.hcl.example backend.hcl
  ```
- Edit `backend.hcl` and set your bucket name
- Initialize with: `terraform init -backend-config=backend.hcl`

**Option C: Override during init**
```bash
terraform init -backend-config="bucket=your-bucket-name"
```

> **Note**: If you already have local state, Terraform will prompt you to migrate it to the remote backend during `terraform init`.

### 1. Configure Variables

Create a `terraform.tfvars` file:

```hcl
gcp_project_id = "your-project-id"
gcp_region     = "us-central1"
environment    = "dev"
project_name   = "asptemplate"

# Instance properties for WebApi
webapi_cpu         = "1"      # 1 vCPU
webapi_memory      = "1Gi"    # 1 GB
webapi_max_instances = 10
webapi_min_instances = 1

# Optional: Pre-built images (leave empty to build manually)
# webapi_image = "gcr.io/your-project-id/asptemplate-webapi:latest"

# Secrets (optional, can be set via environment variables or Secret Manager)
# database_connection_string = "Server=...;Database=...;..."
# jwt_secret = "your-secret-key"

# CORS (optional - set if you need to allow specific origins)
# cors_allowed_origins = "https://your-client-app.com"

# Email configuration
# email_resend_api_key = "your-resend-api-key"
# email_resend_domain = "your-domain.com"
```

### 2. Initialize Terraform

```bash
cd tf/cloudrun

# If using backend.hcl file:
terraform init -backend-config=backend.hcl

# Or if you updated providers.tf directly:
terraform init

# Or override bucket name during init:
terraform init -backend-config="bucket=your-bucket-name"
```

> **First-time setup**: If you have existing local state, Terraform will ask if you want to migrate it to the remote backend. Type `yes` to migrate.

### 3. Review the Plan

```bash
terraform plan
```

### 4. Apply the Configuration

```bash
terraform apply
```

## Building and Pushing Docker Images

After the infrastructure is created, build and push your Docker images:

### Authenticate with Artifact Registry

```bash
gcloud auth configure-docker ${var.gcp_region}-docker.pkg.dev
```

### Build and Push WebApi

```bash
# Get the repository URL from Terraform output
REPO_URL=$(terraform output -raw docker_repository_url)

# Build the image
docker build -f src/WebApi/Dockerfile -t $REPO_URL/webapi:latest .

# Push the image
docker push $REPO_URL/webapi:latest
```

### Configure CORS (Optional)

If you need to allow specific origins to access the API, update CORS settings:

1. Update `terraform.tfvars` with the CORS origin:
   ```hcl
   cors_allowed_origins = "https://your-client-app.com"
   ```

2. Apply again:
   ```bash
   terraform apply
   ```

### Update Cloud Run Service

After pushing new images, update the service:

```bash
# Update WebApi
gcloud run services update asptemplate-webapi \
  --image $REPO_URL/webapi:latest \
  --region us-central1
```

Or update the image in `terraform.tfvars` and run `terraform apply`.

## Configuration

### Adjusting Instance Properties

You can modify CPU and memory in `terraform.tfvars`:

- **CPU**: Valid values are "1", "2", "4", "6", "8" (vCPUs)
- **Memory**: Valid values like "128Mi", "256Mi", "512Mi", "1Gi", "2Gi", "4Gi", etc.
- **Memory limits**: Must be between 128Mi and 8Gi per vCPU

### Autoscaling

Configure min/max instances to control autoscaling:
- `min_instances`: Minimum number of instances (0 for scale-to-zero)
- `max_instances`: Maximum number of instances

## Outputs

After applying, get the service URL:

```bash
# Get service URL
terraform output webapi_url

# Get Docker repository URL
terraform output docker_repository_url
```

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

## Notes

- Cloud Run automatically handles HTTPS, load balancing, and scaling
- Services are publicly accessible by default (configure IAM for private access if needed)
- Container logs are automatically sent to Cloud Logging
- No VPC or networking configuration needed - Cloud Run handles it all
- For production, consider using Secret Manager for sensitive values instead of variables
