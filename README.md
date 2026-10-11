# Blazor Auth State Doctor

CLI .NET para investigar divergências entre autorização observada no servidor e estado de autenticação exibido pela UI em aplicações Blazor. O repositório combina inspeção heurística, replay de traces sanitizados, um harness local Playwright e uma pequena biblioteca Blazor opt-in.

> **Estado:** preview lite. O Chromium exercita uma fixture .NET 10 com cookie sintético em loopback; nenhuma credencial real ou aplicação do usuário é acessada. A biblioteca fornece apenas uma observação booleana do estado visível ao componente. Isso ainda não é cobertura de OIDC, de todos os render modes, reconexão ou diagnóstico automático de aplicações arbitrárias.

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

A fixture usa um usuário sintético e **não solicita nem aceita credenciais**. Seus endpoints de teste exigem loopback e POST same-origin. O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. Não instale esses endpoints numa aplicação real.

## Adapter opt-in para Blazor

O projeto [`AuthStateDoctor.Blazor`](src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj) contém `<AuthStateProbe>`, que expõe somente `true`/`false`/`unknown` do `AuthenticationStateProvider` através de um atributo DOM oculto. `Enabled` é `false` por padrão: componente desabilitado não produz markup nem assina eventos.

O preview NuGet ainda não foi publicado. Consulte [`docs/adapter-integration.md`](docs/adapter-integration.md) para criar pacote local e habilitar a sonda somente em Development/teste. Não a habilite em produção: scripts da mesma origem podem ler o atributo. A sonda não acessa claims/cookies/tokens e **não prova autorização server-side**.

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

Transições aceitas: `prerender`, `hydrate`, `login`, `logout`, `hard-refresh`, `enhanced-navigation`, `reconnect`, `second-tab`.

Expectativas aceitas: `converge` e `full-reload-required`.

- `0`: nenhuma divergência não esperada; reloads declarados são reportados como boundary esperado.
- `1`: divergência não esperada entre estado do servidor e estado da UI.
- `2`: uso/entrada inválida ou falha operacional.

## Testes e CI

```bash
bash tests/smoke.sh
```

A suíte compila a CLI e o adapter, empacota o preview NuGet sem publicá-lo, exercita contrato/limites/redação, inicia Chromium e valida o trace real da fixture. A GitHub Action roda os mesmos testes e guarda JSON/SARIF sanitizados como artifacts; isso depende de runners Actions funcionais.

## Segurança e limites

Leia [`SECURITY.md`](SECURITY.md) antes de manipular traces e [`docs/browser-harness-lite.md`](docs/browser-harness-lite.md) para arquitetura e interpretação. `AuthorizeView`, rotas e a sonda DOM são apresentação; autorização de recursos deve permanecer no servidor.

## Próximas etapas

1. Generalizar o runner para conectar a uma app não produtiva fornecida pelo usuário sem assumir fluxo de login ou armazenar credenciais.
2. Definir e testar uma sonda server-side opt-in que exponha apenas autorização booleana, sem substituir políticas do app.
3. Validar o contrato com um fluxo IdP/OIDC de teste, sem gravar tokens ou claims.
4. Ampliar testes de reconexão, múltiplas abas e matriz de render modes; só então tratar achados como recomendações específicas para produção.
