from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from datetime import datetime, timedelta, timezone
import argparse
from pathlib import Path
import os

def main(args, root_folder):
    if args.root_ca_key is None:
        print('Root CA key path is not provided.')
        return
    if args.root_ca_cert is None:
        print('Root CA certificate path is not provided.')
        return
    if args.password_root is None:
        print('Password for root CA is not provided.')
        return
    if args.password_int is None:
        print('Password for intermediate CA is not provided.')
        return
    if args.name is None:
        print('Name is not provided.')
        return
    if args.output is None:
        print('Output directory is not provided.')
        return

    output_path = os.path.join(root_folder, args.output)
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    organization = 'localhost'
    country = 'DE'

    root_private_key, root_cert = load_root_ca(root_ca_key_path=os.path.join(root_folder, args.root_ca_key),
                                               root_ca_cert_path=os.path.join(root_folder, args.root_ca_cert),
                                               password=args.password_root)

    _, _ = issue_intermediate_ca(output_path=output_path,
                                 root_private_key=root_private_key,
                                 root_cert=root_cert,
                                 organization=organization,
                                 country=country,
                                 common_name=args.name,
                                 valid_days=1825,
                                 password=args.password_int)

    print('Intermediate CA is issued.')

def load_root_ca(root_ca_key_path,
                 root_ca_cert_path,
                 password):
    with open(root_ca_key_path, 'rb') as file:
        root_private_key = serialization.load_pem_private_key(file.read(), password=password.encode('utf-8'))

    with open(root_ca_cert_path, 'rb') as file:
        root_cert = x509.load_pem_x509_certificate(file.read())

    return root_private_key, root_cert

def issue_intermediate_ca(output_path,
                          root_private_key,
                          root_cert,
                          organization,
                          country,
                          common_name,
                          password,
                          valid_days):
    intermediate_private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=4096
    )
    
    subject = x509.Name([
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization),
        x509.NameAttribute(NameOID.COUNTRY_NAME, country),
        x509.NameAttribute(NameOID.COMMON_NAME, common_name)
    ])

    now = datetime.now(timezone.utc)
    intermediate_cert = (
        x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(root_cert.subject)
            .public_key(intermediate_private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5))
            .not_valid_after(now + timedelta(days=valid_days))
        
            .add_extension(
                x509.BasicConstraints(ca=True, path_length=0),
                critical=True,
            )
            .add_extension(
                x509.KeyUsage(
                    digital_signature=True,
                    key_cert_sign=True,
                    crl_sign=True,
                    content_commitment=False,
                    key_encipherment=False,
                    data_encipherment=False,
                    key_agreement=False,
                    encipher_only=False,
                    decipher_only=False,
                ),
                critical=True,
            )
            .add_extension(
                x509.AuthorityKeyIdentifier.from_issuer_subject_key_identifier(
                    root_cert.extensions.get_extension_for_class(x509.SubjectKeyIdentifier).value
                ),
                critical=False,
            )
            .add_extension(
                x509.SubjectKeyIdentifier.from_public_key(intermediate_private_key.public_key()),
                critical=False,
            )

            .sign(root_private_key, hashes.SHA256())
    )
    
    with open(os.path.join(output_path, f'{common_name}.key'), 'wb') as file:
        file.write(intermediate_private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.BestAvailableEncryption(password.encode('utf-8'))))
    
    with open(os.path.join(output_path, f'{common_name}.crt'), 'wb') as file:
        file.write(intermediate_cert.public_bytes(serialization.Encoding.PEM))
    
    return intermediate_private_key, intermediate_cert

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--root-ca-key', type=str)
    parser.add_argument('--root-ca-cert', type=str)
    parser.add_argument('--password-root', type=str)
    parser.add_argument('--password-int', type=str)
    parser.add_argument('--name', type=str)
    parser.add_argument('--output', type=str)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
