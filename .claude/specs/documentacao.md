# Spec: documentação do saga-poc-dotnet

Fonte única das regras de documentação deste repositório. Quem executa é a skill `atualizar-documentacao` (`.claude/skills/atualizar-documentacao/SKILL.md`); o `CLAUDE.md` só aponta para cá.

O repositório é público e é avaliado por quem lê. A documentação tem de bater com o código e com o que foi validado na stack, inclusive nas limitações.

---

## 1. O que vai em cada documento

| Arquivo | O que vai nele | O que não vai |
|---|---|---|
| `docs/README.md` | Porta de entrada (o GitHub o exibe na página do repositório): o que é, arquitetura em uma página, como rodar e validar, limitações conhecidas, índice | Detalhe de implementação, histórico de correções |
| `docs/arquitetura.md` | Estrutura macro: serviços, filas, comunicação, prazos da SAGA, deploy, observabilidade | Padrões internos, justificativa de tecnologia |
| `docs/design/padroes-utilizados.md` | Padrões internos (SAGA, CQRS, Result Pattern, Unit of Work) e como o código os aplica | Lista de serviços e portas |
| `docs/design/escolhas-tecnicas.md` | Por que cada tecnologia (Rebus, RabbitMQ, PostgreSQL, Redis, OpenTelemetry) | Decisão com alternativas pesadas: isso é ADR |
| `docs/decisoes-tecnicas/NNN-nome.md` | ADR: contexto, decisão, alternativas, consequências e "O que pode dar errado"; o README da pasta tem o template e o índice com o status | |
| `docs/casos-uso.md` | Os casos da SAGA (tabela com o que falha e o desfecho esperado) e as regras dos serviços simulados | |
| `docs/diagramas-compensacao.md`, `docs/diagramas-fluxo-caixa.md` | Diagramas mermaid e a matriz de compensação | Imagem gerada por IA |
| `docs/fluxo-caixa.md` | Contexto de fluxo de caixa: lançamentos, eventos, consolidado e cache | |
| `docs/runbook-troubleshooting.md`, `docs/troubleshooting-fluxo-caixa.md` | Sintoma, diagnóstico (comandos que funcionam sem `jq`) e ação | |
| `docs/estado-e-evolucao.md` | Levantamento com a stack no ar: defeitos (com evidência e PR), o que foi verificado, pendências por impacto e caminho de evolução. Atualizar quando uma pendência for resolvida ou um levantamento novo for feito | Plano de fase (fica no `CLAUDE.md`) |
| `docs/boas-praticas.md` | Aprendizados reais, com data quando é algo que já foi corrigido | Recomendação genérica sem lastro no projeto |
| `docs/scripts/readme-script.md` | O que cada script `.sh` valida, variáveis de ambiente e a tabela "Quando um script falha" | |
| `docker/README.md` | Operação da stack, portas, volumes e consultas SQL | |
| `CLAUDE.md` | Regras de trabalho, armadilhas conhecidas, plano e backlog | Regras de documentação (ficam nesta spec) |

Fora do git (`.gitignore`): `docs/gerador-imagens*.md` (prompts das imagens geradas por IA, removidas na fase 6 por não baterem com o sistema) e `docs/rascunhos/` (planos locais).

## 2. Regras de escrita

1. **Nada de métrica sem evidência.** "99,9%", "50 req/s" ou "P95 < 10 ms" só com teste de carga ou monitoramento que comprove; do contrário, escrever como objetivo ("meta: ...").
2. **Declarar limitações e trade-offs reais.** Todo ADR tem "O que pode dar errado". Limitação descoberta entra em "Limitações conhecidas" do `docs/README.md` e no backlog do `CLAUDE.md`.
3. **Descrever o que o código faz hoje**, não o que deveria fazer. Comportamento que não foi validado na stack é escrito como não verificado.
4. **Tom direto, sem texto decorativo.** Não repetir a mesma estrutura de pastas, diagrama ou tabela em vários documentos: um documento é a fonte e os outros apontam para ele.
5. **Arquitetura (macro) ≠ design (padrões internos) ≠ implementação (tecnologia escolhida).**
6. **Diagrama em mermaid ou tabela**, nunca imagem gerada por IA.
7. **Números que aparecem em vários lugares ficam consistentes:** quantidade de casos de uso, de testes, de cenários do fluxo de caixa, prazos e tentativas da SAGA, portas.
8. **Português do Brasil revisado** pela skill `revisar-texto-ptbr`; identificadores, comandos e caminhos entre crases.

## 3. O que atualizar a cada tipo de mudança

| Mudança no código ou na stack | Documentos a conferir |
|---|---|
| Fluxo da SAGA, compensação, prazos, tentativas | `arquitetura.md` (seção do orquestrador), `diagramas-compensacao.md` (fluxos, matriz, "O que pode dar errado"), `design/padroes-utilizados.md`, `runbook-troubleshooting.md`, armadilhas do `CLAUDE.md` |
| Caso de uso novo ou desfecho novo | `casos-uso.md` (tabela, classificação, regras dos serviços), `scripts/readme-script.md`, o próprio script, contagem de casos no `README.md`, no `runbook` e no `CLAUDE.md` |
| Regra nova num serviço simulado (`REST_*`, `CLI_*`, endereço) | `casos-uso.md` (regras dos serviços simulados), armadilha "Simulações nos serviços" do `CLAUDE.md` |
| Fluxo de caixa (lançamento, evento, consolidado, cache) | `fluxo-caixa.md`, `diagramas-fluxo-caixa.md`, ADR 002 (cache), `troubleshooting-fluxo-caixa.md` |
| Endpoint, porta, Swagger, variável de ambiente | tabela de serviços do `README.md` e do `CLAUDE.md`, `docker/README.md`, exemplos de `curl` |
| Logs, traces, Seq, Jaeger | `arquitetura.md` (observabilidade), item "Observabilidade" das limitações do `README.md` |
| Pacote, imagem ou versão de plataforma | `design/escolhas-tecnicas.md`, `docker/README.md` |
| Decisão com alternativas reais e custo | ADR novo em `decisoes-tecnicas/` e o índice do README da pasta |
| Defeito corrigido | sai das limitações do `README.md` e do backlog; vira aprendizado em `boas-praticas.md` quando ensina algo |
| Documento removido ou renomeado | `git grep nome-do-arquivo` e corrigir os links |

## 4. Conferência antes de dar por concluído

- [ ] Cada afirmação nova bate com o código (ler o trecho) ou com a execução na stack (comando rodado nesta sessão).
- [ ] Comandos e exemplos de `curl` usam as portas e rotas reais; payload com acento vai por arquivo (`--data-binary @arquivo`).
- [ ] Links relativos existem com a grafia exata (`git ls-files`): o GitHub diferencia maiúsculas de minúsculas, o Windows não.
- [ ] Contagens e valores repetidos (casos, testes, prazos, portas) iguais em todos os documentos (`git grep`).
- [ ] Nenhuma métrica sem evidência; limitações novas registradas no `README.md` e no backlog.
- [ ] Revisão pt-BR feita com `revisar-texto-ptbr`.
