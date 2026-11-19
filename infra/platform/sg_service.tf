resource "aws_security_group" "service" {
  name_prefix = "${var.project_name}-service-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-service-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "service_api_gateway_https" {
  security_group_id = aws_security_group.service.id
  referenced_security_group_id = aws_security_group.api_gateway.id
  from_port = 8443
  to_port = 8443
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "service_grpcs" {
  security_group_id = aws_security_group.service.id
  referenced_security_group_id = aws_security_group.service.id
  from_port = 8444
  to_port = 8444
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "service_grpcs" {
  security_group_id = aws_security_group.service.id
  referenced_security_group_id = aws_security_group.service.id
  from_port = 8444
  to_port = 8444
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "service_aws_https" {
  security_group_id = aws_security_group.service.id
  referenced_security_group_id = aws_security_group.vpc_endpoints.id
  from_port = 443
  to_port = 443
  ip_protocol = "tcp"
}
