---
name: atualizar-documentacao
description: Atualiza e confere a documentação do saga-poc-dotnet (docs/, README, ADRs, runbooks, CLAUDE.md) seguindo a spec .claude/specs/documentacao.md. Use depois de qualquer mudança de comportamento no código, na stack ou nos scripts, antes de abrir PR, e quando o usuário pedir para documentar, atualizar o README, escrever ou revisar um ADR, ou conferir se a documentação bate com o código.
---

# Atualizar a documentação

As regras ficam em `.claude/specs/documentacao.md`. Esta skill é o procedimento para aplicá-las.

## Passo a passo

1. **Ler a spec inteira** (`.claude/specs/documentacao.md`) antes de editar qualquer documento.
2. **Delimitar a mudança:** `git diff origin/main...HEAD --stat` e o diff dos arquivos de código ou de stack. Anotar o que mudou de comportamento: fluxo, desfecho, regra simulada, endpoint, porta, configuração, limitação nova ou resolvida.
3. **Achar os documentos afetados** na tabela da seção 3 da spec. Procurar também as menções soltas: `git grep -n "<termo>" -- '*.md'` para cada nome de mensagem, estado, caso, porta ou número alterado.
4. **Editar** só o que mudou, no documento que é a fonte do assunto (seção 1 da spec). Nos outros, apontar para a fonte em vez de repetir.
5. **Conferir** com a lista da seção 4 da spec. O que não puder ser confirmado no código ou na stack nesta sessão é escrito como não verificado, nunca como fato.
6. **Revisar o português** com a skill `revisar-texto-ptbr`.
7. **Relatar** em uma linha por documento o que mudou e, se houver, o que ficou de fora e por quê.

## Quando a spec não cobre o caso

Se a mudança não se encaixa em nenhuma linha da seção 3, decidir pelo documento da seção 1 que trata do assunto e, no fim, propor ao usuário a linha nova para a tabela da spec, em vez de criar regra só na conversa.
