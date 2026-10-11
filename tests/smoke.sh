#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/AuthStateDoctor/AuthStateDoctor.csproj"
PROBE_PROJECT="$ROOT/src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj"
CLI=(dotnet run --no-build --project "$PROJECT" --)
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ -n "${AUTHDOCTOR_ARTIFACT_DIR:-}" ]]; then
  output_dir="$AUTHDOCTOR_ARTIFACT_DIR"
  [[ "$output_dir" = /* ]] || output_dir="$ROOT/$output_dir"
  mkdir -p "$output_dir"
else
  output_dir="$work"
fi

browser_trace="$output_dir/authdoctor-browser-trace.json"
json_report="$output_dir/authdoctor-report.json"
sarif_report="$output_dir/authdoctor.sarif"

dotnet build "$PROJECT" --configuration Release --nologo
dotnet pack "$PROBE_PROJECT" --configuration Release --nologo --output "$work/packages"
package="$work/packages/AuthStateDoctor.Blazor.0.2.0-preview.1.nupkg"
test -s "$package"
unzip -l "$package" | grep -q 'lib/net10.0/AuthStateDoctor.Blazor.dll'
unzip -Z -1 "$package" | grep -Fxq 'README.md'
CLI=(dotnet run --no-build --configuration Release --project "$PROJECT" --)

sample_output="$("${CLI[@]}" test --trace "$ROOT/samples/trace.example.json")"
grep -q '\[PASS\]' <<<"$sample_output"
grep -q '\[EXPECTED-RELOAD\]' <<<"$sample_output"
"${CLI[@]}" test --trace "$ROOT/samples/trace.example.json" --format sarif > "$work/sample.sarif"
python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert d["version"] == "2.1.0" and d["$schema"].endswith("sarif-2.1.0.json")' "$work/sample.sarif"
inspect_output="$("${CLI[@]}" inspect "$PROJECT")"
grep -q '"net10.0"' <<<"$inspect_output"

mkdir -p "$work/app/Pages"
printf '%s\n' '<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' > "$work/app/App.csproj"
printf '%s\n' '@rendermode InteractiveServer' > "$work/app/Pages/Home.razor"
render_output="$("${CLI[@]}" inspect "$work/app")"
grep -q 'InteractiveServer' <<<"$render_output"

cat > "$work/mismatch.json" <<'JSON'
{
  "schema": "authdoctor-trace/v1",
  "accessToken": "SYNTHETIC_DO_NOT_LEAK_TOKEN_123",
  "observations": [{
    "scenarioId": "logout-check",
    "transition": "logout",
    "elapsedMs": 250,
    "serverAuthenticated": false,
    "uiAuthenticated": true,
    "expectation": "converge",
    "email": "synthetic@example.invalid",
    "cookie": "SYNTHETIC_COOKIE_VALUE"
  }]
}
JSON
set +e
"${CLI[@]}" test --trace "$work/mismatch.json" --format json > "$work/mismatch-report.json"
result=$?
set -e
[[ "$result" -eq 1 ]]
grep -q 'ui-stale-authenticated' "$work/mismatch-report.json"
if grep -Eq 'SYNTHETIC_DO_NOT_LEAK_TOKEN_123|synthetic@example.invalid|SYNTHETIC_COOKIE_VALUE|accessToken|email|cookie' "$work/mismatch-report.json"; then
  echo 'ERRO: dado extra ou sintético sensível vazou no relatório.' >&2
  exit 1
fi

cat > "$work/invalid.json" <<'JSON'
{"schema":"authdoctor-trace/v1","observations":[{"scenarioId":"x","transition":"unknown-transition-secret","elapsedMs":0,"serverAuthenticated":false,"uiAuthenticated":false,"expectation":"converge"}]}
JSON
set +e
"${CLI[@]}" test --trace "$work/invalid.json" > "$work/invalid.out" 2> "$work/invalid.err"
result=$?
set -e
[[ "$result" -eq 2 ]]
! grep -q 'unknown-transition-secret' "$work/invalid.err"

printf '%s\n' '{"schema":"authdoctor-trace/v1","schema":"shadow","observations":[]}' > "$work/duplicate.json"
set +e
"${CLI[@]}" test --trace "$work/duplicate.json" > "$work/duplicate.out" 2> "$work/duplicate.err"
result=$?
set -e
[[ "$result" -eq 2 ]]
! grep -q 'shadow' "$work/duplicate.err"

printf '%s\n' '{"schema":"authdoctor-trace/v1","observations":[{"scenarioId":"s","transition":"login","elapsedMs":2,"serverAuthenticated":true,"uiAuthenticated":true,"expectation":"converge"},{"scenarioId":"s","transition":"logout","elapsedMs":1,"serverAuthenticated":false,"uiAuthenticated":false,"expectation":"converge"}]}' > "$work/nonmonotonic.json"
set +e
"${CLI[@]}" test --trace "$work/nonmonotonic.json" > "$work/nonmonotonic.out" 2> "$work/nonmonotonic.err"
result=$?
set -e
[[ "$result" -eq 2 ]]

head -c 1048577 /dev/zero > "$work/oversized.json"
set +e
"${CLI[@]}" test --trace "$work/oversized.json" > "$work/oversized.out" 2> "$work/oversized.err"
result=$?
set -e
[[ "$result" -eq 2 ]]

if [[ ! -x "$ROOT/tests/browser/node_modules/.bin/playwright" ]]; then
  npm ci --prefix "$ROOT/tests/browser" --ignore-scripts --no-audit --no-fund
fi
node "$ROOT/tests/browser/run-fixture.mjs" --output "$browser_trace"
"${CLI[@]}" test --trace "$browser_trace" --format json --output "$json_report"
"${CLI[@]}" test --trace "$browser_trace" --format sarif --output "$sarif_report"
python3 - "$json_report" "$sarif_report" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as f:
    report = json.load(f)
assert report["schema"] == "authdoctor-trace/v1"
assert report["observationCount"] == 11
assert report["failures"] == 0
assert report["expectedReloads"] == 4
transitions = {item["transition"] for item in report["findings"]}
assert {"prerender", "hydrate", "login", "logout", "enhanced-navigation", "second-tab", "hard-refresh", "static-render"} <= transitions
assert {item["scenarioId"] for item in report["findings"]} == {"synthetic-cookie-fixture", "synthetic-second-tab", "synthetic-static-ssr"}

with open(sys.argv[2], encoding="utf-8") as f:
    sarif = json.load(f)
assert sarif["version"] == "2.1.0"
assert sarif["$schema"].endswith("sarif-2.1.0.json")
assert len(sarif["runs"]) == 1
PY

printf '%s\n' 'Smoke tests passaram: build/pack NuGet local, CLI, inspeção, SARIF, conformance, redação, limites de input e E2E Playwright com Interactive Server, Static SSR e multiaba.'
