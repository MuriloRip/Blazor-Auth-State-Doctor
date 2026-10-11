# Requisitos e backlog — Blazor Auth State Doctor

> **Status:** estrutura inicial do produto, baseada no material de requisitos fornecido.
>
> **Objetivo deste documento:** consolidar a proposta da primeira release completa e transformar os requisitos em uma referência legível para implementação, validação e evolução do projeto.

## 1. Visão do produto

### 1.1 Objetivo

Detectar e explicar divergências entre a autorização server-side e o estado visual da UI em aplicações Blazor, especialmente nos limites de prerender, hydration, navegação, cookies/OIDC, logout e reconexão.

### 1.2 Usuários-alvo

- Desenvolvedor .NET/Blazor.
- Maintainer de biblioteca ou template Blazor.
- Engenheiro de QA/DevEx responsável por testes de regressão.
- Equipe que recebeu incidentes de login/logout aparentemente inconsistentes.

## 2. Escopo da primeira release completa

| Área | Decisão para a primeira release |
|---|---|
| Plataforma principal | .NET 10 |
| Compatibilidade adicional | Limitada com .NET 8 |
| Autenticação | Cookie authentication e um fluxo OIDC configurável |
| Matriz de renderização | Dois render modes selecionados |
| Execução | Local e via GitHub Action |
| Tipo de diagnóstico | Somente leitura |
| Saídas | Terminal, JSON e SARIF |
| Segurança dos dados | Nenhum token, cookie ou claim sensível deve aparecer nos logs/relatórios |

## 3. Requisitos funcionais

| ID | Requisito | Prioridade |
|---|---|---|
| P1-RF-01 | Descobrir a versão do SDK, TFM, modo de renderização e configuração relevante da aplicação. | P0 |
| P1-RF-02 | Executar cenários de prerender, hydration, login, logout, hard refresh e enhanced navigation. | P0 |
| P1-RF-03 | Permitir configurar um usuário de teste sem armazenar credenciais no relatório. | P0 |
| P1-RF-04 | Validar se o estado visual converge para o estado observado no servidor após cada transição. | P0 |
| P1-RF-05 | Distinguir falha de autorização server-side, falha de propagação do estado e comportamento esperado que exige full reload. | P0 |
| P1-RF-06 | Executar cenário de reconexão e, opcionalmente, cenário com duas abas. | P1 |
| P1-RF-07 | Gerar trace temporal com eventos sanitizados e indicação do boundary provável. | P0 |
| P1-RF-08 | Gerar SARIF para integração com code scanning. | P1 |
| P1-RF-09 | Incluir links para documentação oficial e orientação contextual. | P1 |
| P1-RF-10 | Funcionar como CLI e como integração opcional de teste/NuGet. | P1 |

## 4. Histórias de usuário e critérios de aceite

### P1-US-01 — Executar um diagnóstico básico

**História:** Como desenvolvedor, quero executar `authdoctor test` em meu projeto para saber se há divergência de estado.

**Critérios de aceite:**

- O comando identifica o projeto.
- Informa as versões detectadas.
- Termina com código de saída diferente de zero somente quando há falha de conformance.
- Não exige alteração permanente no código.

### P1-US-02 — Entender a causa provável

**História:** Como desenvolvedor, quero saber em qual transição o estado divergiu.

**Critérios de aceite:**

- O relatório mostra cenário, estado esperado, estado observado, momento da divergência e classificação de causa.
- A ferramenta não afirma que todo caso é bug do framework.

### P1-US-03 — Executar com segurança no CI

**História:** Como maintainer, quero rodar o diagnóstico no GitHub Actions sem vazar segredos.

**Critérios de aceite:**

- Credenciais podem vir de secrets do CI.
- Logs são sanitizados.
- Cookies, tokens e claims não aparecem.
- O job publica JSON/SARIF como artefato.

### P1-US-04 — Evitar falso positivo em full reload

**História:** Como desenvolvedor, quero que o harness reconheça fluxos em que a documentação exige recarregamento completo.

**Critérios de aceite:**

- O teste diferencia estado temporariamente não convergente de comportamento documentado.
- A recomendação indica quando um full reload é esperado.

## 5. Backlog técnico

| ID | Prioridade | Item |
|---|---|---|
| P1-B01 | P0 | Definir modelo de eventos sanitizados e contrato JSON do resultado. |
| P1-B02 | P0 | Criar runner Playwright mínimo para login/logout/hard refresh. |
| P1-B03 | P0 | Criar página de diagnóstico controlada com estados conhecidos. |
| P1-B04 | P0 | Implementar comparação server-side versus UI. |
| P1-B05 | P0 | Implementar redaction de secrets e claims. |
| P1-B06 | P1 | Adicionar adapter para GitHub Actions e SARIF. |
| P1-B07 | P1 | Adicionar reconexão e cenário de duas abas. |
| P1-B08 | P2 | Ampliar matriz de IdPs, render modes e versões. |

## 6. Fora da primeira release

- Corrigir automaticamente Identity, OIDC ou autorização.
- Capturar ou decodificar tokens reais.
- Suportar todos os provedores de identidade.
- Declarar compatibilidade com qualquer versão do .NET.
- Substituir bUnit ou Playwright.

## 7. Métricas de validação

- Pelo menos 5 equipes conseguem instalar e executar o diagnóstico.
- O relatório identifica corretamente falhas sem acesso a segredos.
- Usuários conseguem distinguir falha real de comportamento esperado.
- Taxa de falsos positivos aceitável, definida com os pilotos.
- Pelo menos 2 casos reais reproduzidos ou descartados com explicação.

## 8. Sequenciamento inicial sugerido

A sequência abaixo organiza os itens existentes por dependência prática; não altera as prioridades registradas no backlog.

1. **Contrato e segurança:** P1-B01 e P1-B05.
2. **Harness mínimo:** P1-B03 e P1-B02.
3. **Detecção de conformance:** P1-B04, cobrindo os cenários P1-RF-02 e a distinção prevista em P1-RF-05.
4. **Experiência de diagnóstico:** produzir trace temporal (P1-RF-07) e validar os critérios das histórias P1-US-01, P1-US-02 e P1-US-04.
5. **Integração de CI:** P1-B06, com saídas JSON/SARIF e credenciais via secrets, conforme P1-US-03.
6. **Expansão de cobertura:** P1-B07 e, posteriormente, P1-B08.

## 9. Pontos a detalhar nas próximas iterações

- Quais são os dois render modes que compõem a matriz inicial.
- Qual fluxo OIDC servirá como referência e quais parâmetros serão configuráveis.
- Como o usuário de teste será provisionado e como as credenciais serão fornecidas localmente e no CI.
- Quais eventos, campos e regras de redaction compõem o contrato sanitizado.
- Como será definido o limiar de convergência do estado visual e a classificação de causa provável.
- Quais códigos de saída e níveis de severidade serão usados em terminal, JSON e SARIF.
- Quais casos documentados exigem full reload e como serão representados no diagnóstico.
- Quais equipes participarão dos pilotos e como será medida a taxa aceitável de falsos positivos.


## 10. Estado de implementação (checkpoint de 2026-10-11)

| Área | Status verificável | Limite atual |
|---|---|---|
| Contrato sanitizado e parser | Implementado e coberto por smoke tests. | Só observa os campos v1 definidos. |
| Fixtures Playwright controladas | Implementadas em dois apps .NET 10: Interactive Server com cookie sintético e app Static SSR oficial separado. | Não são login de usuário nem fluxo OIDC; faltam reconexão e cobertura ampla de render modes. |
| Probe de componente Blazor | `AuthStateDoctor.Blazor` preview; emite só booleano do `AuthenticationStateProvider`; `Enabled=false` por padrão. | Observa estado de UI, não autorização no servidor. |
| Comparação UI/servidor | Fixture usa os probes opt-in da biblioteca: componente UI e endpoint server-side local. | A fixture ainda não valida configuração ou policies de uma aplicação externa. |
| Empacotamento NuGet | Pacote `0.2.0-preview.1` criado e validado localmente. | Não publicado em registry público. |
| Relatórios e CI | CLI produz terminal/JSON/SARIF; workflow gera artifacts sanitizados. | As execuções do PR #3 terminaram antes de alocar runner (`runner_id=0`, sem logs); CI remota permanece não validada. |
| Multiaba | Um cenário sintético cobre cookie compartilhado, circuito obsoleto após logout e reload convergente. | Não é matriz geral de browser/IdP/render mode. |
| OIDC e reconexão | Não implementados. | Requer fixture/IdP e matriz de teste dedicada. |

A próxima etapa escolhida é um runner de conexão a uma aplicação não produtiva sem assumir seletores, protocolo de login ou credenciais: observará apenas as sondas opt-in e manterá a autenticação sob controle do desenvolvedor. A integração seguirá em branch/PR, sem publicar o pacote ou modificar `main` diretamente.
