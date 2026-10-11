# Segurança e divulgação responsável

## Escopo desta versão

O MVP é uma CLI local de **replay e classificação de traces**. Não autentica usuários, não acessa endpoints da aplicação, não captura cookies/tokens, não executa Playwright e não substitui uma avaliação de segurança ou teste end-to-end. `inspect` faz descoberta local por heurística e não valida autorização efetiva.

## Dados e limites

- Não forneça tokens, cookies, senhas, claims, nomes ou e-mails reais no trace.
- O leitor aceita no máximo 1 MiB, 10.000 observações e profundidade JSON limitada.
- Somente campos tipados e validados são projetados no relatório; campos adicionais no JSON são ignorados e não são copiados.
- Identificadores são limitados a caracteres ASCII seguros e transições usam vocabulário fechado.
- Saídas não incluem caminho absoluto, texto original, headers, mensagens de exceção externas ou conteúdo livre do trace.
- Relatórios JSON/SARIF escritos com `--output` ficam no caminho indicado pelo operador; proteja esse arquivo segundo as políticas do seu CI.

## Uso seguro

1. Use contas e ambientes de teste não produtivos.
2. Armazene credenciais apenas no secret manager do CI (o MVP atual não consome credenciais).
3. Revise traces antes de compartilhá-los e use valores sintéticos.
4. Execute a ferramenta com permissões mínimas e mantenha SDK/runtime atualizados.
5. Trate diagnósticos como evidência para investigação, não como prova de vulnerabilidade ou de conformidade total.

## Reportar vulnerabilidades

Abra um reporte privado de segurança no GitHub para este repositório. Não publique credenciais, traces com dados pessoais ou detalhes exploráveis em issue pública.
