import json
from pathlib import Path
import shutil
import time
import zipfile

root = Path(__file__).resolve().parent.parent
output = root / "bin" / "Release"
manifest = json.loads((output / "ThrowCrapOut.json").read_text())
archive = output / "ThrowCrapOut" / "latest.zip"
icon = root / "images" / "icon.png"
if not icon.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"):
    raise SystemExit("Invalid plugin icon")
with zipfile.ZipFile(archive) as package:
    if package.testzip() is not None:
        raise SystemExit("Invalid release archive")
    embedded = json.loads(package.read("ThrowCrapOut.json"))
    if embedded["AssemblyVersion"] != manifest["AssemblyVersion"]:
        raise SystemExit("Release manifest version mismatch")
    if "ThrowCrapOut.dll" not in package.namelist():
        raise SystemExit("Plugin DLL missing from release archive")
    if embedded.get("IconUrl") != manifest.get("IconUrl") or not embedded.get("IconUrl"):
        raise SystemExit("Plugin icon URL missing from release manifest")
    entries = [(info, package.read(info.filename)) for info in package.infolist()
               if info.filename != "images/icon.png"]
temporary_archive = archive.with_suffix(".zip.tmp")
with zipfile.ZipFile(temporary_archive, "w") as package:
    for info, content in entries:
        package.writestr(info, content)
    package.write(icon, "images/icon.png", compress_type=zipfile.ZIP_DEFLATED)
temporary_archive.replace(archive)
repository = "https://github.com/NeoGriever/ThrowCrapOut"
raw = "https://raw.githubusercontent.com/NeoGriever/ThrowCrapOut/main"
destination = root / "plugins" / "ThrowCrapOut"
destination.mkdir(parents=True, exist_ok=True)
shutil.copy2(archive, destination / "latest.zip")
shutil.copy2(icon, destination / "icon.png")
manifest.update(
    RepoUrl=repository,
    IconUrl=f"{raw}/plugins/ThrowCrapOut/icon.png",
    DownloadLinkInstall=f"{raw}/plugins/ThrowCrapOut/latest.zip",
    DownloadLinkUpdate=f"{raw}/plugins/ThrowCrapOut/latest.zip",
    DownloadLinkTesting=f"{raw}/plugins/ThrowCrapOut/latest.zip",
    LastUpdate=str(int(time.time())),
)
entry = {
    key: manifest[key]
    for key in (
        "Author", "Name", "InternalName", "AssemblyVersion", "Description",
        "ApplicableVersion", "RepoUrl", "Tags", "DalamudApiLevel", "Punchline",
        "Changelog", "IconUrl", "DownloadLinkInstall", "DownloadLinkUpdate",
        "LastUpdate",
    )
}
entry["IsHide"] = False
entry["IsTestingExclusive"] = False
repository_json = json.dumps([entry], ensure_ascii=False, indent=2) + "\n"
for filename in ("repo.json", "pluginmaster.json"):
    (root / filename).write_text(repository_json)
print(f"Prepared ThrowCrapOut {manifest['AssemblyVersion']}: repo.json, pluginmaster.json and {destination.relative_to(root)}/latest.zip")
