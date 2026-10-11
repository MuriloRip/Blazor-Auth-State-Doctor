# Blazor Auth State Doctor

CLI .NET para investigar divergências entre autorização observada no servidor e estado de autenticação exibido pela UI em aplicações Blazor. O repositório combina inspeção heurística, replay de traces sanitizados, um harness local Playwright e uma pequena biblioteca Blazor opt-in.

> **Estado:** preview lite. O Chromium exercita fixtures .NET 10 em loopback: uma Interactive Server com cookie sintético e outra exclusivamente Static SSR. Nenhuma credencial real ou aplicação do usuário é acessada. A biblioteca oferece sondas para UI e servidor, mas isso ainda não é cobertura de OIDC, de todos os render modes, reconexão ou diagnóstico automático de aplicações arbitrárias.

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

O runner publica as duas fixtures em diretórios temporários, confirma que a sonda server-side **falha fechada** fora de Development/Testing, inicia os servidores Testing em `127.0.0.1`, rejeita uma tentativa POST off-origin e exercita prerender/hydration/login/logout/segunda aba/enhanced navigation/hard refresh/Static SSR. A execução validada gerou onze checkpoints, incluindo logout obsoleto em circuito, convergência após reload da segunda aba e confirmação de que Static SSR não abre circuito interativo. O trace guarda apenas identificador sintético, transição, tempo relativo, dois booleanos e expectativa; não inclui cookies, tokens, claims, HTML ou dados pessoais.

A fixture usa um usuário sintético e **não solicita nem aceita credenciais**. Seus endpoints exigem loopback e POST same-origin. O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. Não instale esses endpoints numa aplicação real.

## Adapter opt-in para Blazor

O projeto [`AuthStateDoctor.Blazor`](src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj) contém:

- `<AuthStateProbe>`: expõe no DOM somente `true`/`false`/`unknown` do `AuthenticationStateProvider`; `Enabled` é `false` por padrão.
- `MapAuthStateDoctorServerProbe()`: mapeia `GET /_authdoctor/server-state` e retorna apenas `{ "authenticated": boolean }`. Só aceita ser mapeado em Development/Testing e restringe chamadas a loopback/local host.

O preview NuGet ainda não foi publicado. Consulte [`docs/adapter-integration.md`](docs/adapter-integration.md) para criar um pacote local e habilitar as sondas apenas em Development/teste. Não as habilite em produção: a sonda de UI expõe um booleano de autenticação a scripts da mesma origem e a sonda server-side existe apenas para teste local.

Nenhuma sonda substitui autorização. A rota server-side usa `AllowAnonymous` somente porque o resultado é um booleano e a implementação impõe ambiente de teste e loopback; revisar o código antes de qualquer uso diferente.

## Compilar, empacotar e inspecionar

```bash
dotnet build src/AuthStateDoctor/AuthStateDoctor.csproj --configuration Release
dotnet pack src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj --configuration Release --output ./artifacts/nuget
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

Transições aceitas: `prerender`, `hydrate`, `login`, `logout`, `hard-refresh`, `enhanced-navigation`, `reconnect`, `second-tab`, `static-render`.

Expectativas aceitas: `converge` e `full-reload-required`.

- `0`: nenhuma divergência não esperada; reloads declarados são reportados como boundary esperado.
- `1`: divergência não esperada entre estado do servidor e estado da UI.
- `2`: uso/entrada inválida ou falha operacional.

## Testes e CI

```bash
bash tests/smoke.sh
```

A suíte compila a CLI e o adapter, empacota o preview NuGet sem publicá-lo, exercita contrato/limites/redação, inicia Chromium e valida Interactive Server, Static SSR, segunda aba e fail-closed em Production. A GitHub Action roda os mesmos testes e guarda JSON/SARIF sanitizados como artifacts; a CI remota ainda depende de runners Actions funcionais.

## Segurança e limites

Leia [`SECURITY.md`](SECURITY.md) e [`docs/adapter-integration.md`](docs/adapter-integration.md) antes de habilitar sondas. `AuthorizeView`, rotas e sondas DOM não são fronteiras de autorização: recursos protegidos devem ser autorizados no servidor.

## Próximas etapas

1. Generalizar o runner para se conectar a uma aplicação não produtiva fornecida pelo usuário sem assumir seletores, login ou credenciais.
2. Validar o contrato com um fluxo IdP/OIDC de teste, sem gravar tokens ou claims.
3. Expandir cenários de reconexão, matriz de render modes e múltiplas abas além da fixture sintética.
4. Só então tratar achados como recomendações específicas para produção.
