from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from datetime import datetime, timedelta, timezone
import argparse
from pathlib import Path
import os

def main(args, root_folder):
    if args.password is None:
        print('Password is not provided.')
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

    _, _ = generate_root_ca(output_path=output_path,
                            organization=organization,
                            country=country,
                            common_name=args.name,
                            valid_days=36500,
                            password=args.password)

    print('Root CA is generated.')

def generate_root_ca(output_path,
                     organization,
                     country,
                     common_name,
                     valid_days,
                     password):
    root_private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=4096
    )
    
    subject = issuer = x509.Name([
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization),
        x509.NameAttribute(NameOID.COUNTRY_NAME, country),
        x509.NameAttribute(NameOID.COMMON_NAME, common_name)
    ])

    now = datetime.now(timezone.utc)
    root_cert = (
        x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(issuer)
            .public_key(root_private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5))
            .not_valid_after(now + timedelta(days=valid_days))
        
            .add_extension(
                x509.BasicConstraints(ca=True, path_length=1),
                critical=True,
            )
            .add_extension(
                x509.KeyUsage(
                    digital_signature=False,
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
                x509.SubjectKeyIdentifier.from_public_key(root_private_key.public_key()),
                critical=False,
            )

            .sign(root_private_key, hashes.SHA256())
    )

    with open(os.path.join(output_path, f'{common_name}.key'), 'wb') as file:
        file.write(root_private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.BestAvailableEncryption(password.encode('utf-8'))))
    
    with open(os.path.join(output_path, f'{common_name}.crt'), 'wb') as file:
        file.write(root_cert.public_bytes(serialization.Encoding.PEM))
    
    return root_private_key, root_cert

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--password', type=str)
    parser.add_argument('--name', type=str)
    parser.add_argument('--output', type=str)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
