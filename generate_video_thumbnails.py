#!/usr/bin/python

import os
import subprocess
import sys

def generate_thumbnails(dir_path):
    for (root, dirs, files) in os.walk(dir_path):
        for f in files:
            path = os.path.join(root, f)
            
            if is_hidden(path):
                continue

            if is_video(path):
                thumbnail_path = get_video_thumbnail_path(path)
                incomplete_thumbnail_path = get_temp_path(thumbnail_path)

                if os.path.exists(incomplete_thumbnail_path):
                    os.remove(incomplete_thumbnail_path)

                if not os.path.exists(thumbnail_path):
                    print(f"Generating thumbnail for {os.path.basename(path)}")
                    generate_thumbnail(path, incomplete_thumbnail_path)
                    os.rename(incomplete_thumbnail_path, thumbnail_path)


def generate_thumbnail(video_path, thumbnail_path):
    command = ["ffmpegthumbnailer", "-s0", "-cjpg", "-i", video_path, "-o", thumbnail_path]
    r = subprocess.run(command)


def is_hidden(path):
    filename = os.path.basename(path)
    return filename.startswith(".")


def is_video(path):
    extension = get_extension_lowercase(path)
    return extension in [".mp4", ".m4v", ".webm"]


def get_extension_lowercase(path):
    filename = os.path.basename(path)
    filename_components = filename.split(".")
    if len(filename_components) > 1:
        return "." + filename_components[-1].lower()
    
    return None


def get_video_thumbnail_path(video_path):
    filename = os.path.basename(video_path)
    dir_path = os.path.dirname(video_path)
    thumbnail_name = f".{filename}.jpg"
    thumbnail_path = os.path.join(dir_path, thumbnail_name)
    return thumbnail_path


def get_temp_path(path):
    return path + ".incomplete"


if __name__ == "__main__":
    path = os.getcwd()
    if len(sys.argv) > 1:
        path = sys.argv[1]

    print(f"Searching for videos in {path}")
    generate_thumbnails(path)
