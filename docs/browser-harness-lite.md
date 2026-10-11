# Harness de navegador lite

## Fixture e adapter

A fixture `samples/DiagnosticFixture` é uma aplicação Blazor Web App .NET 10 com render mode Interactive Server e um handler de cookie exclusivo para testes. `tests/browser/run-fixture.mjs` publica o app em diretório temporário, inicia o executável em loopback, abre Chromium, exercita prerender/hydration/login/logout/enhanced navigation/hard refresh e escreve somente observações do contrato `authdoctor-trace/v1`.

A fixture consome o projeto `src/AuthStateDoctor.Blazor`, cuja Razor Class Library contém `<AuthStateProbe>`. A sonda observa exclusivamente `AuthenticationStateProvider` e emite um elemento DOM oculto com `true`/`false`/`unknown`. Ela é **opt-in e desabilitada por padrão**; quando desabilitada, não gera markup nem assina os eventos do provider. O E2E verifica que só a instância explicitamente habilitada aparece e que o elemento não possui texto nem filhos.

A identidade criada pelo endpoint de fixture é sintética, não aceita credenciais e não representa fluxo OIDC. Os endpoints `/_authdoctor/*` só respondem a conexões loopback; POST exige `Origin` same-origin. O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. O probe de estado retorna apenas um booleano, e o probe protegido aplica autorização server-side.

## Interpretação correta

`AuthStateProbe` reporta somente o estado disponível a um componente Blazor. Não coleta, infere nem garante o estado server-side de uma aplicação real, não valida seu elemento visual particular e não é uma fronteira de autorização. A fixture consulta separadamente seu endpoint protegido. Aplicações reais precisam manter autorização no servidor e criar sondas de teste específicas, sem retornar dados pessoais.

O harness classifica divergências no cenário sintético; não declara que todos os render modes ou aplicações precisam sempre recarregar. A documentação Microsoft descreve `AuthenticationStateProvider` e notificações de mudança para provedores customizados ([estado de autenticação](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/authentication-state?view=aspnetcore-10.0)). O guia geral alerta que autorização executada no cliente não é enforcement confiável ([segurança Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)). No exemplo OIDC com estado persistido, a documentação afirma que login/logout exige full page reload porque o estado WebAssembly persistido fica fixo pela vida da aplicação ([OIDC em Blazor Web Apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)); essa regra é específica ao modelo documentado, não generalizada pelo checker.

## Integração controlada

Veja [`adapter-integration.md`](adapter-integration.md) para empacotar localmente a biblioteca e habilitá-la somente em Development/teste. O preview não foi publicado no NuGet.org. Não habilite a sonda em produção: ela torna um booleano de autenticação legível por scripts da mesma origem.

## Executar localmente

```bash
npm ci --prefix tests/browser
(cd tests/browser && npx playwright install chromium)
node tests/browser/run-fixture.mjs --output /tmp/authdoctor-browser-trace.json
dotnet run --project src/AuthStateDoctor -- test --trace /tmp/authdoctor-browser-trace.json
```

O runner publica e remove artefatos temporários, encerra o processo filho e o navegador e não imprime HTML, cookies, tokens, claims, IPs de usuário ou dados pessoais.
