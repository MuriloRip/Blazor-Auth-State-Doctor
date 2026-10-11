# Harness de navegador lite

## Fixture e adapter

`samples/DiagnosticFixture` é uma aplicação Blazor Web App .NET 10 Interactive Server com cookie sintético. `samples/StaticSsrFixture` é uma aplicação separada, configurada pelo template oficial sem interatividade. São dois processos publicados temporariamente pelo runner: separar as fixtures preserva os testes de Enhanced Navigation no app interativo e valida Static SSR pelo pipeline normal de componentes, sem simular uma rota estática dentro de um circuito.

`tests/browser/run-fixture.mjs` verifica que a extensão server-side recusa Production, inicia as duas apps no ambiente `Testing` em loopback e exercita prerender/hydration/login/logout/enhanced navigation/hard refresh/segunda aba e Static SSR. Na fixture estática, o runner confirma ausência dos marcadores interativos e que nenhum novo circuito WebSocket é aberto.

A fixture consome `src/AuthStateDoctor.Blazor`, uma Razor Class Library com `<AuthStateProbe>` e `MapAuthStateDoctorServerProbe()`. O componente observa somente `AuthenticationStateProvider` e emite um booleano oculto; `Enabled=false` por padrão, sem markup ou assinatura de eventos quando desabilitado. A fixture tem uma instância habilitada e outra default-disabled e o E2E valida a cardinalidade/shape do DOM.

A extensão server-side mapeia `GET /_authdoctor/server-state`, retorna só `{ "authenticated": boolean }`, exige ambiente Development/Testing e restringe o request a remote address loopback e host local. É opt-in e `AllowAnonymous` porque o único dado é o booleano; não exponha essa rota fora do servidor de diagnóstico local. A fixture ainda tem um cookie/login sintético separado; não copie esses endpoints de mutação para uma aplicação real.

O cenário multiaba abre uma segunda página depois do login, verifica autenticação, executa logout na primeira, observa a sessão server-side como não autenticada e o estado do circuito da segunda página ainda autenticado, e confirma convergência depois do hard refresh. Isso caracteriza o comportamento da fixture, não uma regra universal para todos os provedores/render modes.

## Interpretação correta

`AuthStateProbe` reporta apenas o estado entregue ao componente. `MapAuthStateDoctorServerProbe` reporta apenas se a requisição local atual está autenticada. Nenhuma das sondas valida policies de um recurso real, substitui autorização server-side ou prova que todos os elementos visuais estejam corretos.

O harness classifica divergências no cenário sintético; não declara que todas as aplicações precisam sempre recarregar. A documentação Microsoft descreve `AuthenticationStateProvider` e notificações customizadas ([estado de autenticação](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/authentication-state?view=aspnetcore-10.0)); e alerta que autorização no cliente não é enforcement confiável ([segurança Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)). No exemplo OIDC com estado persistido, a documentação exige full reload para login/logout porque o estado WebAssembly persistido fica fixo pela vida da aplicação ([OIDC em Blazor Web Apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)); essa regra é específica ao modelo documentado.

## Integração e execução

Veja [`adapter-integration.md`](adapter-integration.md) para empacotar localmente e habilitar as sondas só em Development/Testing. O pacote preview não foi publicado. A fixture de browser pode ser executada assim:

```bash
npm ci --prefix tests/browser
(cd tests/browser && npx playwright install chromium)
node tests/browser/run-fixture.mjs --output /tmp/authdoctor-browser-trace.json
dotnet run --project src/AuthStateDoctor -- test --trace /tmp/authdoctor-browser-trace.json
```

O runner remove o publish temporário, encerra o processo e o navegador e não imprime HTML, cookies, tokens, claims, IPs de usuário ou dados pessoais.
