# Integração opt-in do AuthStateDoctor.Blazor

`AuthStateDoctor.Blazor` é uma Razor Class Library .NET 10 com um componente para observar estado de UI e uma extensão para um probe server-side local. **Não é um middleware de login, cliente OIDC, coletor de tokens ou auditoria de autorização.** O pacote preview ainda não foi publicado no NuGet.org.

## Preparar o pacote local

```bash
dotnet pack src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj \
  --configuration Release --output ./artifacts/nuget
dotnet add /caminho/para/App.csproj package AuthStateDoctor.Blazor \
  --version 0.2.0-preview.1 --source ./artifacts/nuget
```

Ou inclua `src/AuthStateDoctor.Blazor` como `ProjectReference` durante o desenvolvimento.

## Probe de UI

No layout do ambiente de teste:

```razor
@using AuthStateDoctor.Blazor
@inject IWebHostEnvironment Environment

@if (Environment.IsDevelopment())
{
    <AuthStateProbe Enabled="true" />
}
```

`Enabled` é `false` por padrão. Quando desabilitado, o componente não emite markup e não se inscreve nos eventos do `AuthenticationStateProvider`. Em hosts sem `IWebHostEnvironment`, use uma flag de configuração definida somente no ambiente de diagnóstico; não publique um valor `true` compilado em produção.

Quando habilitado, o DOM inclui um único elemento oculto deste formato:

```html
<output data-authdoctor-probe="v1" data-authenticated="true" aria-hidden="true" hidden=""></output>
```

O valor é apenas `true`, `false` ou `unknown`. O componente não lê claims, tokens, cookies, nomes, e-mails, URL, armazenamento do navegador ou conteúdo de outros elementos. Não serialize nem registre a página inteira para ler a sonda; consulte somente `data-authenticated`.

## Probe server-side local

No `Program.cs` da aplicação de teste, depois de configurar `UseAuthentication()`/`UseAuthorization()`:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapAuthStateDoctorServerProbe();
}
```

A extensão volta a verificar o ambiente e lança exceção se chamada fora de Development/Testing, para falhar fechado. A rota fixa `GET /_authdoctor/server-state` permite acesso anônimo apenas para retornar `{ "authenticated": true|false }`; exige endereço remoto loopback e `Host` local, define `Cache-Control: no-store` e retorna 404 fora desse limite. Ela não retorna roles, claims, nomes, policy names ou detalhes do principal. Não adicione CORS para essa rota e não exponha o servidor de teste numa interface/rede compartilhada.

A sonda é específica para execução local: reverse proxies, containers remotos ou hosts com endereços não loopback podem ser recusados intencionalmente. Não desative a checagem de ambiente/endereço para contornar isso; adapte o ambiente de teste com isolamento e revisão de segurança.

## Limites

A sonda de UI mostra o estado observado pelo componente; o endpoint mostra se o servidor autenticou a requisição recebida. Nenhum dos dois prova que um recurso real aplique as policies certas. Mantenha autorização nas rotas/handlers do app. Use os probes só em Development/Testing e remova-os quando o diagnóstico acabar; o booleano no DOM é legível por JavaScript da mesma origem.
