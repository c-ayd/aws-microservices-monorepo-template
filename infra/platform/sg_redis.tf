resource "aws_security_group" "redis_server" {
  name_prefix = "${var.project_name}-redis-server-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-redis-server-sg"
  }
}

resource "aws_security_group" "redis_client" {
  name_prefix = "${var.project_name}-redis-client-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-redis-client-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "redis_ssl" {
  security_group_id = aws_security_group.redis_server.id
  referenced_security_group_id = aws_security_group.redis_client.id
  from_port = 6379
  to_port = 6379
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "redis_ssl" {
  security_group_id = aws_security_group.redis_client.id
  referenced_security_group_id = aws_security_group.redis_server.id
  from_port = 6379
  to_port = 6379
  ip_protocol = "tcp"
}
