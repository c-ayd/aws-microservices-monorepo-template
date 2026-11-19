terraform {
  backend "s3" {
    bucket = "template-tf-394768989038-eu-central-1-an"
    key = "infra/platform/terraform.tfstate"
    region = "eu-central-1"
    use_lockfile = true
  }
}
