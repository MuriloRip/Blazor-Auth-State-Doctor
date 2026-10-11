# Integração opt-in do AuthStateProbe

`AuthStateDoctor.Blazor` é uma pequena Razor Class Library .NET 10 que expõe somente o estado booleano de autenticação observado pelo `AuthenticationStateProvider` de um componente. **Não é um coletor de estado server-side, cliente OIDC, middleware ou auditoria de autorização.** O trace do CLI continua sendo a forma separada de comparar observações sanitizadas.

## Preparar o pacote local

O pacote preview ainda não é publicado no NuGet.org. Para testá-lo a partir do clone:

```bash
dotnet pack src/AuthStateDoctor.Blazor/AuthStateDoctor.Blazor.csproj \
  --configuration Release --output ./artifacts/nuget
dotnet add /caminho/para/App.csproj package AuthStateDoctor.Blazor \
  --version 0.2.0-preview.1 --source ./artifacts/nuget
```

Também é possível incluir o projeto `src/AuthStateDoctor.Blazor` diretamente como `ProjectReference` durante desenvolvimento.

## Habilitar explicitamente em desenvolvimento/teste

No layout do seu ambiente de teste:

```razor
@using AuthStateDoctor.Blazor
@inject IWebHostEnvironment Environment

@if (Environment.IsDevelopment())
{
    <AuthStateProbe Enabled="true" />
}
```

`Enabled` é `false` por padrão. Quando desabilitado, o componente não emite markup e não se inscreve no `AuthenticationStateProvider`. Em hosts sem `IWebHostEnvironment`, use uma flag de configuração que só seja definida no ambiente de diagnóstico; não use um valor `true` compilado para produção.

Com o componente habilitado, a página contém um único elemento oculto como:

```html
<output data-authdoctor-probe="v1" data-authenticated="true" aria-hidden="true" hidden=""></output>
```

O único valor coletado é `true`, `false` ou `unknown`; o componente não lê nomes, claims, tokens, cookies, URL, armazenamento do navegador ou conteúdo de outros elementos. O runner Playwright pode consultar apenas o atributo `data-authenticated` e deve gerar o contrato sanitizado de trace, sem serializar HTML.

## Limites e cuidado

A sonda descreve o estado entregue ao componente, não prova que a operação protegida esteja autorizada no servidor nem que todos os elementos visuais da aplicação estejam corretos. Valide endpoints protegidos com um mecanismo de teste server-side próprio, sem expor claims ou credenciais. Mantenha a sonda desabilitada em produção e remova-a se não for necessária; ela expõe um sinal de autenticação no DOM a scripts já executados na mesma origem.
