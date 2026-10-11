#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/AuthStateDoctor/AuthStateDoctor.csproj"
CLI=(dotnet run --no-build --project "$PROJECT" --)
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dotnet build "$PROJECT" --configuration Release --nologo
CLI=(dotnet run --no-build --configuration Release --project "$PROJECT" --)

sample_output="$("${CLI[@]}" test --trace "$ROOT/samples/trace.example.json")"
grep -q '\[PASS\]' <<<"$sample_output"
grep -q '\[EXPECTED-RELOAD\]' <<<"$sample_output"
"${CLI[@]}" test --trace "$ROOT/samples/trace.example.json" --format sarif > "$work/report.sarif"
python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert d["version"] == "2.1.0" and d["$schema"].endswith("sarif-2.1.0.json")' "$work/report.sarif"
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
"${CLI[@]}" test --trace "$work/mismatch.json" --format json > "$work/report.json"
result=$?
set -e
[[ "$result" -eq 1 ]]
grep -q 'ui-stale-authenticated' "$work/report.json"
if grep -Eq 'SYNTHETIC_DO_NOT_LEAK_TOKEN_123|synthetic@example.invalid|SYNTHETIC_COOKIE_VALUE|accessToken|email|cookie' "$work/report.json"; then
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

echo 'Smoke tests passaram: build, CLI/inspeção, render mode, SARIF, conformance, redaction e limites de input.'
