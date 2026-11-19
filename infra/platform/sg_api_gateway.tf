resource "aws_security_group" "api_gateway" {
  name_prefix = "${var.project_name}-api-gateway-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-api-gateway-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "api_gateway_alb_https" {
  security_group_id = aws_security_group.api_gateway.id
  referenced_security_group_id = aws_security_group.alb.id
  from_port = 8443
  to_port = 8443
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "api_gateway_services_https" {
  security_group_id = aws_security_group.api_gateway.id
  referenced_security_group_id = aws_security_group.service.id
  from_port = 8443
  to_port = 8443
  ip_protocol = "tcp"
}
