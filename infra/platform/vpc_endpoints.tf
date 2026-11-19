resource "aws_vpc_endpoint" "s3" {
  vpc_id = aws_vpc.main.id
  service_name = "com.amazonaws.${var.region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids = [aws_route_table.private.id]

  tags = {
    Name = "${var.project_name}-s3-gateway"
  }
}

locals {
  vpc_endpoint_aws_services = [
    "secretsmanager",
    "kms",
    "logs",
    "ecr.api",
    "ecr.dkr",
    "ecs",
    "ecs.agent",
    "ecs.telemetry"
  ]
}

resource "aws_vpc_endpoint" "aws_services" {
  for_each = toset(local.vpc_endpoint_aws_services)

  vpc_id = aws_vpc.main.id
  service_name = "com.amazonaws.${var.region}.${each.key}"
  vpc_endpoint_type = "Interface"
  subnet_ids = [for subnet in aws_subnet.private : subnet.id]
  security_group_ids = [aws_security_group.vpc_endpoints.id]
  private_dns_enabled = true

  tags = {
    Name = "${var.project_name}-vpc-endpoints-${each.key}"
  }
}
