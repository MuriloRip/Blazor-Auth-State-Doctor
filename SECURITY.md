# Política de segurança e divulgação responsável

## Escopo

A CLI inspeciona metadados locais e reproduz traces JSON sanitizados. O harness de navegador exercita **somente a fixture sintética deste repositório** em loopback; não autentica em aplicações arbitrárias, não usa credenciais reais, não faz varredura de endpoints e não substitui revisão de autorização.

O `inspect` é heurístico e não comprova autorização efetiva nem configurações de runtime.

## Limites do harness

- O runner publica a fixture em diretório temporário e inicia o processo em `127.0.0.1` numa porta efêmera.
- Os endpoints de teste rejeitam conexões não loopback; operações POST exigem `Origin` same-origin, verificado por teste negativo.
- O endpoint de login não pede senha: cria apenas um principal sintético. Não copie a fixture para produção.
- O cookie é HttpOnly, SameSite=Strict, não deslizante e expira em 15 minutos. O teste verifica a invisibilidade em `document.cookie` sem ler/imprimir o valor.
- As sondas retornam somente um booleano de estado e status HTTP de uma rota protegida. O trace contém identificador sintético, transição, tempo relativo, dois booleanos e expectativa.
- O runner não grava nem imprime HTML, texto livre da página, cookies, tokens, claims, IPs, corpos de request ou logs do app. Arquivo do trace usa permissões owner-only onde suportado.
- Navegador, processo filho e publish temporário são fechados/removidos ao finalizar.

Loopback reduz exposição, mas não isola processos locais do mesmo usuário. Não rode a fixture em host compartilhado ou acessível externamente.

## Dados, parser e relatórios

- Nunca inclua tokens, cookies, senhas, claims, nomes/e-mails reais, conteúdo de página ou URLs com credenciais em traces.
- O leitor limita o input a 1 MiB, 10.000 observações e profundidade JSON restrita; rejeita propriedades duplicadas, tempos não monotônicos por cenário e transições fora do vocabulário.
- Só campos tipados e validados são projetados para terminal, JSON e SARIF. Campos extras são ignorados e não ecoados.
- Saídas não contêm caminhos absolutos, texto original ou mensagem externa de exceção. Os artifacts sintéticos da CI têm retenção de sete dias.
- Relatórios locais especificados por `--output` ficam sob controle do operador; proteja-os segundo a política do seu CI.

## Autorização

`AuthorizeView`/rotas protegidas determinam o que a UI apresenta; não são uma fronteira de segurança para recursos. Aplicações reais devem autorizar cada operação no servidor segundo sua política. A fixture inclui uma sonda server-side separada que valida essa diferença.

## Reportar vulnerabilidades

Abra um reporte privado de segurança no GitHub para este repositório. Não publique credenciais, traces com dados pessoais ou detalhes exploráveis em issue pública.
