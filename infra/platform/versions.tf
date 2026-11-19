terraform {
  required_version = ">= 1.11.0, < 2.0.0" // For S3-based tfstate locking

  required_providers {
    aws = {
      source = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}
