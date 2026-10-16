"""LT05 external IFX cold/warm acceptance measurements.

Run prepare, baseline, clean, cold and warm commands one at a time; inspect each
saved result before the next gate. CI, parity, negatives and summary run separately.
The single external worktree keeps runner IDs stable. Clean removes only inventoried
bin/obj tool outputs inside that worktree; source, packages and prior reports remain.
"""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import statistics
import subprocess
import sys
import time
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

import psutil


ARCH = Path(__file__).resolve().parents[1]
LAB = Path("D:/ArchSift-lab")
REFERENCE = LAB / "fixtures/lt02a-ifx-cc06528-20261015"
METRICS = LAB / "runs/lt05-metrics-20261015"
FIXTURES = LAB / "fixtures"
PACKAGE_CACHE = LAB / "packages/lt02a-ifx-cc06528-20261015"
FEED = Path.home() / ".nuget/packages"
PROJECT = Path("tests/Architecture/IFX.Architecture.Tests/IFX.Architecture.Tests.csproj")
RULE = Path("tests/Architecture/IFX.Architecture.Tests/lt02a-mechanism.json")
ADAPTER = Path("tests/Architecture/IFX.Architecture.Tests/ArchSiftJsonTests.cs")
FROZEN_BASELINE = ARCH / "docs/decisions/20261015-lightweight-ifx-original-case-ids.json"
BASELINE = METRICS / "worktree-original-case-ids.json"
HOST_SCRIPT = ARCH / "scripts/HostState.ps1"
COMMIT = "cc06528ab17052baafa496df2a89f6cde9207384"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def host_hash() -> str:
    command = ["pwsh", "-NoProfile", "-Command",
               f". '{HOST_SCRIPT.as_posix()}'; Get-ArchSiftHostHash"]
    completed = subprocess.run(command, text=True, capture_output=True, check=True)
    value = completed.stdout.strip().splitlines()[-1]
    if len(value) != 64:
        raise RuntimeError("Host hash command did not return a digest.")
    return value


def checked_host(before: str, stage: str) -> None:
    after = host_hash()
    print(f"host {stage}: {before} == {after}: {before == after}", flush=True)
    if before != after:
        raise RuntimeError("Persistent host state changed; stop without repair.")


def path_for(method: str, round_number: int) -> Path:
    if method not in {"native", "cli", "web"} or round_number not in {1, 2, 3}:
        raise ValueError("Method must be native/cli/web and round must be 1..3.")
    return FIXTURES / "lt05-shared-cc06528"


def evidence_for(method: str, round_number: int) -> Path:
    return METRICS / "measured" / f"{method}-{round_number}"


def prepare(method: str, round_number: int) -> None:
    if method != "native" or round_number != 1:
        raise ValueError("Prepare the single shared worktree with: prepare native 1.")
    target = path_for(method, round_number)
    if target.exists():
        raise FileExistsError(f"Cold target already exists: {target}")
    if not target.resolve().is_relative_to(FIXTURES.resolve()):
        raise ValueError("Worktree path escapes the external fixtures root.")
    if not REFERENCE.is_dir() or not PACKAGE_CACHE.is_dir() or not FEED.is_dir():
        raise FileNotFoundError("Reviewed reference or offline package input is missing.")
    subprocess.run(["git", "-C", str(REFERENCE), "worktree", "add", "--detach",
                    str(target), COMMIT], check=True)
    shutil.copy2(REFERENCE / "NuGet.config", target / "NuGet.config")
    for relative in [PROJECT, RULE, ADAPTER]:
        shutil.copy2(REFERENCE / relative, target / relative)
    head = subprocess.check_output(["git", "-C", str(target), "rev-parse", "HEAD"], text=True).strip()
    if head != COMMIT:
        raise RuntimeError("Cold worktree has the wrong source commit.")
    output_dirs = [item for item in target.rglob("*") if item.is_dir() and item.name in {"bin", "obj"}]
    if output_dirs:
        raise RuntimeError("Cold worktree unexpectedly contains build outputs.")
    root = METRICS / "shared-preflight"
    root.mkdir(parents=True, exist_ok=False)
    inventory = {"method": method, "round": round_number, "target": str(target),
                 "commit": head, "projectSha256": digest(target / PROJECT),
                 "nugetConfigSha256": digest(target / "NuGet.config"),
                 "ruleSha256": digest(target / RULE),
                 "adapterSha256": digest(target / ADAPTER),
                 "frozenBaselineSha256": digest(FROZEN_BASELINE), "packageCache": str(PACKAGE_CACHE),
                 "localFeed": str(FEED), "initialBuildOutputDirectories": 0}
    (root / "input.json").write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(inventory, ensure_ascii=False), flush=True)


def process_peak(process: psutil.Process) -> int:
    try:
        group = [process, *process.children(recursive=True)]
        return sum(item.memory_info().rss for item in group if item.is_running())
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return 0


def execute(args: list[str], cwd: Path, stdout: Path, stderr: Path,
            environment: dict[str, str]) -> dict:
    started = time.perf_counter()
    peak = 0
    with stdout.open("wb") as out, stderr.open("wb") as err:
        child = subprocess.Popen(args, cwd=cwd, env=environment, stdout=out, stderr=err)
        observed = psutil.Process(child.pid)
        while child.poll() is None:
            peak = max(peak, process_peak(observed))
            time.sleep(0.05)
        peak = max(peak, process_peak(observed))
        code = child.wait()
    return {"seconds": round(time.perf_counter() - started, 3), "exitCode": code,
            "peakTreeRssBytes": peak, "command": args, "stdout": str(stdout), "stderr": str(stderr)}


def directory_bytes(root: Path) -> int:
    if not root.exists():
        return 0
    total = 0
    for directory, _, files in os.walk(root):
        for name in files:
            try:
                total += (Path(directory) / name).stat().st_size
            except FileNotFoundError:
                pass
    return total


def build_output_bytes(root: Path) -> int:
    total = 0
    for directory, child_dirs, files in os.walk(root):
        current = Path(directory)
        if current.name in {"bin", "obj"}:
            total += directory_bytes(current)
            child_dirs[:] = []
        elif current.name == ".git":
            child_dirs[:] = []
    return total


def baseline_preflight() -> None:
    target = path_for("native", 1)
    if BASELINE.exists() or not target.is_dir():
        raise RuntimeError("Preflight baseline already exists or worktree was not prepared.")
    run_dir = next(METRICS / f"baseline-preflight-{number}" for number in range(1, 100)
                   if not (METRICS / f"baseline-preflight-{number}").exists())
    run_dir.mkdir(parents=True, exist_ok=False)
    env = os.environ.copy()
    env["IFX_UPDATE_SNAPSHOTS"] = "0"
    adapter_config = run_dir / "adapter-config.json"
    adapter_config.write_text(json.dumps({"targetRoot": str(target), "rulesets": [RULE.as_posix()]}) + "\n",
                              encoding="utf-8")
    env["ARCHSIFT_TEST_GATE_CONFIG"] = str(adapter_config)
    before = host_hash()
    project = str(target / PROJECT)
    commands = [
        ("restore", ["dotnet", "restore", project, "--configfile", str(target / "NuGet.config"),
                     "--source", str(FEED), "--packages", str(PACKAGE_CACHE),
                     "--disable-parallel", "--disable-build-servers", "-p:NuGetAudit=false", "-m:1", "-v:minimal"]),
        ("build", ["dotnet", "build", project, "--no-restore", "-c", "Release", "-f", "net10.0",
                   "--disable-build-servers", "-p:UseSharedCompilation=false", "-m:1", "-v:minimal"]),
    ]
    executable = target / PROJECT.parent / "bin/Release/net10.0/IFX.Architecture.Tests.exe"
    for name, command in commands:
        stage = execute(command, target, run_dir / f"{name}.log", run_dir / f"{name}.stderr.log", env)
        checked_host(before, "preflight-" + name)
        if stage["exitCode"] != 0:
            raise RuntimeError("Preflight " + name + " failed.")
    test = execute([str(executable), "--no-ansi", "--progress", "off", "--report-xunit-ctrf",
                    "--report-xunit-ctrf-filename", "baseline.ctrf.json",
                    "--results-directory", str(run_dir)],
                   target, run_dir / "test.log", run_dir / "test.stderr.log", env)
    checked_host(before, "preflight-test")
    cases = json.loads((run_dir / "baseline.ctrf.json").read_text(encoding="utf-8-sig"))["results"]["tests"]
    old = json.loads((LAB / "runs/lt02a-original-20261015/original-ctrf.json").read_text(
        encoding="utf-8-sig"))["results"]["tests"]
    original = [item for item in cases if "ArchSiftJsonTests" not in item["name"]]
    added = [item for item in cases if "ArchSiftJsonTests" in item["name"]]
    ids = sorted(item["extra"]["id"] for item in original)
    if test["exitCode"] != 0 or len(cases) != 81 or len(original) != 80 or len(added) != 1 or \
       set(item["name"] for item in original) != set(item["name"] for item in old) or \
       len(set(ids)) != 80 or any(item["status"] != "passed" for item in cases):
        raise RuntimeError("Preflight did not preserve all 80 original case names and one JSON case.")
    BASELINE.write_bytes((json.dumps(ids, indent=2) + "\n").encode("utf-8"))
    record = {"target": str(target), "commit": COMMIT, "baselineSha256": digest(BASELINE),
              "frozenBaselineSha256": digest(FROZEN_BASELINE),
              "sameOriginalNames": True, "pathScopedIdsDiffer": ids != sorted(json.loads(
                  FROZEN_BASELINE.read_text(encoding="utf-8"))),
              "originals": 80, "jsonCases": 1, "ctrtSha256": digest(run_dir / "baseline.ctrf.json"),
              "hostHash": before}
    (run_dir / "preflight.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(record), flush=True)


def clean_build_outputs(method: str, round_number: int) -> None:
    target = path_for(method, round_number).resolve()
    if not BASELINE.is_file():
        raise RuntimeError("Reviewed local runner baseline is required before cleaning.")
    manifest = METRICS / f"clean-before-{method}-{round_number}.json"
    if manifest.exists():
        raise FileExistsError("Clean manifest already exists; preserve previous evidence.")
    targets = []
    for directory, children, _ in os.walk(target):
        current = Path(directory)
        if current.name == ".git":
            children[:] = []
        elif current.name in {"bin", "obj"}:
            resolved = current.resolve()
            if resolved == target or not resolved.is_relative_to(target) or current.is_symlink():
                raise RuntimeError("Refusing unverified recursive cleanup path.")
            targets.append({"path": str(resolved), "bytes": directory_bytes(resolved)})
            children[:] = []
    if not targets:
        raise RuntimeError("No precisely identified bin/obj outputs to clean.")
    record = {"state": "planned", "worktree": str(target), "baselineSha256": digest(BASELINE),
              "ownedBuildDirectories": targets}
    manifest.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    for item in targets:
        path = Path(item["path"])
        if path.name not in {"bin", "obj"} or not path.resolve().is_relative_to(target):
            raise RuntimeError("Cleanup target changed after the manifest was written.")
        shutil.rmtree(path)
    record["state"] = "completed"
    record["buildOutputBytesAfter"] = build_output_bytes(target)
    manifest.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    if record["buildOutputBytesAfter"] != 0:
        raise RuntimeError("Precise cleanup left build outputs; stop before cold measurement.")
    print(json.dumps({"clean": str(manifest), "directories": len(targets),
                      "removedBytes": sum(item["bytes"] for item in targets)}), flush=True)


def gate_config(target: Path, run_directory: Path) -> dict:
    return {"schemaVersion": 1, "targetRoot": str(target), "testProject": PROJECT.as_posix(),
            "testExecutableName": "IFX.Architecture.Tests", "targetFramework": "net10.0",
            "configuration": "Release", "nuGetConfig": "NuGet.config", "localFeed": str(FEED),
            "packageCache": str(PACKAGE_CACHE), "rulesets": [RULE.as_posix()],
            "originalBaseline": str(BASELINE), "originalBaselineSha256": digest(BASELINE),
            "runDirectory": str(run_directory), "runnerFormat": "mtp-ctrf"}


def inspect_gate(report_path: Path) -> dict:
    report = json.loads(report_path.read_text(encoding="utf-8"))
    checks = report["checks"]
    original = [item for item in checks if item["source"] == "ifx-specialized"]
    json_checks = [item for item in checks if item["source"] == "json-type-dependency"]
    expected = set(json.loads(BASELINE.read_text(encoding="utf-8")))
    actual = {item["runnerCaseId"] for item in original if item.get("executed")}
    if (report["outcome"] != {"runGate": "allow", "exitCode": 0, "reasons": []} or
        len(original) != 80 or len(json_checks) != 1 or actual != expected or
        any(item["result"] != "passed" for item in checks) or
        len(report["notIncluded"]) != 588 or not report["testScope"]["coverageComplete"]):
        raise RuntimeError("Full IFX gate or original-case preservation failed.")
    return {"gate": "allow", "exitCode": 0, "originals": len(original),
            "jsonCases": len(json_checks), "notIncluded": len(report["notIncluded"]),
            "rulesetSha256": report["inputs"]["rulesets"][0]["sha256"],
            "reportSha256": digest(report_path), "reportPath": str(report_path)}


def native(target: Path, root: Path, phase: str, before: str) -> tuple[dict, dict]:
    run_dir = root / phase
    run_dir.mkdir(exist_ok=False)
    package_source = str(FEED)
    project = str(target / PROJECT)
    env = os.environ.copy()
    env["IFX_UPDATE_SNAPSHOTS"] = "0"
    adapter_config = run_dir / "adapter-config.json"
    adapter_config.write_text(json.dumps({"targetRoot": str(target), "rulesets": [RULE.as_posix()]}) + "\n",
                              encoding="utf-8")
    env["ARCHSIFT_TEST_GATE_CONFIG"] = str(adapter_config)
    commands = [
        ("restore", ["dotnet", "restore", project, "--configfile", str(target / "NuGet.config"),
                     "--source", package_source, "--packages", str(PACKAGE_CACHE),
                     "--disable-parallel", "--disable-build-servers", "-p:NuGetAudit=false", "-m:1", "-v:minimal"]),
        ("build", ["dotnet", "build", project, "--no-restore", "-c", "Release", "-f", "net10.0",
                   "--disable-build-servers", "-p:UseSharedCompilation=false", "-m:1", "-v:minimal"]),
    ]
    executable = target / PROJECT.parent / "bin/Release/net10.0/IFX.Architecture.Tests.exe"
    stages = {}
    for name, command in commands:
        stages[name] = execute(command, target, run_dir / f"{name}.log", run_dir / f"{name}.stderr.log", env)
        checked_host(before, name)
        if stages[name]["exitCode"] != 0:
            raise RuntimeError(f"Native {name} failed; inspect {run_dir}.")
    stages["discovery"] = execute([str(executable), "--list-tests", "json", "--no-ansi", "--progress", "off"],
                                  target, run_dir / "discovery.json", run_dir / "discovery.stderr.log", env)
    checked_host(before, "discovery")
    if stages["discovery"]["exitCode"] != 0:
        raise RuntimeError("Native discovery failed.")
    stages["test"] = execute([str(executable), "--no-ansi", "--progress", "off", "--report-xunit-ctrf",
                              "--report-xunit-ctrf-filename", "native.ctrf.json",
                              "--results-directory", str(run_dir)],
                             target, run_dir / "test.log", run_dir / "test.stderr.log", env)
    checked_host(before, "test")
    ctrf = json.loads((run_dir / "native.ctrf.json").read_text(encoding="utf-8-sig"))
    tests = ctrf["results"]["tests"]
    original = [item for item in tests if "ArchSiftJsonTests" not in item["name"]]
    added = [item for item in tests if "ArchSiftJsonTests" in item["name"]]
    ids = {item["extra"]["id"] for item in original}
    expected = set(json.loads(BASELINE.read_text(encoding="utf-8")))
    if stages["test"]["exitCode"] != 0 or ids != expected or len(original) != 80 or len(added) != 1 or \
       any(item["status"] != "passed" for item in tests):
        raise RuntimeError("Native IFX full test gate failed.")
    result = {"gate": "allow", "exitCode": 0, "originals": 80, "jsonCases": 1,
              "ctrtSha256": digest(run_dir / "native.ctrf.json")}
    return stages, result


def cli(target: Path, root: Path, phase: str, before: str) -> tuple[dict, dict]:
    run_dir = root / phase
    config_path = root / f"{phase}.config.json"
    config_path.write_text(json.dumps(gate_config(target, run_dir), indent=2) + "\n", encoding="utf-8")
    env = os.environ.copy()
    env["IFX_UPDATE_SNAPSHOTS"] = "1"
    executable = ARCH / "src/ArchSift.Cli/bin/Release/net10.0/archsift.exe"
    stage = execute([str(executable), "test-gate", "--config", str(config_path)], target,
                    root / f"{phase}.cli.stdout.log", root / f"{phase}.cli.stderr.log", env)
    checked_host(before, "cli-gate")
    if stage["exitCode"] != 0:
        raise RuntimeError(f"CLI gate failed; inspect {stage['stdout']}.")
    return {"gateProcess": stage}, inspect_gate(run_dir / "architecture-test-run.json")


def request(url: str, token: str, path: str, body: dict | None = None) -> dict:
    data = None if body is None else json.dumps(body).encode("utf-8")
    operation = urllib.request.Request(url + "/api/" + path, data=data,
        headers={"X-ArchSift-Token": token, "Content-Type": "application/json"},
        method="GET" if body is None else "POST")
    with urllib.request.urlopen(operation, timeout=15) as response:
        content = response.read()
    return json.loads(content) if content else {}


def web(target: Path, root: Path, phase: str, before: str) -> tuple[dict, dict, Path]:
    state_path = root / "web-server.json"
    started = time.perf_counter()
    startup_seconds = 0.0
    if phase == "cold":
        config = {"schemaVersion": 1, "target": {"root": str(target), "entry": PROJECT.as_posix()},
                  "rulesets": [], "build": {"mode": "existing", "targetFramework": "net10.0",
                  "configuration": "Release", "allowNetwork": False, "sources": []},
                  "output": {"directory": str(root / "web-output"), "formats": ["json", "html"]},
                  "rulesDirectory": str(root / "web-rules")}
        web_config = root / "web-config.json"
        web_config.write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")
        env = os.environ.copy()
        env["IFX_UPDATE_SNAPSHOTS"] = "1"
        output = root / "web-server.stdout.log"
        error = root / "web-server.stderr.log"
        with output.open("wb") as out, error.open("wb") as err:
            child = subprocess.Popen(["dotnet", str(ARCH / "src/ArchSift.Web/bin/Release/net10.0/ArchSift.Web.dll"),
                                      "--config", str(web_config)], cwd=ARCH, env=env, stdout=out, stderr=err,
                                     creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        line = None
        for _ in range(150):
            if child.poll() is not None:
                raise RuntimeError("Web service exited before publishing its loopback URL.")
            text = output.read_text(encoding="utf-8", errors="replace")
            line = next((value for value in text.splitlines() if value.startswith("ARCHSIFT_UI=")), None)
            if line:
                break
            time.sleep(0.1)
        if not line:
            raise TimeoutError("Web service did not publish its loopback URL.")
        url, token = line.removeprefix("ARCHSIFT_UI=").split("/#session=")
        state_path.write_text(json.dumps({"pid": child.pid, "url": url, "token": token}) + "\n", encoding="utf-8")
        startup_seconds = round(time.perf_counter() - started, 3)
        checked_host(before, "web-start")
    state = json.loads(state_path.read_text(encoding="utf-8"))
    server = psutil.Process(state["pid"])
    if not server.is_running():
        raise RuntimeError("Web service is no longer running.")
    url, token = state["url"], state["token"]
    config_path = root / "web-gate-config.json"
    if phase == "cold":
        config_path.write_text(json.dumps(gate_config(target, root / "unused-gate-directory"), indent=2) + "\n",
                               encoding="utf-8")
        view = request(url, token, "test-workbench/config", {"path": str(config_path)})
        if len(view["originalCaseIds"]) != 80 or len(view["notIncluded"]) != 588:
            raise RuntimeError("Web source inventory does not match the frozen baseline.")
    response = request(url, token, "test-workbench/run", {"mode": "full", "caseId": None})
    peak = 0
    for _ in range(1500):
        peak = max(peak, process_peak(server))
        time.sleep(0.1)
        job = request(url, token, "test-workbench/job")
        if job["state"] != "running":
            break
    else:
        raise TimeoutError("Web gate did not reach a terminal state.")
    total_seconds = round(time.perf_counter() - started, 3)
    checked_host(before, "web-gate")
    if job["exitCode"] != 0 or job["gate"] != "allow":
        raise RuntimeError("Web gate did not allow: " + json.dumps(job))
    report_path = Path(response["runDirectory"]) / "architecture-test-run.json"
    result = inspect_gate(report_path)
    marker = json.loads((report_path.parent / "architecture-test-state.json").read_text(encoding="utf-8"))
    api_report = request(url, token, "test-workbench/report/" + marker["runId"] + "/json")
    if api_report["outcome"] != {"runGate": "allow", "exitCode": 0, "reasons": []}:
        raise RuntimeError("Web download differs from current gate.")
    stage = {"webSession": {"seconds": total_seconds, "startupSeconds": startup_seconds,
                            "peakTreeRssBytes": peak, "jobId": response["jobId"],
                            "reportDirectory": response["runDirectory"]}}
    if phase == "warm":
        request(url, token, "shutdown", {})
        server.wait(timeout=20)
        checked_host(before, "web-shutdown")
    return stage, result, report_path.parent


def report_stage_seconds(report_dir: Path) -> dict:
    marker = json.loads((report_dir / "architecture-test-state.json").read_text(encoding="utf-8"))
    times = [datetime.fromisoformat(marker["startedUtc"]).timestamp()]
    names = ["restore", "build", "discovery", "test"]
    for name in names:
        path = report_dir / f"{name}.log"
        if path.exists():
            times.append(path.stat().st_mtime)
    times.append((report_dir / "architecture-test-run.json").stat().st_mtime)
    deltas = [round(max(0.0, right - left), 3) for left, right in zip(times, times[1:])]
    return dict(zip([*names, "report"], deltas))


def run(method: str, round_number: int, phase: str) -> None:
    if phase not in {"cold", "warm"}:
        raise ValueError("Phase must be cold/warm.")
    target = path_for(method, round_number)
    root = evidence_for(method, round_number)
    if not target.is_dir() or not BASELINE.is_file():
        raise FileNotFoundError("Prepare this external worktree first.")
    root.mkdir(parents=True, exist_ok=True)
    output = root / f"{phase}.metric.json"
    if output.exists():
        raise FileExistsError("Metric already exists; preserving prior evidence.")
    if phase == "warm" and not (root / "cold.metric.json").exists():
        raise RuntimeError("A verified cold run is required before the warm run.")
    before = host_hash()
    initial_build = build_output_bytes(target)
    if phase == "cold" and initial_build:
        raise RuntimeError("Cold target already contains build output.")
    if phase == "cold" and not (METRICS / f"clean-before-{method}-{round_number}.json").is_file():
        raise RuntimeError("Reviewed precise build-output cleanup is required before each cold run.")
    if phase == "warm" and not initial_build:
        raise RuntimeError("Warm target has no prior build output.")
    start = time.perf_counter()
    if method == "native":
        stages, outcome = native(target, root, phase, before)
        report_dir = root / phase
    elif method == "cli":
        stages, outcome = cli(target, root, phase, before)
        report_dir = root / phase
    else:
        stages, outcome, report_dir = web(target, root, phase, before)
    wall = round(time.perf_counter() - start, 3)
    checked_host(before, "completed")
    metric = {"schemaVersion": 1, "method": method, "round": round_number, "phase": phase,
              "target": str(target), "commit": COMMIT, "sdk": "10.0.303", "tfm": "net10.0",
              "configuration": "Release", "offlineFeed": str(FEED), "packageCache": str(PACKAGE_CACHE),
              "baselineSha256": digest(BASELINE), "ruleSha256": digest(target / RULE),
              "startedUtc": datetime.now(timezone.utc).isoformat(), "wallSeconds": wall,
              "stages": stages, "estimatedGateStageSeconds": report_stage_seconds(report_dir) if method != "native" else None,
              "peakTreeRssBytes": max(stage.get("peakTreeRssBytes", 0) for stage in stages.values()),
              "reportDirectory": str(report_dir), "ownedReportBytes": directory_bytes(report_dir),
              "buildOutputBytesBefore": initial_build, "buildOutputBytesAfter": build_output_bytes(target),
              "outcome": outcome, "hostHashBefore": before, "hostHashAfter": host_hash()}
    if method == "web":
        metric["peakTreeRssBytes"] = stages["webSession"]["peakTreeRssBytes"]
    if metric["hostHashBefore"] != metric["hostHashAfter"]:
        raise RuntimeError("Host state changed before writing accepted metric.")
    output.write_text(json.dumps(metric, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"method": method, "round": round_number, "phase": phase,
                      "wallSeconds": wall, "outcome": outcome, "ownedReportBytes": metric["ownedReportBytes"],
                      "peakTreeRssBytes": metric["peakTreeRssBytes"],
                      "buildOutputBytesBefore": initial_build,
                      "buildOutputBytesAfter": metric["buildOutputBytesAfter"],
                      "hostEqual": True}, ensure_ascii=False), flush=True)


def summarize() -> None:
    source = METRICS / "measured"
    metrics = []
    for method in ["native", "cli", "web"]:
        for round_number in [1, 2, 3]:
            for phase in ["cold", "warm"]:
                path = source / f"{method}-{round_number}/{phase}.metric.json"
                metric = json.loads(path.read_text(encoding="utf-8"))
                if metric["outcome"]["gate"] != "allow" or metric["outcome"]["exitCode"] != 0 or \
                   metric["outcome"]["originals"] != 80 or metric["outcome"]["jsonCases"] != 1 or \
                   metric["hostHashBefore"] != metric["hostHashAfter"]:
                    raise RuntimeError("A measurement is not a complete valid gate: " + str(path))
                metrics.append(metric)

    def span(values: list[float | int]) -> dict:
        return {"median": round(statistics.median(values), 3), "min": min(values), "max": max(values),
                "values": values}

    groups = {}
    for method in ["native", "cli", "web"]:
        for phase in ["cold", "warm"]:
            selected = [item for item in metrics if item["method"] == method and item["phase"] == phase]
            if method == "native":
                stage_names = ["restore", "build", "discovery", "test"]
                stages = {name: span([item["stages"][name]["seconds"] for item in selected])
                          for name in stage_names}
            else:
                stage_names = ["restore", "build", "discovery", "test", "report"]
                stages = {name: span([item["estimatedGateStageSeconds"][name] for item in selected])
                          for name in stage_names}
            groups[f"{method}-{phase}"] = {
                "wallSeconds": span([item["wallSeconds"] for item in selected]),
                "peakTreeRssBytes": span([item["peakTreeRssBytes"] for item in selected]),
                "ownedReportBytes": span([item["ownedReportBytes"] for item in selected]),
                "buildOutputBytesBefore": span([item["buildOutputBytesBefore"] for item in selected]),
                "buildOutputBytesAfter": span([item["buildOutputBytesAfter"] for item in selected]),
                "stages": stages}
            if method == "web":
                groups[f"{method}-{phase}"]["startupSeconds"] = span([
                    item["stages"]["webSession"]["startupSeconds"] for item in selected])
    result = {"schemaVersion": 1, "kind": "lt05-measurement-summary", "target": str(path_for("native", 1)),
              "commit": COMMIT, "sdk": "10.0.303", "targetFramework": "net10.0",
              "configuration": "Release", "baselineSha256": digest(BASELINE),
              "ruleSha256": digest(path_for("native", 1) / RULE),
              "runCount": len(metrics), "packageCachePreserved": True,
              "ownedReportBytesTotal": sum(item["ownedReportBytes"] for item in metrics),
              "groups": groups,
              "limits": ["Cold means no IFX worktree bin/obj; external ArchSift.Testing outputs and the offline package cache were preserved.",
                         "Native stages are directly timed; CLI/Web stage values are estimated from sequential log modification times.",
                         "Process-tree RSS is sampled, not a system-wide instantaneous peak; Web cold wall time includes service startup."]}
    destination = METRICS / "summary.json"
    if destination.exists():
        raise FileExistsError("Measurement summary already exists; preserve previous evidence.")
    destination.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"runCount": len(metrics), "summary": str(destination),
                      "groups": {key: value["wallSeconds"] for key, value in groups.items()},
                      "totalOwnedReportBytes": result["ownedReportBytesTotal"]}), flush=True)


def ci_simulation() -> None:
    target = path_for("cli", 1)
    root = METRICS / "ci-headless"
    root.mkdir(exist_ok=False)
    run_dir = root / "run"
    config_path = root / "config.json"
    config_path.write_text(json.dumps(gate_config(target, run_dir), indent=2) + "\n", encoding="utf-8")
    env = os.environ.copy()
    env["CI"] = "true"
    env["IFX_UPDATE_SNAPSHOTS"] = "1"
    before = host_hash()
    stage = execute([str(ARCH / "src/ArchSift.Cli/bin/Release/net10.0/archsift.exe"),
                     "test-gate", "--config", str(config_path)], target,
                    root / "cli.stdout.log", root / "cli.stderr.log", env)
    checked_host(before, "ci-headless")
    if stage["exitCode"] != 0:
        raise RuntimeError("Headless CI simulation failed.")
    outcome = inspect_gate(run_dir / "architecture-test-run.json")
    report = json.loads((run_dir / "architecture-test-run.json").read_text(encoding="utf-8"))
    if not report["process"]["snapshotUpdatesDisabled"] or report["run"]["mode"] != "full":
        raise RuntimeError("Headless CI run did not enforce the full snapshot-safe gate.")
    result = {"kind": "lt05-local-ci-simulation", "commit": COMMIT,
              "target": str(target), "baselineSha256": digest(BASELINE),
              "ruleSha256": digest(target / RULE), "ciChildEnvironment": True,
              "snapshotParentValue": "1", "snapshotChildDisabled": True,
              "result": outcome, "process": stage, "hostHash": before}
    (root / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"result": outcome, "hostEqual": True, "record": str(root / "result.json")}), flush=True)


def parity() -> None:
    paths = {}
    for method in ["cli", "web"]:
        metric = json.loads((evidence_for(method, 3) / "warm.metric.json").read_text(encoding="utf-8"))
        paths[method] = Path(metric["outcome"]["reportPath"])
    paths["ci"] = METRICS / "ci-headless/run/architecture-test-run.json"
    reports = {key: json.loads(path.read_text(encoding="utf-8")) for key, path in paths.items()}
    def projection(report: dict) -> dict:
        return {"gate": report["outcome"], "rulesetSha256": report["inputs"]["rulesets"][0]["sha256"],
                "originalIds": sorted(report["testScope"]["expectedOriginalCaseIds"]),
                "checks": sorted((item["source"], item["id"], item["result"], item["executed"])
                                 for item in report["checks"]),
                "notIncludedIds": sorted(item["id"] for item in report["notIncluded"]),
                "snapshotUpdatesDisabled": report["process"]["snapshotUpdatesDisabled"]}
    projections = {key: projection(report) for key, report in reports.items()}
    if not projections["cli"] == projections["web"] == projections["ci"]:
        raise RuntimeError("CLI, Web, and local CI reports differ on the executable test contract.")
    original = [item for item in reports["cli"]["checks"] if item["source"] == "ifx-specialized"]
    classes = {}
    for item in original:
        scope = item.get("scope", "")
        name = scope.rsplit(".", 1)[0].split(".")[-1]
        classes[name] = classes.get(name, 0) + 1
    if classes != {"ArchitectureTests": 16, "FixtureTests": 47, "ApplicationPortAdapterTests": 17}:
        raise RuntimeError("Three original IFX test classes or theory rows changed.")
    source_counts = {}
    for item in reports["cli"]["notIncluded"]:
        source_counts[item["source"]] = source_counts.get(item["source"], 0) + 1
    if source_counts != {"legacy-archsift": 182, "candidate-json-test": 406}:
        raise RuntimeError("A/B not-included inventory changed.")
    record = {"kind": "lt05-three-entry-parity", "sourceCommit": COMMIT,
              "reportPaths": {key: str(value) for key, value in paths.items()},
              "reportSha256": {key: digest(value) for key, value in paths.items()},
              "sameGateRulesCasesCoverageAndSnapshotIsolation": True,
              "originalClassCounts": classes, "notIncludedSourceCounts": source_counts,
              "originalCaseCount": 80, "jsonCaseCount": 1}
    target = METRICS / "parity.json"
    if target.exists():
        raise FileExistsError("Parity evidence already exists.")
    target.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(record), flush=True)


def negative(kind: str) -> None:
    if kind not in {"ifx", "json"}:
        raise ValueError("Negative kind must be ifx or json.")
    target = path_for("cli", 1).resolve()
    source = target / (Path("docs/protocols/snapshots/api/IFX.Platform.Context.Contracts.txt")
                       if kind == "ifx" else RULE)
    if source.is_symlink() or not source.resolve().is_relative_to(target):
        raise RuntimeError("Negative input is not a regular file inside the external worktree.")
    root = METRICS / f"negative-{kind}"
    root.mkdir(exist_ok=False)
    snapshots = sorted((target / "docs/protocols/snapshots/api").glob("*.txt"))
    snapshots_before = {str(path.relative_to(target)): digest(path) for path in snapshots}
    if len(snapshots) != 11:
        raise RuntimeError("IFX API snapshot scope differs from the reviewed 11-file baseline.")
    original = source.read_bytes()
    original_hash = digest(source)
    run_dir = root / "run"
    config_path = root / "config.json"
    config_path.write_text(json.dumps(gate_config(target, run_dir), indent=2) + "\n", encoding="utf-8")
    env = os.environ.copy()
    env["CI"] = "true"
    env["IFX_UPDATE_SNAPSHOTS"] = "1"
    before = host_hash()
    stage = None
    try:
        if kind == "ifx":
            source.write_bytes(original + b"X")
        else:
            edited = json.loads(original)
            rule = edited["rules"][0]
            rule["parameters"]["source"]["value"] = "IFX.Platform.Context.Contracts.EndpointScopeMetadata"
            rule["parameters"]["forbiddenTarget"]["kind"] = "type"
            rule["parameters"]["forbiddenTarget"]["value"] = "IFX.Platform.Context.Contracts.ExecutionScope"
            source.write_bytes(json.dumps(edited, indent=2).encode("utf-8"))
        (root / "modified-input.json").write_text(json.dumps({"kind": kind, "source": str(source),
             "originalSha256": original_hash, "modifiedSha256": digest(source)}, indent=2) + "\n", encoding="utf-8")
        stage = execute([str(ARCH / "src/ArchSift.Cli/bin/Release/net10.0/archsift.exe"),
                         "test-gate", "--config", str(config_path)], target,
                        root / "cli.stdout.log", root / "cli.stderr.log", env)
        checked_host(before, "negative-" + kind)
        report_path = run_dir / "architecture-test-run.json"
        report = json.loads(report_path.read_text(encoding="utf-8"))
        if stage["exitCode"] != 2 or report["outcome"]["runGate"] != "block" or \
           report["outcome"]["exitCode"] != 2 or not report["process"]["snapshotUpdatesDisabled"]:
            raise RuntimeError("Negative gate did not produce the expected current block/2.")
        originals = [item for item in report["checks"] if item["source"] == "ifx-specialized"]
        json_checks = [item for item in report["checks"] if item["source"] == "json-type-dependency"]
        if len(originals) != 80 or len(json_checks) != 1 or \
           any(not item["executed"] for item in report["checks"]):
            raise RuntimeError("Negative run did not execute the entire 80+1 case scope.")
        if kind == "ifx":
            failed = [item for item in originals if item["result"] == "failed"]
            if len(failed) != 1 or "PublicApi_ContractsSnapshotsMatch" not in failed[0].get("scope", "") or \
               json_checks[0]["result"] != "passed" or len(report["findings"]) != 0:
                raise RuntimeError("IFX assertion failure was mixed with JSON findings.")
        else:
            if any(item["result"] != "passed" for item in originals) or \
               json_checks[0]["result"] != "failed" or len(report["findings"]) != 1:
                raise RuntimeError("JSON dependency violation was mixed with original IFX assertions.")
        result = {"kind": kind, "gate": "block", "exitCode": 2,
                  "originalCount": 80, "jsonCount": 1, "findingCount": len(report["findings"]),
                  "ifxFailedCount": sum(item["result"] == "failed" for item in originals),
                  "reportSha256": digest(report_path), "reportPath": str(report_path),
                  "snapshotUpdatesDisabled": True, "parentSnapshotValue": "1",
                  "process": stage, "hostHash": before}
    finally:
        source.write_bytes(original)
        if digest(source) != original_hash:
            raise RuntimeError("Temporary negative input could not be restored byte-for-byte.")
    snapshots_after = {str(path.relative_to(target)): digest(path) for path in snapshots}
    if snapshots_before != snapshots_after:
        raise RuntimeError("API snapshot bytes changed during the negative gate.")
    checked_host(before, "negative-restored-" + kind)
    result["inputRestoredSha256"] = original_hash
    result["allElevenSnapshotsRestored"] = True
    result_path = root / "result.json"
    result_path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"kind": kind, "gate": result["gate"], "exitCode": result["exitCode"],
                      "ifxFailedCount": result["ifxFailedCount"], "findingCount": result["findingCount"],
                      "allElevenSnapshotsRestored": True, "hostEqual": True,
                      "reportSha256": result["reportSha256"]}), flush=True)


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "summarize":
        summarize()
        raise SystemExit(0)
    if len(sys.argv) == 2 and sys.argv[1] == "ci":
        ci_simulation()
        raise SystemExit(0)
    if len(sys.argv) == 2 and sys.argv[1] == "parity":
        parity()
        raise SystemExit(0)
    if len(sys.argv) == 3 and sys.argv[1] == "negative":
        negative(sys.argv[2])
        raise SystemExit(0)
    if len(sys.argv) != 4 or sys.argv[1] not in {"prepare", "baseline", "clean", "run-cold", "run-warm"}:
        raise SystemExit("usage: python scripts/lt05_measure.py summarize | prepare|baseline|clean|run-cold|run-warm native|cli|web 1|2|3")
    operation, name, number = sys.argv[1], sys.argv[2], int(sys.argv[3])
    if operation == "prepare":
        prepare(name, number)
    elif operation == "baseline":
        if name != "native" or number != 1:
            raise SystemExit("usage: baseline native 1")
        baseline_preflight()
    elif operation == "clean":
        clean_build_outputs(name, number)
    else:
        run(name, number, "cold" if operation == "run-cold" else "warm")
