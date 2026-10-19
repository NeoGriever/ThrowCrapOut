#!/usr/bin/env fish

set -l project_root (path dirname (status filename))
cd "$project_root"; or exit 1

set -l account (gh api user --jq .login); or exit 1
if test "$account" != NeoGriever
    echo "GitHub account NeoGriever is required; active account: $account" >&2
    exit 1
end

fish Release.ThrowCrapOut.fish --build-only; or exit 1
set -l release_version (python3 -c 'import json; print(json.load(open("bin/Release/ThrowCrapOut.json"))["AssemblyVersion"])'); or exit 1

if not test -d .git
    git init -b main; or exit 1
end

git add -- '*.cs' '*.csproj' .gitignore packages.lock.json Tests scripts images Release.ThrowCrapOut.fish Publish.ThrowCrapOut.fish repo.json pluginmaster.json plugins; or exit 1
git diff --cached --quiet
set -l diff_status $status
if test $diff_status -eq 1
    git commit -m "Release $release_version"; or exit 1
else if test $diff_status -ne 0
    exit 1
end

if git remote get-url origin >/dev/null 2>/dev/null
    set -l origin (git remote get-url origin)
    if test "$origin" != https://github.com/NeoGriever/ThrowCrapOut.git; and test "$origin" != git@github.com:NeoGriever/ThrowCrapOut.git
        echo "Unexpected origin: $origin" >&2
        exit 1
    end
    git push -u origin main; or exit 1
else if gh repo view NeoGriever/ThrowCrapOut >/dev/null 2>/dev/null
    git remote add origin https://github.com/NeoGriever/ThrowCrapOut.git; or exit 1
    git push -u origin main; or exit 1
else
    gh repo create NeoGriever/ThrowCrapOut --public --source=. --remote=origin --push --description 'Compact armoury organizer for FFXIV Dalamud'; or exit 1
end

echo "Repository URL: https://raw.githubusercontent.com/NeoGriever/ThrowCrapOut/main/repo.json"
