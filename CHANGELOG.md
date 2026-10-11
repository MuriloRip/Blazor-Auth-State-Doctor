# Changelog

## Unreleased — harness de navegador lite

- Adicionada fixture Blazor Interactive Server .NET 10 com cookie e identidade sintéticos; endpoints de teste restritos a loopback e POST same-origin.
- Adicionado runner Playwright fixado por lockfile, com publicação temporária e verificação de prerender, hydration, login, logout, probe protegido, enhanced navigation e hard refresh.
- Runner gera somente o contrato sanitizado `authdoctor-trace/v1`; novos smoke checks cobrem a saída real da fixture, JSON/SARIF, visibilidade HttpOnly, tentativa off-origin e limites do parser.
- Atualizada a GitHub Action para executar Chromium E2E e guardar artifacts JSON/SARIF sanitizados por sete dias, com permissões mínimas.
- Documentados os limites do harness e as referências oficiais sobre estado de autenticação e autorização no Blazor.

## MVP inicial — CLI

- CLI .NET 10 com `inspect` heurístico de TFM, SDK e render modes.
- Contrato JSON de trace e replay determinístico de checkpoints tipados.
- Saídas terminal, JSON e SARIF; código 1 para divergência não esperada.
- Limite de input, rejeição de propriedades duplicadas, enums restritos e projeção de campos permitidos.
- Smoke tests, schema JSON, amostra e workflow básico de CI.

## Ainda não faz parte do harness lite

Não há adapter de integração para aplicações reais, fluxo OIDC/IdP, coleta de tokens/claims, matriz completa de render modes, testes de reconexão/múltiplas abas ou afirmação de cobertura abrangente. Essas etapas exigem integração opt-in e validação com uma aplicação-alvo não produtiva.
