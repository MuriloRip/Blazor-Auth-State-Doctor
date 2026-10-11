# Política de segurança e divulgação responsável

## Escopo

A CLI inspeciona metadados locais e reproduz traces JSON sanitizados. O harness Playwright exercita somente a fixture sintética deste repositório em loopback. A biblioteca `AuthStateDoctor.Blazor` oferece sondas explicitamente opt-in para diagnóstico local; elas não autenticam usuários, não verificam OIDC nem substituem autorização.

O `inspect` é heurístico e não comprova autorização efetiva nem configurações de runtime.

## Adapter opt-in

`<AuthStateProbe>` é desabilitado por padrão. Quando `Enabled=false`, não emite markup e não se inscreve em eventos do provider. Quando explicitamente habilitado, emite um único elemento oculto com `true`, `false` ou `unknown`; não consulta claims, tokens, cookies, nome, e-mail, URL, armazenamento do navegador ou conteúdo de outros elementos. O componente não persiste dados nem faz requests de rede.

`MapAuthStateDoctorServerProbe()` só pode ser mapeado em Development/Testing; fora disso falha no startup. A rota `GET /_authdoctor/server-state` é anônima apenas para retornar um booleano e restringe remote address e `Host` a loopback/local host. Também define `Cache-Control: no-store`. Não habilite CORS nem exponha o host de teste à rede.

Habilite as sondas apenas em ambientes locais/teste e desative-as em produção. O atributo DOM é legível por JavaScript já executado na mesma origem. O adapter é uma conveniência de teste, não uma nova boundary de segurança. Não contorne o guard de ambiente/loopback para usar o endpoint por proxy ou host compartilhado.

## Limites do harness

- O runner publica as fixtures Interactive Server e Static SSR em diretórios temporários separados, verifica que o guard de Production falha fechado e inicia cada processo `Testing` em `127.0.0.1` numa porta efêmera.
- Os endpoints sintéticos rejeitam conexões não loopback; operações POST exigem `Origin` same-origin, verificado por teste negativo.
- O endpoint de login não pede senha: cria apenas um principal sintético. Não copie endpoints de mutação da fixture para produção.
- O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. O teste verifica a invisibilidade em `document.cookie` sem ler/imprimir o valor.
- As sondas retornam somente um booleano de estado e status HTTP de uma rota protegida. O trace contém identificador sintético, transição, tempo relativo, dois booleanos e expectativa.
- O runner não grava nem imprime HTML, texto livre da página, cookies, tokens, claims, IPs, corpos de request ou logs do app. Arquivo do trace usa permissões owner-only onde suportado.
- Navegador, processo filho e publish temporário são fechados/removidos ao finalizar.

Loopback reduz exposição, mas não isola processos locais do mesmo usuário. Não rode a fixture em host compartilhado ou acessível externamente.

## Dados, parser e relatórios

Nunca inclua tokens, cookies, senhas, claims, nomes/e-mails reais, conteúdo de página ou URLs com credenciais em traces. O leitor limita o input a 1 MiB, 10.000 observações e profundidade JSON restrita; rejeita propriedades duplicadas, tempos não monotônicos por cenário e transições fora do vocabulário. Só campos tipados e validados são projetados para terminal, JSON e SARIF; campos extras são ignorados e não ecoados. Artifacts sintéticos da CI têm retenção de sete dias. Proteja relatórios locais segundo a política do seu CI.

## Autorização

`AuthorizeView`, rotas protegidas e sondas DOM não são uma fronteira de segurança para recursos. Aplicações reais devem autorizar cada operação no servidor segundo sua política. A fixture consulta uma rota protegida de teste separada.

## Reportar vulnerabilidades

Abra um reporte privado de segurança no GitHub para este repositório. Não publique credenciais, traces com dados pessoais ou detalhes exploráveis em issue pública.
