#!/usr/bin/env bash
# Packs the four FastBindings packages with one version into a folder:
#   scripts/pack.sh VERSION [OUT]       (MAUI_CORE_ONLY=1: FastBindings.MAUI only for net8.0, where the MAUI workload is missing)
# FastBindings (Avalonia), FastBindings.MAUI, FastBindings.WPF_50 (WPF, .NET 5+) and FastBindings.WPF_45 (WPF, .NET Framework 4.5).
set -euo pipefail
version=$1
out=${2:-artifacts/packages}
mkdir -p "$out"
props=(-c Release "-p:Version=$version" "-p:AssemblyVersion=$version.0" "-p:FileVersion=$version.0" -p:EnableWindowsTargeting=true)
maui=()
[ "${MAUI_CORE_ONLY:-}" = 1 ] && maui=(-p:MauiCoreOnly=true)

dotnet build FastBinding.Net5 "${props[@]}"
dotnet build FastBinding.Net4.5 "${props[@]}"
dotnet pack FastBinding.Avalonia "${props[@]}" -o "$out"
dotnet pack FastBinding.MAUI "${props[@]}" "${maui[@]}" -o "$out"
dotnet pack FastBinding.Net5 "${props[@]}" --no-build -o "$out"
dotnet pack FastBinding.Net4.5 "${props[@]}" --no-build -o "$out"
ls -l "$out"
