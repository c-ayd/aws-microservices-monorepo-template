from pathlib import Path
from send2trash import send2trash
import json
import re
import subprocess
import time
import os

def main():
    src_folder = os.path.join(Path(__file__).resolve().parent.parent, 'src')

    for i in range(2):
        try:
            delete_lock_files(src_folder)
            delete_bin_folders(src_folder)
            rebuild_projects()
            break
        except:
            if i == 1:
                raise

            print('Something went wrong. Retrying...')
            time.sleep(1)

def delete_lock_files(root_folder):
    for file in Path(root_folder).rglob("packages.lock.json"):
        if file.is_file():
            send2trash(str(file))

    print('All packages.lock.json files are deleted.')

def delete_bin_folders(root_folder):
    folders = [
        folder
        for folder in Path(root_folder).rglob('bin')
        if folder.is_dir()
    ]

    for folder in folders:
        if folder.exists():
            send2trash(str(folder))

    print('All bin folders are deleted.')

def rebuild_projects():
    root_folder = Path(__file__).resolve().parent.parent
    vscode_tasks_path = root_folder / '.vscode' / 'tasks.json'
    vscode_tasks_without_comments = re.sub(r'//[^\n]*', '', vscode_tasks_path.read_text(encoding='utf-8'))
    vscode_tasks = json.loads(vscode_tasks_without_comments)

    for task in vscode_tasks['tasks']:
        if not task['label'].startswith('Build'):
            continue

        subprocess.run(task['command'], shell=True, cwd=root_folder)

if __name__ == '__main__':
    main()
