#!/usr/bin/env python3
"""
The next version of the FastBindings packages: the highest stable version published on nuget.org for any of them (and any
git tag vX.Y.Z), bumped by the given part. All the packages get one version, so a package that lagged catches up.

Usage: next-version.py patch|minor|major   (prints e.g. 1.1.4)
"""
import json, re, subprocess, sys, urllib.request

PACKAGES = ["FastBindings", "FastBindings.MAUI", "FastBindings.WPF", "FastBindings.WPF_50", "FastBindings.WPF_45"]
STABLE = re.compile(r"^(\d+)\.(\d+)\.(\d+)$")

def published(package):
    url = f"https://api.nuget.org/v3-flatcontainer/{package.lower()}/index.json"
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            return json.load(response)["versions"]
    except urllib.error.HTTPError as error:
        if error.code == 404:   # never published
            return []
        raise

def tagged():
    out = subprocess.run(["git", "tag", "--list", "v*"], capture_output=True, text=True).stdout
    return [t[1:] for t in out.split()]

def main():
    part = sys.argv[1] if len(sys.argv) > 1 else "patch"
    if part not in ("patch", "minor", "major"):
        sys.exit(f"unknown part {part!r}: patch, minor or major")
    versions = [tuple(map(int, m.groups())) for v in sum((published(p) for p in PACKAGES), []) + tagged()
                if (m := STABLE.match(v))]
    major, minor, patch = max(versions, default=(0, 0, 0))
    if part == "major":
        major, minor, patch = major + 1, 0, 0
    elif part == "minor":
        minor, patch = minor + 1, 0
    else:
        patch += 1
    print(f"{major}.{minor}.{patch}")

main()
