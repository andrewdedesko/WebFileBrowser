# WebFileBrowser

## Deploying
```sh
dotnet publish --os linux --arch x64 /t:PublishContainer
podman push --tls-verify=false webfilebrowser nas-o-matic.lan:5000/webfilebrowser:latest

# Or
podman save -o webfilebrowser.tar webfilebrowser:latest
scp webfilebrowser.tar ...

ssh ...

podman load -i webfilebrowser.tar
# Or
podman pull --tls-verify=false localhost:5000/webfilebrowser
podman run -d -p 8080:8080 -e "Shares__Media=/media" -v /mnt/media:/media webfilebrowser
```