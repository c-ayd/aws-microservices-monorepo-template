resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-igw"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = {
    Name = "${var.project_name}-public-rt"
  }
}

resource "aws_route_table_association" "public" {
  for_each = aws_subnet.public

  route_table_id = aws_route_table.public.id
  subnet_id = each.value.id
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-private-rt"
  }
}

resource "aws_route_table_association" "private" {
  for_each = aws_subnet.private

  route_table_id = aws_route_table.private.id
  subnet_id = each.value.id
}

resource "aws_lb" "alb" {
  name = "${var.project_name}-alb"
  load_balancer_type = "application"
  internal = false
  ip_address_type = "ipv4"
  subnets = [for subnet in aws_subnet.public : subnet.id]
  security_groups = [aws_security_group.alb.id]
}

resource "aws_lb_target_group" "ecs" {
  name = "${var.project_name}-ecs-tg"
  vpc_id = aws_vpc.main.id
  target_type = "ip"
  protocol = "HTTPS"
  protocol_version = "HTTP1"
  ip_address_type = "ipv4"
  port = var.default_https_port

  health_check {
    path = "/health"
    protocol = "HTTPS"
    port = "traffic-port"
    healthy_threshold = 3
    unhealthy_threshold = 2
    timeout = 5
    interval = 30
  }
}
