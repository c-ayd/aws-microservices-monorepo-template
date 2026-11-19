resource "aws_security_group" "alb" {
  name_prefix = "${var.project_name}-alb-"
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-alb-sg"
  }
}

resource "aws_vpc_security_group_ingress_rule" "alb_internet_https" {
  security_group_id = aws_security_group.alb.id
  cidr_ipv4 = "0.0.0.0/0"
  from_port = 443
  to_port = 443
  ip_protocol = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "alb_api_gateway_https" {
  security_group_id = aws_security_group.alb.id
  referenced_security_group_id = aws_security_group.api_gateway.id
  from_port = 8443
  to_port = 8443
  ip_protocol = "tcp"
}
