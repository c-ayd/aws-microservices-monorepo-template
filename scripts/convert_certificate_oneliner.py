from pathlib import Path
import argparse
import os

def main(args, root_folder):
    if args.cert is None:
        print('Certificate file is not provided.')
        return
    if args.output is None:
        print('Output directory is not provided.')
        return

    output_path = os.path.join(root_folder, args.output)
    if not os.path.exists(output_path):
        os.makedirs(output_path)

    convert_cert(root_folder, args.cert, args.output)
    print('Certificate file is converted to one-line text.')

def convert_cert(root_folder, cert_path, output):
    with open(os.path.join(root_folder, cert_path), 'r', encoding='utf-8') as file:
        content = file.read()

    lines = content.replace('\r\n', '\n').replace('\r', '\n').split('\n')
    lines = [line.strip() for line in lines if line.strip()]
    oneliner = '\\n'.join(lines)

    with open(os.path.join(root_folder, output, os.path.basename(cert_path) + '.txt'), 'w', encoding='utf-8', newline='') as file:
        file.write(oneliner)

if __name__ == '__main__':
    root_folder = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser()
    parser.add_argument('--cert', type = str)
    parser.add_argument('--output', type = str)
    args = parser.parse_args()

    main(args=args,
         root_folder=root_folder)
