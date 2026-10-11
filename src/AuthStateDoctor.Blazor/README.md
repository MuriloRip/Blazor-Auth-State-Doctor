# AuthStateDoctor.Blazor (preview)

Razor Class Library .NET 10 para diagnóstico **local e opt-in** de estado de autenticação em Blazor. O pacote não autentica, não lê cookies/tokens/claims, não persiste dados e não envia telemetria.

## Probe de UI

`<AuthStateProbe>` observa apenas o booleano do `AuthenticationStateProvider`. O parâmetro `Enabled` é `false` por padrão; desabilitado, o componente não produz markup nem assina eventos. Habilite-o somente em Development/Testing:

```razor
@using AuthStateDoctor.Blazor
@inject IWebHostEnvironment Environment

@if (Environment.IsDevelopment())
{
    <AuthStateProbe Enabled="true" />
}
```

Quando habilitado, emite `data-authdoctor-probe="v1"` e `data-authenticated="true|false|unknown"`. Não habilite em produção: scripts da mesma origem conseguem ler esse sinal.

## Probe server-side local

Depois de configurar autenticação, o app de teste pode mapear:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapAuthStateDoctorServerProbe();
}
```

A extensão falha no startup se chamada fora desses ambientes. `GET /_authdoctor/server-state` retorna apenas `{ "authenticated": boolean }`, é `AllowAnonymous` para permitir false/true, define `Cache-Control: no-store` e limita `RemoteIpAddress` e `Host` a loopback/local host. Não adicione CORS ou exponha o host de diagnóstico a redes compartilhadas.

## Limites

As sondas observam sinais diferentes: o componente lê o estado disponibilizado à UI; o endpoint lê o principal da requisição server-side atual. Nenhuma prova que recursos reais aplicam as policies corretas. Autorização efetiva deve continuar no servidor. Veja a [política de segurança](https://github.com/MuriloRip/Blazor-Auth-State-Doctor/blob/main/SECURITY.md) e as [instruções de integração](https://github.com/MuriloRip/Blazor-Auth-State-Doctor/blob/main/docs/adapter-integration.md).

O pacote é um preview local do repositório e ainda não foi publicado no NuGet.org.
