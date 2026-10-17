#!/bin/bash

# Testa os 12 casos de uso da POC SAGA e confere o desfecho real de cada pedido.
#
# Uso: ./testar-casos-de-uso.sh [numero-do-caso]
#   ./testar-casos-de-uso.sh      testa todos os casos
#   ./testar-casos-de-uso.sh 4    testa apenas o caso 4
#
# A API responde 202 assim que aceita o pedido; isso não prova que a SAGA
# terminou. Por isso, depois de enviar os pedidos, o script lê os logs do
# orquestrador (docker logs) e classifica cada pedido em CONCLUIDO, CANCELADO
# ou COMPENSADO, comparando com o desfecho esperado do caso.
#
# Variáveis de ambiente:
#   BASE_URL           URL da API (padrão: http://localhost:5000)
#   ORQUESTRADOR       container do orquestrador (padrão: saga-orquestrador)
#   TEMPO_MAXIMO       segundos de espera pelos desfechos (padrão: 60)
#
# Código de saída: 0 se todos os casos tiveram o desfecho esperado; 1 caso contrário.

BASE_URL="${BASE_URL:-http://localhost:5000}"
API_ENDPOINT="$BASE_URL/api/pedidos"
ORQUESTRADOR="${ORQUESTRADOR:-saga-orquestrador}"
TEMPO_MAXIMO="${TEMPO_MAXIMO:-60}"

# Cores
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
GRAY='\033[0;37m'
NC='\033[0m'

# ========================================
# Payloads dos Casos de Uso
# ========================================

CASO_1='{
  "clienteId": "CLI001",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD001",
      "nome": "Pizza Margherita",
      "quantidade": 1,
      "precoUnitario": 45.90
    }
  ],
  "enderecoEntrega": "Rua das Flores, 123 - Centro",
  "formaPagamento": "CREDITO"
}'

CASO_2='{
  "clienteId": "CLI001",
  "restauranteId": "REST_FECHADO",
  "itens": [
    {
      "produtoId": "PROD001",
      "nome": "Hambúrguer Artesanal",
      "quantidade": 2,
      "precoUnitario": 28.50
    }
  ],
  "enderecoEntrega": "Av. Principal, 456",
  "formaPagamento": "DEBITO"
}'

CASO_3='{
  "clienteId": "CLI002",
  "restauranteId": "REST002",
  "itens": [
    {
      "produtoId": "INDISPONIVEL",
      "nome": "Produto Esgotado",
      "quantidade": 1,
      "precoUnitario": 35.00
    }
  ],
  "enderecoEntrega": "Rua das Palmeiras, 789",
  "formaPagamento": "PIX"
}'

CASO_4='{
  "clienteId": "CLI_CARTAO_RECUSADO",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD003",
      "nome": "Sushi Combo",
      "quantidade": 1,
      "precoUnitario": 89.90
    }
  ],
  "enderecoEntrega": "Rua das Acácias, 321",
  "formaPagamento": "CREDITO"
}'

CASO_5='{
  "clienteId": "CLI003",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD004",
      "nome": "Salada Caesar",
      "quantidade": 3,
      "precoUnitario": 22.50
    }
  ],
  "enderecoEntrega": "Rua MUITO LONGE do centro, 9999",
  "formaPagamento": "CREDITO"
}'

CASO_6='{
  "clienteId": "CLI_TIMEOUT",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD005",
      "nome": "Açaí 500ml",
      "quantidade": 1,
      "precoUnitario": 18.90
    }
  ],
  "enderecoEntrega": "Rua dos Pinheiros, 555",
  "formaPagamento": "CREDITO"
}'

CASO_7='{
  "clienteId": "CLI_VIP",
  "restauranteId": "REST_VIP",
  "itens": [
    {
      "produtoId": "PROD_PREMIUM",
      "nome": "Prato Executivo Premium",
      "quantidade": 1,
      "precoUnitario": 120.00
    }
  ],
  "enderecoEntrega": "Av. Empresarial, 1000 - Sala 301",
  "formaPagamento": "CREDITO"
}'

CASO_8='{
  "clienteId": "CLI004",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD001",
      "nome": "Pizza Margherita",
      "quantidade": 2,
      "precoUnitario": 45.90
    },
    {
      "produtoId": "PROD002",
      "nome": "Refrigerante 2L",
      "quantidade": 1,
      "precoUnitario": 8.50
    },
    {
      "produtoId": "PROD003",
      "nome": "Sorvete 1L",
      "quantidade": 1,
      "precoUnitario": 22.00
    }
  ],
  "enderecoEntrega": "Rua das Margaridas, 222",
  "formaPagamento": "PIX"
}'

CASO_9='{
  "clienteId": "CLI005",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD001",
      "nome": "Marmita Fitness",
      "quantidade": 1,
      "precoUnitario": 32.00
    }
  ],
  "enderecoEntrega": "Bairro Afastado, Km 15",
  "formaPagamento": "CREDITO"
}'

CASO_10='{
  "clienteId": "CLI_SEM_NOTIFICACAO",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD001",
      "nome": "Lanche Natural",
      "quantidade": 1,
      "precoUnitario": 15.50
    }
  ],
  "enderecoEntrega": "Rua das Oliveiras, 444",
  "formaPagamento": "DEBITO"
}'

CASO_11='{
  "clienteId": "CLI006",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD007",
      "nome": "Bolo de Aniversário",
      "quantidade": 1,
      "precoUnitario": 85.00
    }
  ],
  "enderecoEntrega": "Rua das Festas, 123 - Apto 501",
  "formaPagamento": "PIX"
}'

CASO_12='{
  "clienteId": "CLI007",
  "restauranteId": "REST001",
  "itens": [
    {
      "produtoId": "PROD008",
      "nome": "Combo Família",
      "quantidade": 1,
      "precoUnitario": 150.00
    }
  ],
  "enderecoEntrega": "Endereço MUITO LONGE E DISTANTE",
  "formaPagamento": "CREDITO"
}'

# ========================================
# Casos: nome e desfecho esperado
# ========================================

NOMES=("" "Pedido Normal (Happy Path)" "Restaurante Fechado" "Item Indisponível"
    "Pagamento Recusado" "Sem Entregador Disponível" "Timeout no Pagamento"
    "Pedido Premium (VIP)" "Múltiplos Itens" "Endereço Longe"
    "Falha na Notificação" "Pedido Agendado" "Compensação Total")

ESPERADO=("" CONCLUIDO CANCELADO CANCELADO COMPENSADO COMPENSADO COMPENSADO
    CONCLUIDO CONCLUIDO CONCLUIDO CONCLUIDO CONCLUIDO COMPENSADO)

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

declare -A PEDIDO_ID

# Envia o payload como arquivo para preservar o UTF-8: passar JSON com acento
# direto no argumento do curl corrompe o texto no Git Bash do Windows.
enviar_caso() {
    local numero=$1
    local var="CASO_$numero"
    local arquivo="$TMP_DIR/caso$numero.json"
    printf '%s' "${!var}" > "$arquivo"

    local resposta http_status corpo
    resposta=$(curl -s -X POST "$API_ENDPOINT" \
        -H "Content-Type: application/json; charset=utf-8" \
        --data-binary @"$arquivo" \
        -w $'\n%{http_code}')
    http_status=$(echo "$resposta" | tail -n1)
    corpo=$(echo "$resposta" | sed '$d')

    if [ "$http_status" == "202" ]; then
        PEDIDO_ID[$numero]=$(echo "$corpo" | sed -n 's/.*"pedidoId":"\([^"]*\)".*/\1/p')
        echo -e "${GRAY}Caso $numero (${NOMES[$numero]}): aceito, pedido ${PEDIDO_ID[$numero]}${NC}"
    else
        echo -e "${RED}Caso $numero (${NOMES[$numero]}): HTTP $http_status${NC}"
        echo -e "${RED}$corpo${NC}"
    fi
}

# Classifica o desfecho de um pedido a partir das linhas [SAGA] do orquestrador.
classificar() {
    local linhas=$1
    if echo "$linhas" | grep -q 'CONCLUÍDO COM SUCESSO'; then echo CONCLUIDO
    elif echo "$linhas" | grep -q 'COMPENSAÇÃO CONCLUÍDA'; then echo COMPENSADO
    elif echo "$linhas" | grep -q 'Cancelado:'; then echo CANCELADO
    elif [ -z "$linhas" ]; then echo SEM_REGISTRO
    else echo EM_ANDAMENTO
    fi
}

# ========================================
# EXECUÇÃO
# ========================================

CASO_USO=${1:-0}
if [ "$CASO_USO" -eq 0 ] 2>/dev/null; then
    CASOS=$(seq 1 12)
elif [ "$CASO_USO" -ge 1 ] 2>/dev/null && [ "$CASO_USO" -le 12 ]; then
    CASOS=$CASO_USO
else
    echo -e "${RED}Caso de uso inválido. Use valores de 1 a 12.${NC}"
    exit 1
fi

if ! docker inspect "$ORQUESTRADOR" >/dev/null 2>&1; then
    echo -e "${RED}Container '$ORQUESTRADOR' não encontrado. Suba a stack com: docker compose -f docker/docker-compose.yml up -d${NC}"
    exit 1
fi

echo -e "${CYAN}Testes dos casos de uso - POC SAGA Pattern${NC}"
echo -e "${CYAN}API: $API_ENDPOINT${NC}\n"

inicio=$(date -u +%Y-%m-%dT%H:%M:%SZ)
for n in $CASOS; do enviar_caso "$n"; done

# Aguarda até todos os pedidos aceitos terem desfecho ou o tempo acabar.
echo -e "\n${YELLOW}Aguardando os desfechos (até ${TEMPO_MAXIMO}s)...${NC}"
for _ in $(seq 1 "$TEMPO_MAXIMO"); do
    logs=$(docker logs "$ORQUESTRADOR" --since "$inicio" 2>&1 | grep '\[SAGA\]')
    pendentes=0
    for n in $CASOS; do
        [ -z "${PEDIDO_ID[$n]}" ] && continue
        d=$(classificar "$(echo "$logs" | grep -F "${PEDIDO_ID[$n]}")")
        [[ "$d" == SEM_REGISTRO || "$d" == EM_ANDAMENTO ]] && pendentes=$((pendentes + 1))
    done
    [ "$pendentes" -eq 0 ] && break
    sleep 1
done

# Relatório
falhas=0
avisos=0
echo
printf "%-5s %-28s %-13s %-13s %s\n" CASO NOME ESPERADO OBTIDO RESULTADO
for n in $CASOS; do
    if [ -z "${PEDIDO_ID[$n]}" ]; then
        obtido="NAO_ACEITO"; linhas=""
    else
        linhas=$(echo "$logs" | grep -F "${PEDIDO_ID[$n]}")
        obtido=$(classificar "$linhas")
    fi

    if [ "$obtido" == "${ESPERADO[$n]}" ]; then
        resultado="${GREEN}OK${NC}"
    elif [ "${ESPERADO[$n]}" == "CONCLUIDO" ] && [ "$obtido" == "COMPENSADO" ] \
        && echo "$linhas" | grep -q 'Tente novamente em alguns minutos'; then
        # O serviço de entregador simula horário de pico (12h-14h UTC) com 30%
        # de chance de não haver entregador: desfecho válido, mas não o esperado.
        resultado="${YELLOW}AVISO (horário de pico simulado)${NC}"
        avisos=$((avisos + 1))
    else
        resultado="${RED}FALHOU${NC}"
        falhas=$((falhas + 1))
    fi
    printf "%-5s %-28s %-13s %-13s " "$n" "${NOMES[$n]:0:28}" "${ESPERADO[$n]}" "$obtido"
    echo -e "$resultado"
done

echo
if [ "$falhas" -eq 0 ]; then
    echo -e "${GREEN}Todos os casos tiveram o desfecho esperado.${NC} Avisos: $avisos"
    exit 0
fi
echo -e "${RED}$falhas caso(s) com desfecho diferente do esperado.${NC} Avisos: $avisos"
echo -e "${YELLOW}Detalhes: docker logs $ORQUESTRADOR | grep '\[SAGA\]'${NC}"
exit 1
