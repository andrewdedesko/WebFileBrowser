#!/usr/bin/python

from pathlib import Path
import os
import subprocess
import sys

def convert_videos(dir_path):
    for (root, dirs, files) in os.walk(dir_path):
        for f in files:
            path = os.path.join(root, f)

            if is_video_to_convert(path):
                print(f"Converting {path}")
                try:
                    convert_video_to_mp4(path)
                except:
                    print(f"Failed to convert {path}")
                    pass


def is_video_to_convert(path):
    extension = get_extension_lowercase(path)
    return extension in [".wmv", ".avi", ".mkv", ".rm", ".flv"]


def get_extension_lowercase(path):
    filename = os.path.basename(path)
    filename_components = filename.split(".")
    if len(filename_components) > 1:
        return "." + filename_components[-1].lower()
    
    return None


def convert_video_to_mp4(video_path):
    if not os.path.exists(video_path):
        raise Exception(f"{video_path} does not exist")
    
    if not os.path.isfile(video_path):
        raise Exception(f"Cannot convert {video_path} because it's not a file")
    
    video_dir = os.path.dirname(video_path)
    output_video = os.path.join(video_dir, Path(video_path).stem + ".mp4")
    temp_video = output_video + ".incomplete"

    if os.path.exists(temp_video):
        os.remove(temp_video)
    
    try:
        transcode_video_to_mp4(video_path, temp_video)
        if not os.path.exists(temp_video):
            raise Exception(f"Transcoding {video_path} to {temp_video} did not create a new file")
        
        os.rename(temp_video, output_video)
        os.remove(video_path)
    except Exception as e:
        os.remove(temp_video)
        raise e


def transcode_video_to_mp4(src_video_path, dest_video_path):
    # ffmpeg -i <src file> -c:v libx264 -crf 23 -c:a aac -q:a 100 <out file>
    command = ["ffmpeg", "-i", src_video_path, "-c:v", "libx264", "-crf", "23", "-c:a", "aac", "-q:a", "100", "-f", "mp4", dest_video_path]
    r = subprocess.run(command)
    print(f"Exit code: {r.returncode}")
    r.check_returncode()


if __name__ == "__main__":
    if len(sys.argv) == 1:
        print(f"Usage: {sys.argv[0]} <directory to scan and convert>")
        sys.exit(1)

    convert_videos(sys.argv[1])
