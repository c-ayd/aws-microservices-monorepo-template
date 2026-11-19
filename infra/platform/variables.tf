variable "project_name" {
  type = string
  default = "template"
}

variable "region" {
  type = string
  default = "eu-central-1"
}

variable "azs" {
  type = list(string)
  default = [
    "eu-central-1a",
    "eu-central-1b",
    "eu-central-1c"
  ]
}

variable "vpc_cidr" {
  type = string
  default = "10.0.0.0/16"
}

variable "public_subnet_cidrs" {
  type = map(string)
  default = {
    "eu-central-1a" = "10.0.0.0/24"
    "eu-central-1b" = "10.0.1.0/24"
    "eu-central-1c" = "10.0.2.0/24"
  }
}

variable "private_subnet_cidrs" {
  type = map(string)
  default = {
    "eu-central-1a" = "10.0.16.0/20"
    "eu-central-1b" = "10.0.32.0/20"
    "eu-central-1c" = "10.0.48.0/20"
  }
}

variable "default_http_port" {
  type = number
  default = 8080
}

variable "default_https_port" {
  type = number
  default = 8443
}

variable "default_grpcs_port" {
  type = number
  default = 8444
}
