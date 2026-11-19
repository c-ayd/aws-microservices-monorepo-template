resource "aws_security_group" "rabbitmq_amqp_server" {
  name_prefix = "${var.project_name}-rabbitmq-amqp-server-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-rabbitmq-amqp-server-sg"
  }
}

resource "aws_security_group" "rabbitmq_amqp_client" {
  name_prefix = "${var.project_name}-rabbitmq-amqp-client-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-rabbitmq-amqp-client-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "rabbitmq_internal" {
  security_group_id = aws_security_group.rabbitmq_amqp_server.id
  referenced_security_group_id = aws_security_group.rabbitmq_amqp_server.id
  ip_protocol = "-1"
}

resource "aws_vpc_security_group_ingress_rule" "rabbitmq_amqps" {
  security_group_id = aws_security_group.rabbitmq_amqp_server.id
  referenced_security_group_id = aws_security_group.rabbitmq_amqp_client.id
  from_port = 5671
  to_port = 5671
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "rabbitmq_amqps" {
  security_group_id = aws_security_group.rabbitmq_amqp_client.id
  referenced_security_group_id = aws_security_group.rabbitmq_amqp_server.id
  from_port = 5671
  to_port = 5671
  ip_protocol = "tcp"
}

resource "aws_security_group" "rabbitmq_management_server" {
  name_prefix = "${var.project_name}-rabbitmq-management-server-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-rabbitmq-management-server-sg"
  }
}

resource "aws_security_group" "rabbitmq_management_client" {
  name_prefix = "${var.project_name}-rabbitmq-management-client-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-rabbitmq-management-client-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "rabbitmq_management_https" {
  security_group_id = aws_security_group.rabbitmq_management_server.id
  referenced_security_group_id = aws_security_group.rabbitmq_management_client.id
  from_port = 15671
  to_port = 15671
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "rabbitmq_management_https" {
  security_group_id = aws_security_group.rabbitmq_management_client.id
  referenced_security_group_id = aws_security_group.rabbitmq_management_server.id
  from_port = 15671
  to_port = 15671
  ip_protocol = "tcp"
}
