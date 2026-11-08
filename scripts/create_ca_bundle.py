from cryptography import x509
from cryptography.hazmat.primitives import serialization
from pathlib import Path
import argparse
import os

def main(args, root_folder):
    if args.name is None:
        print('Name is not provided.')
        return
    
    output_path = os.path.join(root_folder, 'output')
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    create_ca_bundle(root_folder, args.certs, args.name, output_path)
    print('CA Bundle is created.')

def create_ca_bundle(root_folder, cert_paths, name, output_path):
    certs = []
    for cert_path in cert_paths:
        with open(os.path.join(root_folder, cert_path), 'rb') as file:
            certs.append(x509.load_pem_x509_certificate(file.read())
                .public_bytes(serialization.Encoding.PEM))

    bundle = b''.join(certs)
    with open(os.path.join(output_path, f'{name}.crt'), 'wb') as file:
        file.write(bundle)

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--certs', type=str, nargs='+')
    parser.add_argument('--name', type=str)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
