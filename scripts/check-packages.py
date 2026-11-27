#!/usr/bin/env python3
"""
Checks the packages scripts/pack.sh made: each of the five is there with the version asked for and holds the libraries it
should (FastBindings.WPF 1.1.3 went out with only an empty FastBinding.Package.dll in it).

Usage: check-packages.py FOLDER VERSION [--maui-core-only]
"""
import re, sys, zipfile, pathlib

folder, version = pathlib.Path(sys.argv[1]), sys.argv[2]
core_only = "--maui-core-only" in sys.argv
expected = {
    "FastBindings": ["lib/net5.0/FastBinding.Avalonia.dll"],
    "FastBindings.MAUI": ["lib/net8.0/FastBinding.MAUI.dll"] + ([] if core_only else [
        "lib/net8.0-android*/FastBinding.MAUI.dll", "lib/net8.0-ios*/FastBinding.MAUI.dll",
        "lib/net8.0-maccatalyst*/FastBinding.MAUI.dll", "lib/net8.0-windows10.0.19041*/FastBinding.MAUI.dll"]),
    "FastBindings.WPF_50": ["lib/net5.0-windows7.0/FastBinding.Net5.dll"],
    "FastBindings.WPF_45": ["lib/net45/FastBinding.Net4.5.dll"],
    "FastBindings.WPF": ["lib/net5.0-windows7.0/FastBinding.Net5.dll", "lib/net45/FastBinding.Net4.5.dll"],
}
failures = 0
for package, files in expected.items():
    path = folder / f"{package}.{version}.nupkg"
    if not path.exists():
        print(f"MISSING  {path.name}"); failures += 1; continue
    names = zipfile.ZipFile(path).namelist()
    nuspec = zipfile.ZipFile(path).read(f"{package}.nuspec").decode("utf-8-sig")
    stated = re.search(r"<version>([^<]+)</version>", nuspec).group(1)
    libs = sorted(n for n in names if n.startswith("lib/"))
    problems = [f"no {f}" for f in files
                if not any(re.fullmatch(f.replace(".", r"\.").replace("*", "[^/]*"), n) for n in names)]
    if stated != version:
        problems.append(f"version {stated}")
    problems += [f"unexpected {n}" for n in libs if n.endswith("FastBinding.Package.dll")]
    print(f"{'ok' if not problems else 'FAIL':8} {path.name}: {', '.join(libs)}" + (f"  — {'; '.join(problems)}" if problems else ""))
    failures += bool(problems)
sys.exit(1 if failures else 0)
