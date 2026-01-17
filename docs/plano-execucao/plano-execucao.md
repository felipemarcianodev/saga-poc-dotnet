# Plano de Execução - POC SAGA Pattern com Rebus e RabbitMQ

## Documentação Estruturada

Este plano de execução foi organizado em múltiplos arquivos para facilitar a navegação e manutenção.

### Visão Geral e Arquitetura

- [Visão Geral do Projeto](./visao-geral.md) - Objetivos, escopo e estrutura do projeto
- [Arquitetura da Solução](./arquitetura.md) - Fluxo SAGA, componentes e padrões

### Fases de Execução - POC SAGA (Delivery de Comida)

- [Fase 1](./fase-01-fundacao-result-pattern.md) - Fundação - Result Pattern e Estrutura Base
- [Fase 2](./fase-02-configuracao-rebus-rabbitmq.md) - Configuração Rebus + RabbitMQ
- [Fase 3](./fase-03-implementacao-saga-rebus.md) - Implementação da SAGA com Rebus
- [Fase 4](./fase-04-implementacao-servicos-consumers.md) - Implementação dos Serviços (Consumers)
- [Fase 5](./fase-05-api-rest-ponto-entrada.md) - API REST (Ponto de Entrada)
- [Fase 6](./fase-06-casos-uso-cenarios-teste.md) - Casos de Uso e Cenários de Teste
- [Fase 7](./fase-07-documentacao-completa.md) - Documentação Completa
- [Fase 9](./fase-09-integracao-result-pattern.md) - Integração Completa do Result Pattern no Fluxo de Delivery
- [Fase 10](./fase-10-tratamentos-resiliencia.md) - Tratamentos de Resiliência
- [Fase 11](./fase-11-compensacao-rollback.md) - Compensação e Rollback Completo
- [Fase 12](./fase-12-observabilidade-opentelemetry.md) - Observabilidade com OpenTelemetry (Jaeger + Serilog + SEQ)
- [Fase 13](./fase-13-testes-cenarios-complexos.md) - Testes de Cenários Complexos
- [Fase 14](./fase-14-documentacao-refinamento.md) - Documentação e Refinamento Final
- [Fase 15](./fase-15-migracao-rabbitmq.md) - Migração para RabbitMQ (Open Source)

### Fases Avançadas de Produção

- [Fase 16](./fase-16-persistencia-estado-saga.md) - Persistência do Estado da SAGA
- [Fase 17](./fase-17-outbox-pattern.md) - Outbox Pattern (Garantias Transacionais)
- [Fase 18](./fase-18-observabilidade-opentelemetry-avancado.md) - Observabilidade com OpenTelemetry
- [Fase 19](./fase-19-retry-circuit-breaker.md) - Retry Policy e Circuit Breaker
- [Fase 20](./fase-20-idempotencia-deduplicacao.md) - Idempotência (Deduplicação de Mensagens)
- [Fase 21](./fase-21-mongodb-auditoria.md) - MongoDB para Auditoria e Histórico da SAGA
- [Fase 22](./fase-22-seguranca-autenticacao.md) - Segurança e Autenticação

### Recursos e Planejamento

- [Configuração do Ambiente](./configuracao-ambiente.md) - Setup de desenvolvimento e ferramentas
- [Estrutura Final de Pastas](./estrutura-pastas.md) - Organização do código
- [Definição de Pronto (DoD)](./definicao-pronto.md) - Critérios de qualidade
- [Cronograma Estimado](./cronograma.md) - Timeline do projeto
- [Próximos Passos](./proximos-passos.md) - Melhorias e evolução para produção
- [Referências](./referencias.md) - Documentação e recursos externos

---

## Navegação Rápida

### Por Categoria

**Fundação e Setup (Fases 1-2)**
- [Fase 1 - Result Pattern e Estrutura Base](./fase-01-fundacao-result-pattern.md)
- [Fase 2 - Configuração Rebus + RabbitMQ](./fase-02-configuracao-rebus-rabbitmq.md)

**Implementação Core (Fases 3-5)**
- [Fase 3 - Implementação da SAGA](./fase-03-implementacao-saga-rebus.md)
- [Fase 4 - Implementação dos Serviços](./fase-04-implementacao-servicos-consumers.md)
- [Fase 5 - API REST](./fase-05-api-rest-ponto-entrada.md)

**Testes e Documentação (Fases 6-7)**
- [Fase 6 - Casos de Uso e Cenários de Teste](./fase-06-casos-uso-cenarios-teste.md)
- [Fase 7 - Documentação Completa](./fase-07-documentacao-completa.md)

**Melhorias Avançadas (Fases 9-15)**
- [Fase 9 - Integração Result Pattern](./fase-09-integracao-result-pattern.md)
- [Fase 10 - Resiliência](./fase-10-tratamentos-resiliencia.md)
- [Fase 11 - Compensação e Rollback](./fase-11-compensacao-rollback.md)
- [Fase 12 - Observabilidade](./fase-12-observabilidade-opentelemetry.md)
- [Fase 13 - Testes Complexos](./fase-13-testes-cenarios-complexos.md)
- [Fase 14 - Refinamento Final](./fase-14-documentacao-refinamento.md)
- [Fase 15 - Migração RabbitMQ](./fase-15-migracao-rabbitmq.md)

**Fases Avançadas de Produção (Fases 16-22)**
- [Fase 16 - Persistência do Estado da SAGA](./fase-16-persistencia-estado-saga.md)
- [Fase 17 - Outbox Pattern](./fase-17-outbox-pattern.md)
- [Fase 18 - Observabilidade com OpenTelemetry](./fase-18-observabilidade-opentelemetry-avancado.md)
- [Fase 19 - Retry Policy e Circuit Breaker](./fase-19-retry-circuit-breaker.md)
- [Fase 20 - Idempotência](./fase-20-idempotencia-deduplicacao.md)
- [Fase 21 - MongoDB para Auditoria](./fase-21-mongodb-auditoria.md)
- [Fase 22 - Segurança e Autenticação](./fase-22-seguranca-autenticacao.md)

### Fases do Sistema de Fluxo de Caixa (Desafio Backend)

- [Fase 23](./fase-23-analise-design-fluxo-caixa.md) - Análise e Design - Sistema de Fluxo de Caixa
- [Fase 24](./fase-24-implementacao-servico-lancamentos.md) - Implementação do Serviço de Lançamentos (Write Model)
- [Fase 25](./fase-25-implementacao-servico-consolidado.md) - Implementação do Serviço de Consolidado Diário (Read Model)
- [Fase 26](./fase-26-testes-tdd-bdd.md) - Testes TDD/BDD e Validação de NFRs
- [Fase 27](./fase-27-diagramas-documentacao.md) - Diagramas e Documentação Completa

**Sistema de Fluxo de Caixa (Fases 23-27)**
- [Fase 23 - Análise e Design do Fluxo de Caixa](./fase-23-analise-design-fluxo-caixa.md)
- [Fase 24 - Implementação do Serviço de Lançamentos](./fase-24-implementacao-servico-lancamentos.md)
- [Fase 25 - Implementação do Serviço de Consolidado](./fase-25-implementacao-servico-consolidado.md)
- [Fase 26 - Testes TDD/BDD e Validação de NFRs](./fase-26-testes-tdd-bdd.md)
- [Fase 27 - Diagramas e Documentação Completa](./fase-27-diagramas-documentacao.md)

---

## Como Usar Esta Documentação

1. **Iniciando**: Comece pela [Visão Geral](./visao-geral.md) e [Arquitetura](./arquitetura.md)
2. **Configuração**: Siga o guia de [Configuração do Ambiente](./configuracao-ambiente.md)
3. **Desenvolvimento**: Execute as fases sequencialmente, começando pela [Fase 1](./fase-01-fundacao-result-pattern.md)
4. **Referência**: Consulte a [Estrutura de Pastas](./estrutura-pastas.md) conforme necessário
5. **Qualidade**: Verifique a [Definição de Pronto](./definicao-pronto.md) ao completar cada fase

---

*Este é um projeto de demonstração do padrão SAGA orquestrado usando Rebus e RabbitMQ em .NET 9.0*
