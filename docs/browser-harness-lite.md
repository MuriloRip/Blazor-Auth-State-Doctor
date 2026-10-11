# Harness de navegador lite

## O que cobre

A fixture `samples/DiagnosticFixture` é uma aplicação Blazor Web App .NET 10 com render mode Interactive Server e um handler de cookie exclusivo para testes. `tests/browser/run-fixture.mjs` publica o app em diretório temporário, inicia o executável em loopback, abre Chromium, exercita prerender/hydration/login/logout/enhanced navigation/hard refresh e escreve somente observações do contrato `authdoctor-trace/v1`. A CLI consome esse trace sem receber cookies ou claims.

A identidade criada pelo endpoint é sintética, não aceita credenciais e não representa fluxo OIDC. Os endpoints `/_authdoctor/*` só respondem a conexões loopback; POST exige `Origin` same-origin. O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. O probe de estado retorna apenas um booleano, e o probe protegido aplica autorização server-side.

A fixture é um laboratório local, não uma biblioteca para registrar na aplicação do usuário nem um serviço de produção. Para diagnóstico de aplicações reais, o passo seguinte é um adapter opt-in que coleta eventos sanitizados sem adicionar endpoints de login sintético à aplicação alvo.

## Interpretação correta

`AuthorizeView`/`AuthenticationStateProvider` descrevem o estado observado pela interface; não substituem uma regra de autorização do servidor. O harness consulta em separado um endpoint protegido e uma sonda server-side que retorna apenas `authenticated`. O teste classifica uma divergência declarada como `full-reload-required` quando a UI do circuito ainda reflete a autenticação anterior; esse resultado pertence à fixture e **não** declara que todos os render modes ou aplicações precisam sempre recarregar.

A documentação Microsoft descreve `AuthenticationStateProvider` e notificações de mudança para provedores customizados ([estado de autenticação](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/authentication-state?view=aspnetcore-10.0)). O guia geral alerta que autorização executada no cliente não pode ser tratada como enforcement confiável ([segurança Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)). No exemplo OIDC com estado persistido, a documentação afirma que login/logout exige full page reload porque o estado WebAssembly persistido fica fixo pela vida da aplicação ([OIDC em Blazor Web Apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)). Essa regra é específica ao modelo documentado, não generalizada pelo checker.

## Executar localmente

```bash
npm ci --prefix tests/browser
(cd tests/browser && npx playwright install chromium)
node tests/browser/run-fixture.mjs --output /tmp/authdoctor-browser-trace.json
dotnet run --project src/AuthStateDoctor -- test --trace /tmp/authdoctor-browser-trace.json
```

O runner publica e remove os artefatos temporários automaticamente; encerra o processo filho e o navegador ao final; não imprime HTML, cookies, tokens, claims, IPs de usuário ou dados pessoais.
