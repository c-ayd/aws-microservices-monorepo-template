resource "aws_security_group" "postgresql_server" {
  name_prefix = "${var.project_name}-postgresql-server-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-postgresql-server-sg"
  }
}

resource "aws_security_group" "postgresql_client" {
  name_prefix = "${var.project_name}-postgresql-client-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-postgresql-client-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "postgresql_ssl" {
  security_group_id = aws_security_group.postgresql_server.id
  referenced_security_group_id = aws_security_group.postgresql_client.id
  from_port = 5432
  to_port = 5432
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "postgresql_ssl" {
  security_group_id = aws_security_group.postgresql_client.id
  referenced_security_group_id = aws_security_group.postgresql_server.id
  from_port = 5432
  to_port = 5432
  ip_protocol = "tcp"
}
