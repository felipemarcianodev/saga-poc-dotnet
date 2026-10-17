#!/bin/bash

# Testa o fluxo de caixa (CQRS) de ponta a ponta e confere o resultado real.
#
# Uso: ./testar-fluxo-caixa.sh
#
# Caminho testado: API de lançamentos (202) -> fila fluxocaixa-lancamentos ->
# worker de lançamentos (grava e publica o evento) -> fila fluxocaixa-consolidado
# -> Consolidado (atualiza o saldo do dia) -> API de consulta.
#
# O 202 da API de lançamentos não prova nada. Depois de enviar, o script espera
# as duas filas esvaziarem e só então confere os lançamentos gravados e o
# consolidado do dia. Cada cenário usa um comerciante novo, então o cache da
# API de consulta nunca devolve um valor de rodada anterior.
#
# Variáveis de ambiente:
#   LANCAMENTOS_URL    API de lançamentos (padrão: http://localhost:5200)
#   CONSULTA_URL       API de consulta (padrão: http://localhost:5100)
#   RABBITMQ           container do RabbitMQ (padrão: saga-rabbitmq)
#   TEMPO_MAXIMO       segundos de espera pelas filas (padrão: 60)
#
# Código de saída: 0 se todos os cenários passaram; 1 caso contrário.

LANCAMENTOS_URL="${LANCAMENTOS_URL:-http://localhost:5200}"
CONSULTA_URL="${CONSULTA_URL:-http://localhost:5100}"
RABBITMQ="${RABBITMQ:-saga-rabbitmq}"
TEMPO_MAXIMO="${TEMPO_MAXIMO:-60}"

CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

FALHAS=0
SUFIXO="$(date +%s)"
HOJE="$(date -u +%Y-%m-%d)"
ONTEM="$(date -u -d yesterday +%Y-%m-%d)"

ok()    { echo -e "  ${GREEN}[OK]${NC} $1"; }
falha() { echo -e "  ${RED}[FALHA]${NC} $1"; FALHAS=$((FALHAS + 1)); }

# Envia um lançamento e devolve o código HTTP.
# Uso: lancar <tipo 1=débito 2=crédito> <valor> <data> <comerciante> [descrição]
lancar() {
    local arquivo="$TMP_DIR/payload.json"
    local descricao="${5:-Lançamento de teste}"
    printf '{"tipo":%s,"valor":%s,"dataLancamento":"%s","descricao":"%s","comerciante":"%s","categoria":"Teste"}' \
        "$1" "$2" "$3" "$descricao" "$4" > "$arquivo"
    # Arquivo + --data-binary preservam o UTF-8 no Git Bash do Windows.
    curl -s -o /dev/null -w '%{http_code}' \
        -H 'Content-Type: application/json' \
        --data-binary @"$arquivo" \
        "$LANCAMENTOS_URL/api/lancamentos"
}

# Mensagens prontas + em processamento numa fila.
mensagens_na_fila() {
    docker exec "$RABBITMQ" rabbitmqctl list_queues -q name messages_ready messages_unacknowledged 2>/dev/null \
        | awk -v fila="$1" '$1 == fila { print $2 + $3 }'
}

# Espera as filas de lançamento e de consolidado ficarem vazias em duas leituras seguidas.
aguardar_filas() {
    local inicio=$SECONDS vazias=0
    while (( SECONDS - inicio < TEMPO_MAXIMO )); do
        local l c
        l="$(mensagens_na_fila fluxocaixa-lancamentos)"
        c="$(mensagens_na_fila fluxocaixa-consolidado)"
        if [[ "${l:-0}" == "0" && "${c:-0}" == "0" ]]; then
            vazias=$((vazias + 1))
            (( vazias >= 2 )) && return 0
        else
            vazias=0
        fi
        sleep 1
    done
    return 1
}

# Lê um campo numérico de um JSON simples (sem jq).
campo() {
    sed -n "s/.*\"$1\":\(-\{0,1\}[0-9.]*\).*/\1/p" <<< "$2"
}

# Compara dois decimais (150.5 == 150.50).
iguais() {
    awk -v a="$1" -v b="$2" 'BEGIN { exit !((a + 0) == (b + 0)) }'
}

# Confere o consolidado de um comerciante numa data.
# Uso: conferir_consolidado <comerciante> <data> <créditos> <débitos> <saldo> <qtd créditos> <qtd débitos>
conferir_consolidado() {
    local resposta codigo corpo
    resposta="$(curl -s -w $'\n%{http_code}' "$CONSULTA_URL/api/consolidado/$1/$2")"
    codigo="${resposta##*$'\n'}"
    corpo="${resposta%$'\n'*}"

    if [[ "$codigo" != "200" ]]; then
        falha "consolidado de $2 devolveu HTTP $codigo: $corpo"
        return
    fi

    local nome esperado obtido divergiu=0
    for par in "totalCreditos:$3" "totalDebitos:$4" "saldoDiario:$5" "quantidadeCreditos:$6" "quantidadeDebitos:$7"; do
        nome="${par%%:*}"
        esperado="${par#*:}"
        obtido="$(campo "$nome" "$corpo")"
        if ! iguais "$obtido" "$esperado"; then
            falha "$nome de $2: esperado $esperado, obtido ${obtido:-vazio}"
            divergiu=1
        fi
    done
    (( divergiu == 0 )) && ok "consolidado de $2: créditos $3, débitos $4, saldo $5"
}

# Confere quantos lançamentos confirmados o comerciante tem no período.
conferir_lancamentos() {
    local corpo total
    corpo="$(curl -s "$LANCAMENTOS_URL/api/lancamentos?comerciante=$1&inicio=$2&fim=$3")"
    total="$(grep -o '"status":"Confirmado"' <<< "$corpo" | wc -l | tr -d ' ')"
    if [[ "$total" == "$4" ]]; then
        ok "$4 lançamentos gravados e confirmados"
    else
        falha "lançamentos confirmados: esperado $4, obtido $total"
    fi
}

esperar_codigo() {
    local obtido="$1" esperado="$2" descricao="$3"
    if [[ "$obtido" == "$esperado" ]]; then
        ok "$descricao: HTTP $obtido"
    else
        falha "$descricao: esperado HTTP $esperado, obtido $obtido"
    fi
}

echo -e "${CYAN}Fluxo de caixa: teste de ponta a ponta${NC}"
echo "Lançamentos: $LANCAMENTOS_URL | Consulta: $CONSULTA_URL"

ERROS_ANTES="$(mensagens_na_fila error)"
ERROS_ANTES="${ERROS_ANTES:-0}"

# ----------------------------------------------------------------------------
echo -e "\n${YELLOW}Cenário 1: crédito e débito no mesmo dia${NC}"
COM1="FC1-$SUFIXO"
esperar_codigo "$(lancar 2 150.50 "$HOJE" "$COM1" "Venda balcão")" 202 "crédito de 150,50"
esperar_codigo "$(lancar 1 40.25 "$HOJE" "$COM1" "Compra de insumos")" 202 "débito de 40,25"

# ----------------------------------------------------------------------------
echo -e "\n${YELLOW}Cenário 2: vários lançamentos acumulam no mesmo consolidado${NC}"
COM2="FC2-$SUFIXO"
for valor in 100 200 300 400 500; do
    esperar_codigo "$(lancar 2 "$valor" "$HOJE" "$COM2")" 202 "crédito de $valor"
done
for valor in 50 75 125; do
    esperar_codigo "$(lancar 1 "$valor" "$HOJE" "$COM2")" 202 "débito de $valor"
done

# ----------------------------------------------------------------------------
echo -e "\n${YELLOW}Cenário 3: lançamentos em dias diferentes ficam em consolidados separados${NC}"
COM3="FC3-$SUFIXO"
esperar_codigo "$(lancar 2 1000 "$ONTEM" "$COM3")" 202 "crédito de 1000 ontem"
esperar_codigo "$(lancar 1 250 "$HOJE" "$COM3")" 202 "débito de 250 hoje"

# ----------------------------------------------------------------------------
echo -e "\n${YELLOW}Cenário 4: validação da API recusa entrada inválida${NC}"
COM4="FC4-$SUFIXO"
esperar_codigo "$(lancar 2 0 "$HOJE" "$COM4")" 400 "valor zero"
esperar_codigo "$(lancar 2 10 "$HOJE" "$COM4" "ab")" 400 "descrição com 2 caracteres"
esperar_codigo "$(lancar 2 10 "$HOJE" "AB")" 400 "comerciante com 2 caracteres"

# ----------------------------------------------------------------------------
echo -e "\n${CYAN}Aguardando o processamento (até ${TEMPO_MAXIMO}s)...${NC}"
if aguardar_filas; then
    ok "filas fluxocaixa-lancamentos e fluxocaixa-consolidado vazias"
else
    falha "as filas não esvaziaram em ${TEMPO_MAXIMO}s"
fi

echo -e "\n${YELLOW}Conferência do cenário 1${NC}"
conferir_lancamentos "$COM1" "$HOJE" "$HOJE" 2
conferir_consolidado "$COM1" "$HOJE" 150.50 40.25 110.25 1 1

echo -e "\n${YELLOW}Conferência do cenário 2${NC}"
conferir_lancamentos "$COM2" "$HOJE" "$HOJE" 8
conferir_consolidado "$COM2" "$HOJE" 1500 250 1250 5 3

echo -e "\n${YELLOW}Conferência do cenário 3${NC}"
conferir_lancamentos "$COM3" "$ONTEM" "$HOJE" 2
conferir_consolidado "$COM3" "$ONTEM" 1000 0 1000 1 0
conferir_consolidado "$COM3" "$HOJE" 0 250 -250 0 1

echo -e "\n${YELLOW}Conferência do cenário 4${NC}"
conferir_lancamentos "$COM4" "$HOJE" "$HOJE" 0

echo -e "\n${YELLOW}Fila de erro${NC}"
ERROS_DEPOIS="$(mensagens_na_fila error)"
ERROS_DEPOIS="${ERROS_DEPOIS:-0}"
if (( ERROS_DEPOIS > ERROS_ANTES )); then
    falha "$((ERROS_DEPOIS - ERROS_ANTES)) mensagem(ns) nova(s) na fila error"
else
    ok "nenhuma mensagem nova na fila error"
fi

echo
if (( FALHAS == 0 )); then
    echo -e "${GREEN}Todos os cenários passaram.${NC}"
    exit 0
fi
echo -e "${RED}$FALHAS verificação(ões) falharam.${NC}"
exit 1
