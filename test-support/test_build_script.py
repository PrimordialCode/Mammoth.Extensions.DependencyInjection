"""Exercise the real build.ps1 with a native dotnet stub; no .NET commands run.

Usage: python test-support/test_build_script.py --powershell pwsh
Also run with --powershell powershell on Windows to cover Windows PowerShell 5.1.
"""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


PARSER = argparse.ArgumentParser()
PARSER.add_argument("--powershell", default="pwsh")
ARGS = PARSER.parse_args()
POWERSHELL = shutil.which(ARGS.powershell)
if not POWERSHELL:
    PARSER.error("PowerShell executable not found: " + ARGS.powershell)
BUILD_SCRIPT = Path(__file__).resolve().parents[1] / "build.ps1"
COMMANDS = [
    "tool restore",
    "restore ./src/Mammoth.Extensions.DependencyInjection.sln",
    "tool run dotnet-gitversion /updateprojectfiles",
    "build ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-restore -p:ContinuousIntegrationBuild=True",
    "pack ./src/Mammoth.Extensions.DependencyInjection.sln --configuration Release --no-build --output ./artifacts --include-symbols",
]


class BuildScriptTests(unittest.TestCase):
    def run_case(self, failure, native_errors):
        # A fresh child process prevents exit from ending the test runner and measures
        # the actual process exit status (not merely its final LASTEXITCODE variable).
        with tempfile.TemporaryDirectory(prefix="build script regression ") as folder:
            root = Path(folder)
            stub = root / ("dotnet.cmd" if os.name == "nt" else "dotnet")
            if os.name == "nt":
                source = '@echo off\necho %*>>"%BUILD_TEST_LOG%"\n'
                source += 'if "%*"=="%BUILD_TEST_FAIL%" exit /b %BUILD_TEST_CODE%\nexit /b 0\n'
            else:
                source = '#!/bin/sh\nprintf "%s\\n" "$*" >> "$BUILD_TEST_LOG"\n'
                source += '[ "$*" = "$BUILD_TEST_FAIL" ] && exit "$BUILD_TEST_CODE"\nexit 0\n'
            stub.write_text(source, encoding="ascii")
            stub.chmod(0o755)
            log = root / "commands.txt"
            environment = os.environ.copy()
            environment.update({
                # Only the stub is on PATH. Accidentally calling the real SDK is impossible.
                "PATH": str(root),
                "BUILD_TEST_LOG": str(log),
                "BUILD_TEST_FAIL": COMMANDS[failure] if failure is not None else "",
                "BUILD_TEST_CODE": str(23 + failure if failure is not None else 0),
                "BUILD_TEST_SCRIPT": str(BUILD_SCRIPT),
                "BUILD_TEST_STUB": str(stub),
            })
            launcher = root / "invoke.ps1"
            launcher.write_text(
                "$ErrorActionPreference = 'Stop'\n"
                + "$PSNativeCommandUseErrorActionPreference = $" + str(native_errors).lower() + "\n"
                + "if ((Get-Command dotnet -CommandType Application).Source -ne $env:BUILD_TEST_STUB) { throw 'Wrong dotnet binding' }\n"
                + BUILD_SCRIPT.read_text(encoding="utf-8-sig")
                # Execute the unchanged script body directly, with no trailing code that
                # could hide or repair its process exit status.
                , encoding="utf-8")
            result = subprocess.run(
                [POWERSHELL, "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(launcher)],
                cwd=root, env=environment, capture_output=True, text=True, timeout=60,
            )
            observed = log.read_text().splitlines() if log.exists() else []
            expected = COMMANDS if failure is None else COMMANDS[:failure + 1]
            self.assertEqual(expected, observed, result.stdout + result.stderr)
            self.assertEqual(0 if failure is None else 23 + failure, result.returncode,
                             result.stdout + result.stderr)

    def test_success_and_each_native_failure(self):
        for native_errors in (False, True):
            for failure in (None, 0, 1, 2, 3, 4):
                with self.subTest(failure=failure, native_errors=native_errors):
                    self.run_case(failure, native_errors)


if __name__ == "__main__":
    unittest.main(argv=[__file__], verbosity=2)
