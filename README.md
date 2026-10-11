# Blazor Auth State Doctor

CLI .NET para investigar divergências entre autorização observada no servidor e estado de autenticação exibido pela UI em aplicações Blazor. A versão atual combina inspeção heurística, replay de traces sanitizados e um harness de navegador local para uma fixture controlada.

> **Estado:** harness lite. O Chromium exercita uma fixture .NET 10 com cookie sintético em loopback; nenhuma credencial real ou aplicação do usuário é acessada. Isso comprova o fluxo do laboratório, não cobertura de OIDC, todos os render modes, reconexão ou diagnóstico automático de aplicações arbitrárias.

## Requisitos

- .NET SDK 10.0.
- Node.js 22+ e npm.
- Chromium instalado pelo Playwright.

## Executar o harness de navegador

```bash
npm ci --prefix tests/browser
(cd tests/browser && npx playwright install chromium)
node tests/browser/run-fixture.mjs --output /tmp/authdoctor-browser-trace.json
dotnet run --project src/AuthStateDoctor -- test --trace /tmp/authdoctor-browser-trace.json
```

O runner publica a fixture em diretório temporário, inicia o servidor em `127.0.0.1`, verifica uma tentativa POST com `Origin` externo, exercita prerender/hydration/login/logout/enhanced navigation/hard refresh e encerra navegador e servidor. O trace guarda só identificador sintético, transição, tempo relativo, dois booleanos e expectativa declarada. Não inclui cookies, tokens, claims, HTML ou dados pessoais.

A fixture usa um usuário sintético e **não solicita nem aceita credenciais**. Seus endpoints de teste exigem loopback e POST same-origin. O cookie é HttpOnly, SameSite=Strict e expira em 15 minutos. Não instale esses endpoints numa aplicação real.

## Compilar e inspecionar

```bash
dotnet build src/AuthStateDoctor/AuthStateDoctor.csproj --configuration Release
dotnet run --project src/AuthStateDoctor -- inspect /caminho/para/app-ou-pasta
```

A inspeção de render modes é heurística textual e não substitui análise compilada/runtime. O checker compara o estado declarado no trace; não determina sozinho se uma divergência ou expectativa é correta.

## Relatórios e traces

```bash
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json --format json --output authdoctor-report.json
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json --format sarif --output authdoctor.sarif
```

O contrato está em [`schemas/authdoctor-trace-v1.schema.json`](schemas/authdoctor-trace-v1.schema.json), com exemplo em [`samples/trace.example.json`](samples/trace.example.json). O leitor limita o trace a 1 MiB, 10.000 observações e 16 níveis JSON; rejeita propriedades duplicadas, timestamps não monotônicos por cenário e transições fora do vocabulário. Campos extras são ignorados e nunca copiados à saída.

Transições aceitas: `prerender`, `hydrate`, `login`, `logout`, `hard-refresh`, `enhanced-navigation`, `reconnect`, `second-tab`.

Expectativas aceitas: `converge` e `full-reload-required`.

- `0`: nenhuma divergência não esperada; reloads declarados são reportados como boundary esperado.
- `1`: divergência não esperada entre estado do servidor e estado da UI.
- `2`: uso/entrada inválida ou falha operacional.

## Testes e CI

```bash
bash tests/smoke.sh
```

A suíte compila o CLI, exercita contrato/limites/redação, inicia Chromium e valida o trace real da fixture. A GitHub Action roda os mesmos testes e guarda JSON/SARIF sanitizados como artifacts; isso depende de runners Actions funcionais.

## Segurança e limites

Leia [`SECURITY.md`](SECURITY.md) antes de manipular traces e [`docs/browser-harness-lite.md`](docs/browser-harness-lite.md) para arquitetura e interpretação dos resultados. `AuthorizeView` é apresentação, não uma fronteira de segurança: autorização de recursos deve permanecer no servidor.

## Próximas etapas

1. Criar um adapter opt-in de observação para aplicações reais sem endpoints de autenticação sintética.
2. Validar o contrato com uma aplicação-alvo e fluxo real de IdP/OIDC, sem gravar tokens ou claims.
3. Ampliar testes de reconexão, múltiplas abas e matriz de render modes, com cobertura versionada.
4. Só então tratar achados como recomendações específicas para produção; o MVP não promete auditoria abrangente.
