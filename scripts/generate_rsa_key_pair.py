from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import serialization
from pathlib import Path
import os
import argparse

def main(args, root_folder):
    if args.name is None:
        print('File name is not provided.')
        return

    output_path = os.path.join(root_folder, 'output')
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    generate_rsa_key_pair(output_path=output_path,
                      name=args.name)

    print(f'RSA key pair is generated.')

def generate_rsa_key_pair(output_path, name):
    private_key = rsa.generate_private_key(
        backend=default_backend(),
        public_exponent=65537,
        key_size=2048
    )
    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )

    public_key = private_key.public_key()
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )

    with open(os.path.join(output_path, f'{name}-public.pem'), 'wb') as file:
        file.write(public_pem)
    with open(os.path.join(output_path, f'{name}-private.pem'), 'wb') as file:
        file.write(private_pem)

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--name', type=str)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
