from pathlib import Path
import argparse
import secrets
import base64
import os

def main(args, root_folder):
    if args.name is None:
        print('Name is not provided.')
        return
    if args.bytes is None:
        print('Bytes is not provided.')
        return
    
    output_path = os.path.join(root_folder, 'output')
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    generate_aes_key(args.bytes, args.name, output_path)
    print('AES-256 key is generated.')

def generate_aes_key(num_bytes, name, output_path):
    key_bytes = secrets.token_bytes(num_bytes)
    key = base64.b64encode(key_bytes).decode('utf-8')

    with open(os.path.join(output_path, f'{name}.txt'), 'w') as file:
        file.write(key)

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--name', type=str)
    parser.add_argument('--bytes', type=int)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
