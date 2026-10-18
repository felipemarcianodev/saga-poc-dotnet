# saga-poc-dotnet — CLAUDE.md

POC pública de portfólio: SAGA orquestrado para delivery de comida (Rebus + RabbitMQ + PostgreSQL) e um segundo contexto de Fluxo de Caixa com CQRS (lançamentos → eventos → consolidado com cache Redis). O objetivo é mostrar decisões técnicas reais, inclusive as limitações, e não uma vitrine polida.

Responda em português do Brasil, com acentuação correta.

## Estilo de resposta a perguntas

Ao responder **perguntas** ("é possível", "vale a pena", "o que é"), seja curto e direto: resposta objetiva (sim/não + 1 linha) e, em seguida, bullets com o essencial. Só aprofunde se for pedido. Não se aplica à execução de tarefas, onde vale o rigor normal.

---

## Início de sessão

1. `git fetch` e `git status`: partir da `main` atualizada (`git switch main && git pull`), sem alteração pendente de outra sessão.
2. Ler o **Plano de retomada** (fim deste arquivo) e identificar a próxima fase pendente.
3. Uma fase só começa quando o dono pedir ("pode seguir com a fase N"). Ao terminar, entregar o relatório da fase e parar; a seguinte espera um novo pedido.

**Relatório de fim de fase** (curto, em pt-BR): o que mudou e por quê, defeitos encontrados no caminho, resultado de cada portão (build, testes, scripts) e limitações ou pendências que entraram no backlog. Se algo relatado antes estava errado, dizer isso no início, sem rodeio.

## Regras da máquina local

- **Processos:** nunca matar processo por nome (`taskkill /IM`, `pkill`, `Stop-Process -Name`); só o PID de um processo iniciado na própria sessão. Ao fim do trabalho, liberar os servidores de build com `dotnet build-server shutdown`.
- **Scripts `.ps1`:** não rodar sem perguntar antes; os scripts de validação oficiais são os `.sh` de `docs/scripts/`.
- **Stack Docker:** pode ficar no ar entre sessões. Antes dos portões, recriar o que a fase mudou (`docker compose -f docker/docker-compose.yml up -d --build`). Só purgar a fila `error` do RabbitMQ depois de conferir que as mensagens são de teste da própria sessão.
- **Git:** nunca `--force` na `main`; `--force-with-lease` só em branch de fase.

## Governança de texto em pt-BR (bloqueante)

Todo texto que a IA escreve neste repositório (documentação, README, ADR, comentário de código, mensagem de commit, descrição de PR e resposta no chat) passa por revisão de ortografia, gramática, pontuação e acentuação do português do Brasil antes de a tarefa ser dada por concluída.

- **Método:** skill `revisar-texto-ptbr` (`.claude/skills/revisar-texto-ptbr/SKILL.md`).
- **Automático:** `.claude/hooks/revisao-ptbr.mjs`, ligado em `.claude/settings.json`. Aponta palavra sem acento depois de cada edição de `.md`/`.txt`, recusa `git commit` com mensagem sem acento e, no fim do turno, pede a revisão completa dos textos tocados.
- **Falso positivo:** acrescentar a palavra, em minúsculas, em `.claude/hooks/revisao-ptbr.permitidas.txt`; se for identificador, prefira colocá-lo entre crases no texto.
- **Fonte:** o hook e a skill vêm do repositório `meu-portfolio`, onde a governança nasceu. Mudanças são feitas lá e replicadas aqui, não editadas só neste repositório.

---

## Fluxo de trabalho por fase — bloqueante

O trabalho segue o **Plano de retomada** (fim deste arquivo), uma fase por vez:

1. Branch da fase: `fase/N-slug`, criada a partir da branch da fase anterior enquanto ela não estiver na `main`.
2. Implementar a fase.
3. **Portões**, todos verdes antes do commit:
   - `dotnet build SagaPoc.sln` sem erros
   - `dotnet test SagaPoc.sln` com 100% aprovado, **sem `--no-build`** (até 2026-10-17 o projeto de testes estava fora da solution e não compilava; `--no-build` rodava DLLs antigas e escondia isso)
   - se a fase mexe em serviço, mensageria ou compose: stack no Docker + `docs/scripts/testar-casos-de-uso.sh` e `docs/scripts/testar-fluxo-caixa.sh` com saída 0
4. Commit em pt-BR com acentuação, no formato `tipo(escopo): descrição` (`feat`, `fix`, `refactor`, `test`, `docs`, `chore`). **Nunca** incluir assinatura ou atribuição de IA (`Co-Authored-By` etc.).
5. Push da branch, PR para a `main` e merge quando os portões passarem. A `main` do GitHub é **protegida** e só aceita PR; nunca tentar push direto na `main`.
6. Atualizar o status da fase neste arquivo no mesmo PR (com o número do PR) e registrar no backlog o que ficou de fora.

Trabalho fora das fases (governança, correção avulsa) segue o mesmo fluxo, numa branch `chore/slug` ou `fix/slug`.

Falha nos portões → corrigir até ficar verde; não abrir PR vermelho.

**Merge:** sempre squash, com a branch apagada em seguida. A exclusão automática está desligada no repositório, então passar a flag:

```bash
gh pr create --base main --head fase/N-slug --title "tipo(escopo): descrição" --body-file corpo.md
gh pr merge <número> --squash --delete-branch --subject "tipo(escopo): descrição (#<número>)"
```

O squash cria na `main` um commit novo, então a branch da fase seguinte (criada em cima da anterior) precisa ser rebaseada antes do PR dela: `git rebase --onto origin/main <último-commit-da-fase-anterior> fase/N-slug`, conferir com `git diff` que o conteúdo não mudou e publicar com `git push --force-with-lease` (só na branch da fase, nunca na `main`). Um PR fechado não pode ser reaberto depois de force-push; nesse caso, abrir um novo.

Use o `gh` (autenticado) para PR e merge; o token do MCP do GitHub só tem permissão de leitura neste repositório.

---

## Como rodar e validar

```bash
# Build e testes
dotnet build SagaPoc.sln
dotnet test SagaPoc.sln

# Stack completa (a partir da raiz)
docker compose -f docker/docker-compose.yml up -d --build

# Casos de uso da SAGA com conferência do desfecho real (sai 1 se algum divergir)
bash docs/scripts/testar-casos-de-uso.sh        # todos
bash docs/scripts/testar-casos-de-uso.sh 4      # um caso

# Fluxo de caixa de ponta a ponta: lançamentos -> consolidado (sai 1 se divergir)
bash docs/scripts/testar-fluxo-caixa.sh
```

| Serviço | Endereço |
|---|---|
| API de pedidos (SAGA) | http://localhost:5000 (Swagger em `/swagger`) |
| API de lançamentos (escrita) | http://localhost:5200 |
| API do fluxo de caixa (consulta) | http://localhost:5100 |
| RabbitMQ (management) | http://localhost:15672 (saga / saga123) |
| Seq (logs) | http://localhost:5342 |
| Jaeger (traces) | http://localhost:16686 |
| PostgreSQL SAGA / lançamentos / consolidado | 5432 / 5433 / 5434 |
| Redis | 6379 |

**Validar a SAGA pelo desfecho, nunca pelo 202.** A API responde 202 assim que aceita o pedido; quem prova que a SAGA terminou é o log `[SAGA]` do orquestrador (`docker logs saga-orquestrador`), que o script já lê. Mensagem na fila `error` do RabbitMQ é sempre bug.

---

## Mapa do código

```
src/
├── BuildingBlocks/         Common (Result Pattern, mensagens), Infrastructure(.Core),
│                           Observability (Serilog, OpenTelemetry), WebHost (extensões)
├── SagaPoc.Api/            entrada dos pedidos (POST /api/pedidos → IniciarPedido)
├── SagaPoc.Orquestrador/   PedidoSaga (Rebus); estado no PostgreSQL
├── SagaPoc.Servico{Restaurante,Pagamento,Entregador,Notificacao}/   serviços simulados
├── SagaPoc.ServicoLancamentos/   Lancamentos.Api + Lancamentos.Worker (escrita)
└── SagaPoc.ServicoFluxoCaixa/    Domain, Application, Infrastructure, Api (consulta),
                                  Consolidado (worker que aplica os eventos)
tests/SagaPoc.FluxoCaixa.Domain.Tests/   testes de domínio (xUnit)
docker/docker-compose.yml               stack completa
docs/                                   documentação (ver abaixo)
```

Filas: `fila-orquestrador`, `fila-restaurante`, `fila-pagamento`, `fila-entregador`, `fila-notificacao`, `fluxocaixa-lancamentos`, `fluxocaixa-consolidado`.

---

## Decisões e armadilhas conhecidas

- **Estado da SAGA é do Rebus.** O Rebus cria e mantém `pedido_saga_data` e `pedido_saga_index` (`data` é JSON em `bytea`). Nenhum outro componente (EF Core, migration, SQL manual) cria ou altera essas tabelas. Foi exatamente isso que quebrou a SAGA até 2026-10-17: o EF criava uma tabela com o mesmo nome e outro esquema.
- **SAGA concluída some do banco.** `MarkAsComplete()` remove a linha; o desfecho final não fica persistido em lugar nenhum. Por isso `GET /api/pedidos/{id}/status` ainda é **mock** (sempre "EmProcessamento") — ver plano.
- **Simulações nos serviços** (documentar, não "corrigir"): o entregador recusa 30% das alocações entre 12h e 14h UTC (horário de pico); o pool tem 10 entregadores, e cada um volta ao pool quando a entrega simulada termina (1 minuto estimado = 1 segundo). O script marca a recusa de pico como AVISO, não como falha.
- **Fluxo de caixa: eventos por Publish/Subscribe.** O worker de lançamentos publica `LancamentoCreditoRegistrado`/`LancamentoDebitoRegistrado` e o Consolidado assina os dois ao subir (`onCreated`). Sem a assinatura, o RabbitMQ descarta os eventos em silêncio; mapear o evento no roteamento não adianta, porque o mapa só vale para `Send`.
- **Transação no fluxo de caixa:** o DbContext usa `EnableRetryOnFailure`, que recusa transação aberta à mão. Usar `IUnitOfWork.ExecutarEmTransacaoAsync`, que roda o bloco na estratégia de execução do EF e o repete inteiro em falha transitória. Dentro do bloco, ler o estado de novo; não chamar `Update` em entidade recém-adicionada (vira UPDATE de linha inexistente).
- **Migração só no dono da escrita:** o worker de lançamentos migra `fluxocaixa_lancamentos` e o Consolidado migra `fluxocaixa_consolidado`. As APIs não migram, para não disputarem a mesma migration ao subir.
- **Consolidado processa uma mensagem por vez** (`MaxParallelism = 1`): a linha do dia não tem controle de concorrência, e dois eventos em paralelo perderiam uma atualização. Escalar exige concorrência otimista ou UPDATE atômico.
- **Leitura do consolidado pode vir defasada até cerca de 60 s:** o Consolidado apaga a chave no Redis, mas a API de consulta também tem cache em memória (1 min) e response cache (60 s), que não são invalidados. Por isso o script usa um comerciante novo por cenário.
- **Worker com `Microsoft.NET.Sdk` precisa copiar o `appsettings.json`** para a saída (`CopyToOutputDirectory`): o `UseCustomSerilog` do host lê o arquivo do diretório de trabalho e derruba o processo se ele faltar.
- **Dockerfiles copiam `src/` inteiro** (`bin/obj` ficam fora pelo `.dockerignore`). Não voltar para a lista manual de projetos: ela ficava defasada a cada projeto criado ou removido e quebrou o build da imagem.
- **Payload com acento no Git Bash do Windows:** passar JSON direto no argumento do `curl` corrompe o UTF-8. Enviar como arquivo (`--data-binary @arquivo`).
- **Links em markdown:** o GitHub diferencia maiúsculas de minúsculas, o Windows não. Conferir o nome exato do arquivo (`git ls-files`) ao criar link.
- **Observabilidade:** não deixar `catch` silencioso; todo catch loga com placeholder estruturado (nunca `$"..."` em log) e com o identificador da operação (PedidoId, EntregadorId, LancamentoId).

---

## Documentação

| Arquivo | O que vai nele |
|---|---|
| `docs/README.md` | Porta de entrada (o GitHub o exibe na página do repositório): o que é, como rodar, onde ler mais |
| `docs/arquitetura.md` | Estrutura macro: serviços, comunicação, deploy, observabilidade |
| `docs/design/` | Padrões internos (SAGA, CQRS, Result Pattern, Unit of Work) e escolhas de tecnologia |
| `docs/decisoes-tecnicas/` | ADRs numerados (`NNN-nome.md`); o README da pasta tem o template |
| `docs/casos-uso.md`, `docs/diagramas-*.md` | Cenários da SAGA e do fluxo de caixa, com diagramas |
| `docs/fluxo-caixa.md` | Contexto de Fluxo de Caixa |
| `docs/runbook-troubleshooting.md`, `docs/troubleshooting-fluxo-caixa.md` | Diagnóstico operacional |
| `docs/boas-praticas.md` | Aprendizados reais do projeto |
| `docs/scripts/` | Scripts de teste (`readme-script.md` explica cada um) |
| `docker/README.md` | Operação da stack e consultas SQL |

Fora do git (`.gitignore`): `docs/gerador-imagens*.md` (prompts que geraram as imagens de `docs/images/`) e `docs/rascunhos/` (planos locais).

**Regras de escrita** (o repositório é público e é avaliado por quem lê):
- Nada de métrica sem evidência ("99,9%", "50 req/s", "P95 < 10 ms") — só com teste de carga ou monitoramento que comprove; do contrário, escrever como objetivo.
- Declarar limitações e trade-offs reais; seção "O que pode dar errado" nos ADRs.
- Tom direto, sem texto decorativo; não repetir a mesma estrutura de pastas ou diagrama em vários documentos.
- Arquitetura (macro) ≠ design (padrões internos) ≠ implementação (tecnologia escolhida).
- Ao remover um documento, procurar e corrigir os links para ele (`git grep nome-do-arquivo`).

---

## Plano de retomada (iniciado em 2026-10-17)

| Fase | Branch | Status |
|---|---|---|
| 1. SAGA funcionando: build corrigido, Rebus como dono do estado da SAGA (EF removido do orquestrador) | `fase/1-saga-rebus` | ✅ Na `main` (#1) |
| 2. Script de casos de uso que confere o desfecho real; correção do pool do entregador | `fase/2-script-testes` | ✅ Na `main` (#3) |
| 2b. Este CLAUDE.md; limpeza da documentação (índice e guia de Swagger removidos, ADRs versionados, links corrigidos) | `fase/2b-claude-md` | ✅ Na `main` (#4) |
| 3. Lado de escrita do Fluxo de Caixa no compose | `fase/3-fluxo-caixa-compose` | ✅ Na `main` (#5) |
| 4. Migração para .NET 10 | `fase/4-dotnet-10` | ⏳ Pendente |
| 5. CI no GitHub Actions | `fase/5-ci` | ⏳ Pendente |
| 6. README, "Sobre" e tópicos do GitHub | `fase/6-readme-sobre` | ⏳ Pendente |

### Fase 3 — Fluxo de Caixa (escrita) no compose (concluída)
- `lancamentos-api` (porta 5200), `lancamentos-worker` e `fluxocaixa-consolidado` no compose, com Dockerfiles que copiam `src/` inteiro; connection strings e RabbitMQ alinhados nos `appsettings`.
- Corrigido o que impedia o fluxo de funcionar: o Consolidado não assinava os eventos; a transação manual conflitava com a repetição automática do EF; o handler do Consolidado dava `Update` em entidade recém-criada; o worker caía sem o `appsettings.json` na saída.
- O Consolidado passou a usar o Redis de verdade (mesmo prefixo da API), então a chave do dia é apagada a cada evento.
- Projeto de testes corrigido (referência quebrada) e incluído na solution.
- Validação: `docs/scripts/testar-fluxo-caixa.sh` (4 cenários com conferência do saldo e da fila `error`).

### Fase 4 — .NET 10
- `TargetFramework` `net9.0` → `net10.0` em todos os projetos (considerar `Directory.Build.props` centralizando TFM, `Nullable` e `ImplicitUsings`).
- Pacotes `Microsoft.*`/EF Core/Npgsql para a linha 10; revisar Rebus (`Rebus.ServiceProvider` 10.1.0 hoje avisa NU1608 por exigir `Microsoft.Extensions.*` < 9).
- Trocar `OpenTelemetry.Exporter.Jaeger` (descontinuado, com vulnerabilidade GHSA-38h3-2333-qx47) por OTLP; o Jaeger all-in-one recebe OTLP em 4317/4318. Ajustar variáveis `Jaeger__*` no compose.
- Imagens Docker `mcr.microsoft.com/dotnet/sdk:10.0` e `aspnet:10.0`.
- Portões completos, inclusive o script de casos de uso no Docker.

### Fase 5 — CI
- Workflow em `.github/workflows/` com build e `dotnet test` em push/PR.
- Opcional: job que sobe o compose e roda `testar-casos-de-uso.sh` (sai 1 se divergir). Para ficar determinístico, tornar o horário de pico do entregador configurável e desligá-lo no CI.

### Fase 6 — README e "Sobre"
- `docs/README.md` (≈700 linhas) reduzido ao essencial; remover menções a "18 casos" e ao projeto `FluxoCaixa.Lancamentos`, que não existe mais.
- Revisar `docs/arquitetura.md`, `docs/boas-praticas.md` e `docs/casos-uso.md` (citam MassTransit), ADR 002 (metas de latência e vazão sem medição) e os runbooks (usam `jq`).
- Seção "Limitações conhecidas" no README.
- Conferir se as imagens de `docs/images/` ainda batem com o sistema.
- "Sobre" do GitHub: hoje diz ".NET 9"; atualizar a descrição e incluir os tópicos `dotnet`, `cqrs`, `postgresql`, `redis` e `opentelemetry`.

### Backlog (fora das fases, por prioridade)
1. **Status real do pedido:** o orquestrador publica as transições de estado e a API mantém uma projeção (read model) consultada por `GET /api/pedidos/{id}/status`; hoje o endpoint é mock.
2. **Testes automatizados da API de pedidos e da SAGA:** testes de integração (xUnit + Testcontainers com RabbitMQ e PostgreSQL) cobrindo os 12 casos, cenários de erro (400, 404) e os endpoints `/status` e `/health`.
3. Avisos de build restantes (86 na última contagem), começando pelos de pacote.
4. **Idempotência do worker de lançamentos:** cada tentativa de `RegistrarLancamento` gera um `Id` novo; se o envio do evento falhar depois do commit, a repetição do Rebus grava o lançamento de novo. Usar o `CorrelationId` do comando como chave de deduplicação.
5. **`UseCustomSerilog` do host** (`SagaPoc.Observability`) monta a configuração com Seq e a descarta, lendo só o `appsettings.json`; os workers não mandam log para o Seq.
6. Scripts `.ps1` antigos de `docs/scripts/` (fluxo de caixa, sistema completo, monitor) apontam todas as APIs para a mesma URL e não conferem resultado; revisar ou remover na fase 6.
