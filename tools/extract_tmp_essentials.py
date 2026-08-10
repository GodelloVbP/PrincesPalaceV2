"""Extract TextMeshPro's Essential Resources into Assets/, deterministically.

WHY THIS EXISTS, rather than AssetDatabase.ImportPackage:

TMP needs shaders (TMP_SDF and friends) to build a font asset's material. Those
shaders ship ONLY inside "TMP Essential Resources.unitypackage" in the
com.unity.ugui package -- they are not in the package's own assemblies, so
Shader.Find cannot see them and TMP_FontAsset.CreateFontAsset dies with
"ArgumentNullException: Parameter name: shader".

The documented way to get them is AssetDatabase.ImportPackage. In batch mode
that call is ASYNCHRONOUS: it logs success, `-quit` exits before the import
runs, and nothing lands on disk. The failure then surfaces much later, inside
TMP's own code, naming a shader rather than an import.

A .unitypackage is a gzipped tar of GUID-named folders, each holding the asset
bytes, its .meta, and a `pathname` file saying where it goes. Unpacking that
ourselves is synchronous, inspectable and reproducible -- and it keeps the
project's rule that state on disk comes from a tool you can re-run, not from
someone having clicked a menu item once.

Usage:
    python tools/extract_tmp_essentials.py [--force]
"""

import argparse
import glob
import os
import sys
import tarfile

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE_GLOB = os.path.join(
    PROJECT_ROOT, "Library", "PackageCache", "com.unity.ugui@*",
    "Package Resources", "TMP Essential Resources.unitypackage")

# The marker that says the extraction already happened. Cheap idempotence: the
# build calls this every time and it does nothing on the runs that matter.
SENTINEL = os.path.join(PROJECT_ROOT, "Assets", "TextMesh Pro", "Shaders", "TMP_SDF.shader")


def find_package():
    matches = sorted(glob.glob(PACKAGE_GLOB))
    if not matches:
        # The package cache is a build artefact and is not committed, so this is
        # "open the project in Unity once", not "something is broken".
        print("  no com.unity.ugui package cache found - open the project in Unity first", file=sys.stderr)
        return None
    return matches[-1]


def extract(package_path, force=False):
    if os.path.exists(SENTINEL) and not force:
        print(f"  already extracted ({os.path.relpath(SENTINEL, PROJECT_ROOT)}) - nothing to do")
        return 0

    written = 0
    with tarfile.open(package_path, "r:gz") as tar:
        # Group members by their GUID folder, which is how a .unitypackage
        # associates an asset with its pathname and its .meta.
        entries = {}
        for member in tar.getmembers():
            if not member.isfile():
                continue
            parts = member.name.split("/")
            if len(parts) < 2:
                continue
            entries.setdefault(parts[0], {})[parts[-1]] = member

        for guid, files in sorted(entries.items()):
            if "pathname" not in files:
                continue

            raw = tar.extractfile(files["pathname"]).read().decode("utf-8", "replace")
            target = raw.split("\n")[0].strip()
            if not target.startswith("Assets/"):
                continue

            dest = os.path.join(PROJECT_ROOT, target.replace("/", os.sep))

            # A folder entry has a pathname and a .meta but no asset payload.
            if "asset" not in files:
                os.makedirs(dest, exist_ok=True)
            else:
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                with open(dest, "wb") as f:
                    f.write(tar.extractfile(files["asset"]).read())
                written += 1

            # The .meta carries the GUID every reference in every scene and
            # material resolves through. Extracting the asset without it would
            # make Unity invent a fresh GUID on import, and the shader
            # references inside the package's own materials would dangle.
            if "asset.meta" in files:
                with open(dest + ".meta", "wb") as f:
                    f.write(tar.extractfile(files["asset.meta"]).read())

    print(f"  extracted {written} asset(s) from {os.path.basename(package_path)}")
    return written


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="re-extract even if already present")
    args = parser.parse_args()

    package = find_package()
    if package is None:
        return 1

    extract(package, force=args.force)
    return 0


if __name__ == "__main__":
    sys.exit(main())
