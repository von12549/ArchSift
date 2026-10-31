"""Account for every changed entry between an accepted and canonical candidate.

This does not certify raw-byte equality or execute package code. Managed image
equivalence must first be produced by Compare-ManagedReleaseImages.ps1.
"""
import argparse
import copy
import datetime
import email.utils
import hashlib
import json
import pathlib
import re
import xml.etree.ElementTree as ET
import zipfile


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load_zip(path, expected):
    raw = path.read_bytes()
    assert len(raw) <= 10_000_000 and len(raw) == expected["bytes"] and digest(raw) == expected["sha256"], "Archive identity differs"
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        names = [e.filename for e in entries]
        assert len(entries) <= 1500 and sum(e.file_size for e in entries) <= 80_000_000
        assert len(set(n.lower() for n in names)) == len(names)
        assert all(not n.startswith("/") and not re.search(r"[\\:\x00-\x1f]", n) and all(p not in ("", ".", "..") for p in n.split("/")) for n in names)
        assert all(not e.is_dir() and (e.external_attr >> 16) & 0xF000 != 0xA000 for e in entries)
        return {e.filename: archive.read(e) for e in entries}


def relationships(data, core_path):
    assert b"<!DOCTYPE" not in data and b"<!ENTITY" not in data
    tree = ET.fromstring(data)
    assert tree.tag == "{http://schemas.openxmlformats.org/package/2006/relationships}Relationships"
    rows, ids = [], set()
    for row in tree:
        assert row.tag.endswith("}Relationship") and set(row.attrib) <= {"Id", "Type", "Target", "TargetMode"}
        assert {"Id", "Type", "Target"} <= set(row.attrib)
        assert row.attrib["Id"] not in ids
        ids.add(row.attrib["Id"])
        value = {k: v for k, v in row.attrib.items() if k != "Id"}
        value["Target"] = value["Target"].replace(core_path, "{identical-core-properties}")
        rows.append(value)
    return sorted(rows, key=lambda r: json.dumps(r, sort_keys=True))


def endpoints(data):
    value = json.loads(data)
    for endpoint in value["Endpoints"]:
        for header in endpoint["ResponseHeaders"]:
            if header["Name"] == "Last-Modified":
                assert email.utils.parsedate_to_datetime(header["Value"]).tzinfo is not None
                header["Value"] = "{build-time-only}"
    return value


def web_inventory(data, files):
    value = json.loads(data)
    names = set()
    for entry in value["entries"]:
        assert set(entry) == {"path", "bytes", "sha256"} and entry["path"] not in names
        names.add(entry["path"])
        raw = files["tools/net10.0/any/web/" + entry["path"]]
        assert entry["bytes"] == len(raw) and entry["sha256"] == digest(raw), "Embedded inventory bytes differ"
        entry["bytes"] = "{validated-actual-bytes}"
        entry["sha256"] = "{validated-actual-digest}"
    value["entries"].sort(key=lambda e: e["path"])
    return value


def account(old, new, managed):
    old_core = [n for n in old if re.fullmatch(r"package/services/metadata/core-properties/[a-f0-9]{32}\.psmdcp", n)]
    new_core = [n for n in new if re.fullmatch(r"package/services/metadata/core-properties/[a-f0-9]{32}\.psmdcp", n)]
    assert len(old_core) == len(new_core) == 1 and old[old_core[0]] == new[new_core[0]], "Package properties changed"
    assert set(old) - set(new) <= set(old_core) and set(new) - set(old) <= set(new_core), "Unaccounted added/removed content"
    rows = []
    for name in sorted(set(old) & set(new)):
        if old[name] == new[name]:
            continue
        if name == "_rels/.rels":
            assert relationships(old[name], old_core[0]) == relationships(new[name], new_core[0]), "Package relationships changed"
            basis = "opaque relationship IDs and identical-property-part filename only"
        elif name.endswith(".dll"):
            relative = name.removeprefix("tools/net10.0/any/") if name.startswith("tools/net10.0/any/") else pathlib.PurePosixPath(name).name
            row = managed[relative]
            assert row["equal"] and row["nativeResourcesEqual"] and row["portableSymbolsMatch"]
            assert row["oldRawSha256"] == digest(old[name]) and row["mainRawSha256"] == digest(new[name]), "Managed module provenance differs"
            basis = "complete normalized managed image and raw native resources equal; matching PDB verified"
        elif name.endswith(".pdb"):
            relative = name.removeprefix("tools/net10.0/any/").removesuffix(".pdb") + ".dll"
            assert managed[relative]["equal"] and managed[relative]["portableSymbolsMatch"]
            basis = "portable debug documents/identities only; paired full managed image verified"
        elif name == "tools/net10.0/any/web/ArchSift.Web.staticwebassets.endpoints.json":
            assert endpoints(old[name]) == endpoints(new[name]), "Runtime static endpoint behavior changed"
            basis = "Last-Modified build timestamps only; routes, bytes, encodings and integrity unchanged"
        elif name == "tools/net10.0/any/web/web-payload.json":
            assert web_inventory(old[name], old) == web_inventory(new[name], new), "Embedded payload inventory changed"
            basis = "inventory recomputed from independently accounted actual bytes; source/version/path set unchanged"
        else:
            raise AssertionError("Unaccounted runtime content difference: " + name)
        rows.append({"path": name, "basis": basis, "oldSha256": digest(old[name]), "mainSha256": digest(new[name])})
    return {"accountedChangedEntries": rows, "identicalPropertyPartRenamed": {"old": old_core[0], "main": new_core[0]}, "unchangedEntries": sum(old[n] == new[n] for n in set(old) & set(new))}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--old-packages", required=True, type=pathlib.Path)
    parser.add_argument("--new-packages", required=True, type=pathlib.Path)
    parser.add_argument("--old-inventory", required=True, type=pathlib.Path)
    parser.add_argument("--new-inventory", required=True, type=pathlib.Path)
    parser.add_argument("--managed-proof", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    assert not args.output.exists(), "Preserve previous evidence"
    old_ref, new_ref, proof = (json.loads(p.read_text(encoding="utf-8-sig")) for p in (args.old_inventory, args.new_inventory, args.managed_proof))
    assert old_ref["sourceCommit"] == new_ref["sourceCommit"] and old_ref["sourceTree"] == new_ref["sourceTree"], "Different source identity"
    assert proof["status"] == "passed-normalized-managed-image-equivalence" and proof["negativeIlControl"] and proof["negativeMetadataControl"] and proof["hostEqual"]
    managed = {m["path"]: m for m in proof["modules"]}
    assert len(managed) == len(proof["modules"]) and all(m["equal"] for m in managed.values())
    rows = []
    for before in old_ref["packages"]:
        after = [p for p in new_ref["packages"] if p["id"] == before["id"]]
        assert len(after) == 1 and before["file"] == after[0]["file"]
        a = load_zip(args.old_packages / before["file"], before)
        b = load_zip(args.new_packages / after[0]["file"], after[0])
        rows.append({"id": before["id"], "oldSubmissionSha256": before["sha256"], "mainSubmissionSha256": after[0]["sha256"], **account(a, b, managed)})
        if before["id"] == "ArchSift.Cli":
            for name in ("tools/net10.0/any/web/wwwroot/app.js", "tools/net10.0/any/web/app.js"):
                if name in b:
                    mutant = dict(b)
                    mutant[name] += b"negative-control"
                    try:
                        account(a, mutant, managed)
                    except AssertionError:
                        break
                    raise AssertionError("Runtime asset mutation was ignored")
            else:
                raise AssertionError("Missing runtime asset negative control")
            mutant = dict(b)
            endpoint_path = "tools/net10.0/any/web/ArchSift.Web.staticwebassets.endpoints.json"
            changed = json.loads(mutant[endpoint_path]); changed["Endpoints"][0]["Route"] += "-negative-control"
            mutant[endpoint_path] = json.dumps(changed).encode()
            try:
                account(a, mutant, managed)
            except AssertionError:
                pass
            else:
                raise AssertionError("Runtime route mutation was ignored")
    assert len(rows) == 5
    result = {"schemaVersion": 1, "status": "passed-accounted-runtime-equivalence", "sourceCommit": new_ref["sourceCommit"], "sourceTree": new_ref["sourceTree"], "packages": rows, "managedProofSha256": digest(args.managed_proof.read_bytes()), "verifierSha256": digest(pathlib.Path(__file__).read_bytes()), "negativeControls": ["IL mutation", "assembly metadata mutation", "runtime asset mutation", "runtime route mutation"], "rawArchiveEquality": False, "rawPayloadEquality": False, "freshSecondaryMachineExecutionClaimed": False, "publicationPerformed": False}
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": result["status"], "packages": len(rows), "negativeControls": 4, "freshSecondaryMachineExecutionClaimed": False}))


if __name__ == "__main__":
    main()
