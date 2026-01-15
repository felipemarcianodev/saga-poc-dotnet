#!/bin/bash

# Script Bash para testar todos os 12 casos de uso da POC SAGA
# Uso: ./testar-casos-de-uso.sh [numero-do-caso]
# Exemplo: ./testar-casos-de-uso.sh 1  (testa apenas o caso 1)
# Exemplo: ./testar-casos-de-uso.sh     (testa todos os casos)

BASE_URL="${BASE_URL:-http://localhost:5000}"
API_ENDPOINT="$BASE_URL/api/pedidos"

# Cores
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
MAGENTA='\033[0;35m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

testar_caso() {
    local numero=$1
    local nome=$2
    local payload=$3

    echo -e "\n${CYAN}========================================"
    echo -e "CASO $numero : $nome"
    echo -e "========================================${NC}\n"

    echo -e "${YELLOW}Payload:${NC}"
    echo -e "${GRAY}$payload${NC}"

    response=$(curl -s -X POST "$API_ENDPOINT" \
        -H "Content-Type: application/json" \
        -d "$payload" \
        -w "\nHTTP_STATUS:%{http_code}")

    http_status=$(echo "$response" | grep "HTTP_STATUS" | cut -d':' -f2)
    body=$(echo "$response" | sed '/HTTP_STATUS/d')

    if [ "$http_status" == "200" ] || [ "$http_status" == "201" ] || [ "$http_status" == "202" ]; then
        echo -e "\n${GREEN} Resposta (HTTP $http_status):${NC}"
        echo -e "${GREEN}$body${NC}" | jq '.' 2>/dev/null || echo "$body"

        pedido_id=$(echo "$body" | jq -r '.pedidoId' 2>/dev/null)
        status=$(echo "$body" | jq -r '.status' 2>/dev/null)

        echo -e "\n${MAGENTA}📊 PedidoId: $pedido_id${NC}"
        echo -e "${MAGENTA}Status: $status${NC}"

        echo -e "\n${YELLOW}⏳ Aguarde 5 segundos para verificar os logs dos serviços...${NC}\n"
        sleep 5
    else
        echo -e "\n${RED}❌ Erro ao chamar API (HTTP $http_status):${NC}"
        echo -e "${RED}$body${NC}"
    fi
}

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
# EXECUÇÃO
# ========================================

echo -e "${CYAN}"
cat << "EOF"
╔════════════════════════════════════════════════════════════╗
║                                                            ║
║     TESTES DOS CASOS DE USO - POC SAGA PATTERN             ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
EOF
echo -e "${NC}"

echo -e "${CYAN}API: $API_ENDPOINT${NC}\n"

CASO_USO=${1:-0}

if [ "$CASO_USO" -eq 0 ]; then
    # Testar todos os casos
    echo -e "${GREEN}🚀 Testando TODOS os 12 casos de uso...${NC}\n"

    testar_caso 1 "Pedido Normal (Happy Path)" "$CASO_1"
    testar_caso 2 "Restaurante Fechado" "$CASO_2"
    testar_caso 3 "Item Indisponível" "$CASO_3"
    testar_caso 4 "Pagamento Recusado" "$CASO_4"
    testar_caso 5 "Sem Entregador Disponível" "$CASO_5"
    testar_caso 6 "Timeout no Pagamento" "$CASO_6"
    testar_caso 7 "Pedido Premium (VIP)" "$CASO_7"
    testar_caso 8 "Múltiplos Itens" "$CASO_8"
    testar_caso 9 "Endereço Longe" "$CASO_9"
    testar_caso 10 "Falha na Notificação" "$CASO_10"
    testar_caso 11 "Pedido Agendado" "$CASO_11"
    testar_caso 12 "Compensação Total" "$CASO_12"

    echo -e "\n${GREEN} Todos os 12 casos foram executados!${NC}"
else
    # Testar caso específico
    case $CASO_USO in
        1) testar_caso 1 "Pedido Normal (Happy Path)" "$CASO_1" ;;
        2) testar_caso 2 "Restaurante Fechado" "$CASO_2" ;;
        3) testar_caso 3 "Item Indisponível" "$CASO_3" ;;
        4) testar_caso 4 "Pagamento Recusado" "$CASO_4" ;;
        5) testar_caso 5 "Sem Entregador Disponível" "$CASO_5" ;;
        6) testar_caso 6 "Timeout no Pagamento" "$CASO_6" ;;
        7) testar_caso 7 "Pedido Premium (VIP)" "$CASO_7" ;;
        8) testar_caso 8 "Múltiplos Itens" "$CASO_8" ;;
        9) testar_caso 9 "Endereço Longe" "$CASO_9" ;;
        10) testar_caso 10 "Falha na Notificação" "$CASO_10" ;;
        11) testar_caso 11 "Pedido Agendado" "$CASO_11" ;;
        12) testar_caso 12 "Compensação Total" "$CASO_12" ;;
        *) echo -e "${RED}❌ Caso de uso inválido. Use valores de 1 a 12.${NC}" ;;
    esac
fi

echo -e "\n${YELLOW}📝 Para ver os logs detalhados, verifique o console dos serviços em execução.${NC}\n"
