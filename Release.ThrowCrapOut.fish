#!/usr/bin/env fish

set -l script_dir (path dirname (status filename))
set -l project_root (path resolve "$script_dir/ThrowCrapOut")
if test -f "$script_dir/ThrowCrapOut.csproj"
    set project_root (path resolve "$script_dir")
end
if not test -f "$project_root/ThrowCrapOut.csproj"
    echo "ThrowCrapOut.csproj not found" >&2
    exit 1
end
if test (count $argv) -gt 1; or begin; test (count $argv) -eq 1; and test "$argv[1]" != --build-only; end
    echo "Usage: Release.ThrowCrapOut.fish [--build-only]" >&2
    exit 1
end
cd "$project_root"; or exit 1
dotnet run --project Tests/PlannerTests.csproj -c Release; or exit 1
dotnet build ThrowCrapOut.csproj -c Release --nologo; or exit 1
python3 scripts/package.py; or exit 1
if contains -- --build-only $argv
    exit 0
end
git rev-parse --is-inside-work-tree >/dev/null; or exit 1
set -l version (python3 -c 'import json; print(json.load(open("bin/Release/ThrowCrapOut.json"))["AssemblyVersion"])'); or exit 1
git add -- '*.cs' '*.csproj' .gitignore packages.lock.json Tests scripts images Release.ThrowCrapOut.fish repo.json pluginmaster.json plugins; or exit 1
git diff --cached --quiet
set -l diff_status $status
if test $diff_status -eq 1
    git commit -m "Release $version"; or exit 1
else if test $diff_status -ne 0
    exit 1
end
git push -u origin HEAD; or exit 1
echo "Released ThrowCrapOut $version"
