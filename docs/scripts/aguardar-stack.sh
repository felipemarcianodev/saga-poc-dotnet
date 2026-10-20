#!/bin/bash

# Espera a stack do compose ficar pronta para os testes de ponta a ponta.
#
# Uso: ./aguardar-stack.sh
#
# "Container no ar" não basta. A API de pedidos envia para fila-orquestrador
# assim que recebe o pedido, e o worker de lançamentos publica eventos que só
# chegam ao Consolidado depois que ele assina os tópicos. Mensagem enviada antes
# disso é descartada pelo RabbitMQ sem erro. Por isso o script espera:
#   1. /health respondendo 200 nas três APIs;
#   2. as sete filas declaradas no RabbitMQ;
#   3. as duas assinaturas do Consolidado no exchange RebusTopics.
#
# Variáveis de ambiente:
#   RABBITMQ        container do RabbitMQ (padrão: saga-rabbitmq)
#   TEMPO_MAXIMO    segundos de espera (padrão: 180)
#
# Código de saída: 0 se a stack ficou pronta; 1 se o tempo acabou.

RABBITMQ="${RABBITMQ:-saga-rabbitmq}"
TEMPO_MAXIMO="${TEMPO_MAXIMO:-180}"

APIS=(
    "http://localhost:5000/health"
    "http://localhost:5100/health"
    "http://localhost:5200/health"
)
FILAS=(
    fila-orquestrador fila-restaurante fila-pagamento fila-entregador
    fila-notificacao fluxocaixa-lancamentos fluxocaixa-consolidado
)
ASSINATURAS=(LancamentoCreditoRegistrado LancamentoDebitoRegistrado)

pendencias() {
    local faltando=()

    for url in "${APIS[@]}"; do
        [ "$(curl -s -o /dev/null -w '%{http_code}' "$url")" = "200" ] || faltando+=("$url")
    done

    local filas
    filas="$(docker exec "$RABBITMQ" rabbitmqctl list_queues -q name 2>/dev/null)"
    for fila in "${FILAS[@]}"; do
        grep -qx "$fila" <<< "$filas" || faltando+=("fila $fila")
    done

    local bindings
    bindings="$(docker exec "$RABBITMQ" rabbitmqctl list_bindings -q source_name destination_name routing_key 2>/dev/null \
        | awk -F'\t' '$1 == "RebusTopics" && $2 == "fluxocaixa-consolidado"')"
    for evento in "${ASSINATURAS[@]}"; do
        grep -q "$evento" <<< "$bindings" || faltando+=("assinatura $evento")
    done

    printf '%s\n' "${faltando[@]}"
}

inicio=$(date +%s)
while true; do
    faltando="$(pendencias)"
    if [ -z "$faltando" ]; then
        echo "Stack pronta em $(( $(date +%s) - inicio ))s."
        exit 0
    fi
    if [ $(( $(date +%s) - inicio )) -ge "$TEMPO_MAXIMO" ]; then
        echo "Stack não ficou pronta em ${TEMPO_MAXIMO}s. Pendente:"
        echo "$faltando" | sed 's/^/  - /'
        exit 1
    fi
    sleep 3
done
