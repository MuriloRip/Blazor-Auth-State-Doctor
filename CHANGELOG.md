# Changelog

## Unreleased — sondas opt-in e cenário multiaba

- `AuthStateDoctor.Blazor` agora oferece `<AuthStateProbe>` (booleano UI, `Enabled=false` por padrão) e `MapAuthStateDoctorServerProbe()` (booleano server-side apenas em Development/Testing e loopback).
- Fixture usa os dois probes da biblioteca e verifica que a extensão falha ao mapear em Production, além de resposta autorizada/não autorizada.
- Adicionada uma fixture independente gerada pelo template oficial Static SSR; runner confirma que não há marcadores de hydration nem novo circuito WebSocket, preservando Enhanced Navigation na fixture Interactive Server.
- Runner cobre segunda aba autenticada, estado de circuito obsoleto após logout e convergência após hard refresh; contrato de trace permanece sanitizado.
- Smoke test empacota `0.2.0-preview.1` localmente, sem publicar, e valida 11 checkpoints com 0 falhas inesperadas.
- Documentada integração não produtiva, limites de autorização e restrições de ambiente/rede.

## Unreleased — harness de navegador lite

- Adicionada fixture Blazor Interactive Server .NET 10 com cookie e identidade sintéticos; endpoints de mutação restritos a loopback e POST same-origin.
- Adicionado runner Playwright fixado por lockfile, com publicação temporária e verificação de prerender, hydration, login, logout, probe protegido, enhanced navigation e hard refresh.
- Runner gera somente o contrato sanitizado `authdoctor-trace/v1`; smoke checks cobrem JSON/SARIF, visibilidade HttpOnly, tentativa off-origin e limites do parser.
- GitHub Action executa Chromium E2E e guarda artifacts JSON/SARIF sanitizados por sete dias, com permissões mínimas.

## MVP inicial — CLI

- CLI .NET 10 com `inspect` heurístico de TFM, SDK e render modes.
- Contrato JSON de trace e replay determinístico de checkpoints tipados.
- Saídas terminal, JSON e SARIF; código 1 para divergência não esperada.
- Limite de input, rejeição de propriedades duplicadas, enums restritos e projeção de campos permitidos.
- Smoke tests, schema JSON, amostra e workflow básico de CI.

## Limites atuais

Não há runner genérico para aplicação externa, fluxo OIDC/IdP, coleta de tokens/claims, matriz completa de render modes, reconexão ou afirmação de cobertura abrangente. A cobertura multiaba é apenas o cenário sintético da fixture. O preview NuGet ainda não foi publicado.
