# Changelog

## Unreleased — adapter Blazor opt-in

- Adicionado `AuthStateDoctor.Blazor`, uma Razor Class Library .NET 10 com `<AuthStateProbe>` para expor apenas `true`/`false`/`unknown` do `AuthenticationStateProvider`.
- `Enabled` é `false` por padrão; quando desabilitado, o componente não gera markup e não se inscreve nos eventos de estado.
- Fixture E2E usa uma instância habilitada e outra default-disabled; o runner valida cardinalidade/shape do elemento DOM e continua cobrindo prerender, login/logout, navegação e hard refresh.
- Smoke test empacota `0.2.0-preview.1` localmente, confirma o assembly do pacote e não publica nada externamente.
- Documentada integração para ambientes Development/teste e o limite essencial: sonda de UI não comprova autorização server-side.

## Unreleased — harness de navegador lite

- Adicionada fixture Blazor Interactive Server .NET 10 com cookie e identidade sintéticos; endpoints de teste restritos a loopback e POST same-origin.
- Adicionado runner Playwright fixado por lockfile, com publicação temporária e verificação de prerender, hydration, login, logout, probe protegido, enhanced navigation e hard refresh.
- Runner gera somente o contrato sanitizado `authdoctor-trace/v1`; smoke checks cobrem a saída real da fixture, JSON/SARIF, visibilidade HttpOnly, tentativa off-origin e limites do parser.
- GitHub Action executa Chromium E2E e guarda artifacts JSON/SARIF sanitizados por sete dias, com permissões mínimas.

## MVP inicial — CLI

- CLI .NET 10 com `inspect` heurístico de TFM, SDK e render modes.
- Contrato JSON de trace e replay determinístico de checkpoints tipados.
- Saídas terminal, JSON e SARIF; código 1 para divergência não esperada.
- Limite de input, rejeição de propriedades duplicadas, enums restritos e projeção de campos permitidos.
- Smoke tests, schema JSON, amostra e workflow básico de CI.

## Limites atuais

Não há runner genérico para uma aplicação externa, fluxo OIDC/IdP, coleta de tokens/claims, matriz completa de render modes, testes de reconexão/múltiplas abas ou afirmação de cobertura abrangente. O preview NuGet ainda não foi publicado.
