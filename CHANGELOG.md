# Changelog

## Unreleased — primeira versão funcional

- CLI .NET 10 com `inspect` para TFM, SDK e render modes detectados por busca local heurística.
- Contrato JSON `authdoctor-trace/v1` e exemplo sem dados reais.
- Replay determinístico de checkpoints tipados com classificação de mismatch e expectativa declarada de full reload.
- Saídas terminal, JSON e SARIF; código 1 para divergência não esperada.
- Limite de 1 MiB, limite de quantidade/profundidade, rejeição de propriedades duplicadas e enums/identificadores restritos.
- Projeção de campos permitidos: conteúdo extra do input não é copiado para o relatório.
- Smoke tests sem pacote externo e workflow básico de CI.
- Documentação de segurança e limitações da versão.

### Ainda não faz parte desta versão

- Automação real de navegador/Playwright, autenticação em aplicação ou coleta de estado server-side.
- Adapter de integração como pacote NuGet/test SDK.
- Matriz real de OIDC, cookies, render modes, duas abas e reconexão.
