from cryptography import x509
from cryptography.x509.oid import NameOID, ExtendedKeyUsageOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from datetime import datetime, timedelta, timezone
import argparse
from pathlib import Path
import os

def main(args, root_folder):
    if args.int_ca_key is None:
        print('Intermediate CA key path is not provided.')
        return
    if args.int_ca_cert is None:
        print('Intermediate CA certificate path is not provided.')
        return
    if args.password_int is None:
        print('Password for intermediate CA is not provided.')
        return
    if args.output is None:
        print('Output directory is not provided.')
        return
    if args.service_name is None:
        print('Service name is not provided.')
        return
    if args.dns_names is None:
        print('DNS names are not provided.')
        return
    if args.cert_type is None:
        print('Certificate type is not provided.')
        return

    output_path = os.path.join(root_folder, args.output)
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    organization = 'localhost'
    country = 'DE'

    intermediate_private_key, intermediate_cert = load_intermediate_ca(intermediate_ca_key_path=os.path.join(root_folder, args.int_ca_key),
                                                                       intermediate_ca_cert_path=os.path.join(root_folder, args.int_ca_cert),
                                                                       password=args.password_int)

    if args.cert_type == 'both' or args.cert_type == 'server':
        _, _ = issue_server_certificate(output_path=output_path,
                                        intermediate_private_key=intermediate_private_key,
                                        intermediate_cert=intermediate_cert,
                                        organization=organization,
                                        country=country,
                                        service_name=args.service_name,
                                        dns_names=args.dns_names,
                                        valid_days=90)

        print(f'Server certificate for {args.service_name} is issued.')

    if args.cert_type == 'both' or args.cert_type == 'client':
        _, _ = issue_client_certificate(output_path=output_path,
                                        intermediate_private_key=intermediate_private_key,
                                        intermediate_cert=intermediate_cert,
                                        organization=organization,
                                        country=country,
                                        service_name=args.service_name,
                                        valid_days=90)

        print(f'Client certificate for {args.service_name} is issued.')

def load_intermediate_ca(intermediate_ca_key_path,
                         intermediate_ca_cert_path,
                         password):
    with open(intermediate_ca_key_path, 'rb') as file:
        intermediate_private_key = serialization.load_pem_private_key(file.read(), password=password.encode('utf-8'))

    with open(intermediate_ca_cert_path, 'rb') as file:
        intermediate_cert = x509.load_pem_x509_certificate(file.read())

    return intermediate_private_key, intermediate_cert

def issue_server_certificate(output_path,
                             intermediate_private_key,
                             intermediate_cert,
                             organization,
                             country,
                             service_name,
                             dns_names,
                             valid_days):
    server_private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048,
    )

    subject = x509.Name([
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization),
        x509.NameAttribute(NameOID.COUNTRY_NAME, country),
        x509.NameAttribute(NameOID.COMMON_NAME, service_name),
    ])

    now = datetime.now(timezone.utc)
    server_cert = (
        x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(intermediate_cert.subject)
            .public_key(server_private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5))
            .not_valid_after(now + timedelta(days=valid_days))
            
            .add_extension(
                x509.BasicConstraints(ca=False, path_length=None),
                critical=True,
            )
            
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    key_encipherment=True,
                    key_cert_sign=False,
                    crl_sign=False,
                    content_commitment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
            
            .add_extension(
                x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH]),
                critical=True,
            )
            
            .add_extension(
                x509.SubjectAlternativeName([x509.DNSName(dns_name) for dns_name in dns_names]),
                critical=False,
            )
            
            .add_extension(
                x509.SubjectKeyIdentifier.from_public_key(server_private_key.public_key()),
                critical=False,
            )
            
            .add_extension(
                x509.AuthorityKeyIdentifier.from_issuer_subject_key_identifier(
                    intermediate_cert.extensions.get_extension_for_class(x509.SubjectKeyIdentifier).value
                ),
                critical=False,
            )
            
            .sign(intermediate_private_key, hashes.SHA256())
    )

    save_path = os.path.join(output_path, service_name)
    if not os.path.exists(save_path):
        os.makedirs(save_path)

    with open(os.path.join(save_path, f'{service_name}-server.key'), 'wb') as file:
        file.write(server_private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()))

    bundle = b''.join([
        server_cert.public_bytes(serialization.Encoding.PEM),
        intermediate_cert.public_bytes(serialization.Encoding.PEM)])
    with open(os.path.join(save_path, f'{service_name}-server.crt'), 'wb') as file:
        file.write(bundle)

    return server_private_key, server_cert

def issue_client_certificate(output_path,
                             intermediate_private_key,
                             intermediate_cert,
                             organization,
                             country,
                             service_name,
                             valid_days):
    client_private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048,
    )
    
    subject = x509.Name([
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization),
        x509.NameAttribute(NameOID.COUNTRY_NAME, country),
        x509.NameAttribute(NameOID.COMMON_NAME, service_name),
    ])
    
    now = datetime.now(timezone.utc)
    client_cert = (
        x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(intermediate_cert.subject)
            .public_key(client_private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5))
            .not_valid_after(now + timedelta(days=valid_days))
            
            .add_extension(
                x509.BasicConstraints(ca=False, path_length=None),
                critical=True,
            )
            
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    key_encipherment=True,
                    key_cert_sign=False,
                    crl_sign=False,
                    content_commitment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
            
            .add_extension(
                x509.ExtendedKeyUsage([ExtendedKeyUsageOID.CLIENT_AUTH]),
                critical=True,
            )

            .add_extension(
                x509.SubjectAlternativeName([x509.RFC822Name(f'{service_name}@{organization}')]),
                critical=False,
            )
            
            .add_extension(
                x509.SubjectKeyIdentifier.from_public_key(client_private_key.public_key()),
                critical=False,
            )
            
            .add_extension(
                x509.AuthorityKeyIdentifier.from_issuer_subject_key_identifier(
                    intermediate_cert.extensions.get_extension_for_class(x509.SubjectKeyIdentifier).value
                ),
                critical=False,
            )
            
            .sign(intermediate_private_key, hashes.SHA256())
    )

    save_path = os.path.join(output_path, service_name)
    if not os.path.exists(save_path):
        os.makedirs(save_path)

    with open(os.path.join(save_path, f'{service_name}-client.key'), 'wb') as file:
        file.write(client_private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()))

    bundle = b''.join([
        client_cert.public_bytes(serialization.Encoding.PEM),
        intermediate_cert.public_bytes(serialization.Encoding.PEM)])    
    with open(os.path.join(save_path, f'{service_name}-client.crt'), 'wb') as file:
        file.write(bundle)

    return client_private_key, client_cert

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--int-ca-key', type = str)
    parser.add_argument('--int-ca-cert', type = str)
    parser.add_argument('--password-int', type = str)
    parser.add_argument('--output', type = str)
    parser.add_argument('--service-name', type=str)
    parser.add_argument('--dns-names', type=str, nargs='+')
    parser.add_argument('--cert-type', type=str, choices=['server', 'client', 'both'])
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
