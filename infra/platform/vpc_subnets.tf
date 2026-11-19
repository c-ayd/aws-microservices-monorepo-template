resource "aws_vpc" "main" {
  cidr_block = var.vpc_cidr
  assign_generated_ipv6_cidr_block = false
  enable_dns_support = true
  enable_dns_hostnames = true
  
  tags = {
    Name = "${var.project_name}-vpc"
  }
}

resource "aws_subnet" "public" {
  for_each = toset(var.azs)

  vpc_id = aws_vpc.main.id
  availability_zone = each.value
  cidr_block = var.public_subnet_cidrs[each.value]

  tags = {
    Name = "${var.project_name}-public-subnet-${each.value}"
  }
}

resource "aws_subnet" "private" {
  for_each = toset(var.azs)

  vpc_id = aws_vpc.main.id
  availability_zone = each.value
  cidr_block = var.private_subnet_cidrs[each.value]

  tags = {
    Name = "${var.project_name}-private-subnet-${each.value}"
  }
}
