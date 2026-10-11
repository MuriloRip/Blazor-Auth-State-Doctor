# Blazor Auth State Doctor

CLI .NET para investigar divergências entre autorização observada no servidor e estado de autenticação exibido pela UI em aplicações Blazor.

> **Estado:** MVP inicial. A versão atual descobre metadados básicos do projeto e reproduz/classifica traces JSON sanitizados. Ainda não dirige um navegador nem executa login/logout automaticamente.

## Requisitos

- .NET SDK 10.0 para compilar esta primeira versão.
- Sem dependências NuGet de terceiros no runtime.

## Compilar

```bash
dotnet build src/AuthStateDoctor/AuthStateDoctor.csproj --configuration Release
```

## Uso

Descobrir TFM, SDK e render modes reconhecidos por busca textual:

```bash
dotnet run --project src/AuthStateDoctor -- inspect /caminho/para/app-ou-pasta
```

Executar replay do trace de exemplo:

```bash
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json
```

Gerar JSON ou SARIF:

```bash
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json --format json --output authdoctor-report.json
dotnet run --project src/AuthStateDoctor -- test --trace samples/trace.example.json --format sarif --output authdoctor.sarif
```

### Códigos de saída

- `0`: nenhuma divergência não esperada; uma divergência declarada `full-reload-required` é reportada como comportamento esperado.
- `1`: pelo menos uma divergência não esperada entre os estados server-side e visual em um checkpoint.
- `2`: uso/entrada inválida ou falha operacional.

## Trace v1

O contrato está descrito em [`schemas/authdoctor-trace-v1.schema.json`](schemas/authdoctor-trace-v1.schema.json), com exemplo em [`samples/trace.example.json`](samples/trace.example.json). Cada observação representa um checkpoint já obtido por um harness (ou fixture) e contém somente identificadores sintéticos, transição, tempo relativo e dois booleanos. O checker não executa ações no app nem determina se a expectativa informada está correta; ele compara os dados declarados e apresenta orientação investigativa.

O leitor limita o trace a 1 MiB, a 10.000 observações e a 16 níveis JSON; exige timestamps relativos não decrescentes por cenário, rejeita propriedades duplicadas e transições fora do vocabulário. Campos adicionais são ignorados e nunca copiados para a saída.

Transições aceitas: `prerender`, `hydrate`, `login`, `logout`, `hard-refresh`, `enhanced-navigation`, `reconnect`, `second-tab`.

Expectativas aceitas: `converge` e `full-reload-required`.

## Testes

```bash
bash tests/smoke.sh
```

## Segurança

Leia [`SECURITY.md`](SECURITY.md) antes de usar em CI ou manipular traces. Nunca inclua segredos, tokens, cookies ou dados pessoais nos traces.

## Próximas etapas

1. Congelar o contrato de trace e ampliar testes de robustez/segurança.
2. Implementar a página de diagnóstico controlada e um runner Playwright para cenários reais.
3. Validar a coleta de estado server-side sem expor claims/segredos.
4. Integrar GitHub Actions, SARIF e teste de full reload com documentação oficial.
5. Expandir reconexão, múltiplas abas, IdPs e matriz de render modes após pilotos.
