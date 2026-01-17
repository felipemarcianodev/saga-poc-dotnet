# Script PowerShell para testar todos os 12 casos de uso da POC SAGA
# Uso: .\testar-casos-de-uso.ps1 [numero-do-caso]
# Exemplo: .\testar-casos-de-uso.ps1 1  (testa apenas o caso 1)
# Exemplo: .\testar-casos-de-uso.ps1     (testa todos os casos)

param(
    [int]$CasoUso = 0,
    [string]$BaseUrl = "http://localhost:5000"
)

$apiEndpoint = "$BaseUrl/api/pedidos"

function Invoke-TestarCaso {
    param(
        [int]$Numero,
        [string]$Nome,
        [hashtable]$Payload
    )

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "CASO $Numero : $Nome" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    $json = $Payload | ConvertTo-Json -Depth 10

    Write-Host "Payload:" -ForegroundColor Yellow
    Write-Host $json -ForegroundColor Gray

    try {
        $response = Invoke-RestMethod -Uri $apiEndpoint -Method Post -Body $json -ContentType "application/json"

        Write-Host "`n Resposta:" -ForegroundColor Green
        $response | ConvertTo-Json | Write-Host -ForegroundColor Green

        Write-Host "`n📊 PedidoId: $($response.pedidoId)" -ForegroundColor Magenta
        Write-Host "Status: $($response.status)" -ForegroundColor Magenta

        Write-Host "`n⏳ Aguarde 5 segundos para verificar os logs dos servicos...`n" -ForegroundColor Yellow
        Start-Sleep -Seconds 5
    }
    catch {
        Write-Host "`n❌ Erro ao chamar API:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}

# ========================================
# CASO 1: Pedido Normal (Happy Path)
# ========================================
$caso1 = @{
    clienteId = "CLI001"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD001"
            nome = "Pizza Margherita"
            quantidade = 1
            precoUnitario = 45.90
        }
    )
    enderecoEntrega = "Rua das Flores, 123 - Centro"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 2: Restaurante Fechado
# ========================================
$caso2 = @{
    clienteId = "CLI001"
    restauranteId = "REST_FECHADO"
    itens = @(
        @{
            produtoId = "PROD001"
            nome = "Hambúrguer Artesanal"
            quantidade = 2
            precoUnitario = 28.50
        }
    )
    enderecoEntrega = "Av. Principal, 456"
    formaPagamento = "DEBITO"
}

# ========================================
# CASO 3: Item Indisponivel
# ========================================
$caso3 = @{
    clienteId = "CLI002"
    restauranteId = "REST002"
    itens = @(
        @{
            produtoId = "INDISPONIVEL"
            nome = "Produto Esgotado"
            quantidade = 1
            precoUnitario = 35.00
        }
    )
    enderecoEntrega = "Rua das Palmeiras, 789"
    formaPagamento = "PIX"
}

# ========================================
# CASO 4: Pagamento Recusado
# ========================================
$caso4 = @{
    clienteId = "CLI_CARTAO_RECUSADO"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD003"
            nome = "Sushi Combo"
            quantidade = 1
            precoUnitario = 89.90
        }
    )
    enderecoEntrega = "Rua das Acacias, 321"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 5: Sem Entregador Disponivel
# ========================================
$caso5 = @{
    clienteId = "CLI003"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD004"
            nome = "Salada Caesar"
            quantidade = 3
            precoUnitario = 22.50
        }
    )
    enderecoEntrega = "Rua MUITO LONGE do centro, 9999"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 6: Timeout no Pagamento
# ========================================
$caso6 = @{
    clienteId = "CLI_TIMEOUT"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD005"
            nome = "Acai 500ml"
            quantidade = 1
            precoUnitario = 18.90
        }
    )
    enderecoEntrega = "Rua dos Pinheiros, 555"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 7: Pedido Premium (VIP)
# ========================================
$caso7 = @{
    clienteId = "CLI_VIP"
    restauranteId = "REST_VIP"
    itens = @(
        @{
            produtoId = "PROD_PREMIUM"
            nome = "Prato Executivo Premium"
            quantidade = 1
            precoUnitario = 120.00
        }
    )
    enderecoEntrega = "Av. Empresarial, 1000 - Sala 301"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 8: Múltiplos Itens
# ========================================
$caso8 = @{
    clienteId = "CLI004"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD001"
            nome = "Pizza Margherita"
            quantidade = 2
            precoUnitario = 45.90
        },
        @{
            produtoId = "PROD002"
            nome = "Refrigerante 2L"
            quantidade = 1
            precoUnitario = 8.50
        },
        @{
            produtoId = "PROD003"
            nome = "Sorvete 1L"
            quantidade = 1
            precoUnitario = 22.00
        }
    )
    enderecoEntrega = "Rua das Margaridas, 222"
    formaPagamento = "PIX"
}

# ========================================
# CASO 9: Endereco Longe
# ========================================
$caso9 = @{
    clienteId = "CLI005"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD001"
            nome = "Marmita Fitness"
            quantidade = 1
            precoUnitario = 32.00
        }
    )
    enderecoEntrega = "Bairro Afastado, Km 15"
    formaPagamento = "CREDITO"
}

# ========================================
# CASO 10: Falha na Notificacao
# ========================================
$caso10 = @{
    clienteId = "CLI_SEM_NOTIFICACAO"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD001"
            nome = "Lanche Natural"
            quantidade = 1
            precoUnitario = 15.50
        }
    )
    enderecoEntrega = "Rua das Oliveiras, 444"
    formaPagamento = "DEBITO"
}

# ========================================
# CASO 11: Pedido Agendado
# ========================================
$caso11 = @{
    clienteId = "CLI006"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD007"
            nome = "Bolo de Aniversario"
            quantidade = 1
            precoUnitario = 85.00
        }
    )
    enderecoEntrega = "Rua das Festas, 123 - Apto 501"
    formaPagamento = "PIX"
}

# ========================================
# CASO 12: Compensacao Total
# ========================================
$caso12 = @{
    clienteId = "CLI007"
    restauranteId = "REST001"
    itens = @(
        @{
            produtoId = "PROD008"
            nome = "Combo Familia"
            quantidade = 1
            precoUnitario = 150.00
        }
    )
    enderecoEntrega = "Endereco MUITO LONGE E DISTANTE"
    formaPagamento = "CREDITO"
}

# ========================================
# EXECUÇÃO
# ========================================

Write-Host @"
╔════════════════════════════════════════════════════════════╗
║                                                            ║
║     TESTES DOS CASOS DE USO - POC SAGA PATTERN             ║
║                                                            ║
║     API: $apiEndpoint                     ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

if ($CasoUso -eq 0) {
    # Testar todos os casos
    Write-Host "`n🚀 Testando TODOS os 12 casos de uso...`n" -ForegroundColor Green

    Invoke-TestarCaso -Numero 1 -Nome "Pedido Normal (Happy Path)" -Payload $caso1
    Invoke-TestarCaso -Numero 2 -Nome "Restaurante Fechado" -Payload $caso2
    Invoke-TestarCaso -Numero 3 -Nome "Item Indisponivel" -Payload $caso3
    Invoke-TestarCaso -Numero 4 -Nome "Pagamento Recusado" -Payload $caso4
    Invoke-TestarCaso -Numero 5 -Nome "Sem Entregador Disponivel" -Payload $caso5
    Invoke-TestarCaso -Numero 6 -Nome "Timeout no Pagamento" -Payload $caso6
    Invoke-TestarCaso -Numero 7 -Nome "Pedido Premium (VIP)" -Payload $caso7
    Invoke-TestarCaso -Numero 8 -Nome "Múltiplos Itens" -Payload $caso8
    Invoke-TestarCaso -Numero 9 -Nome "Endereco Longe" -Payload $caso9
    Invoke-TestarCaso -Numero 10 -Nome "Falha na Notificacao" -Payload $caso10
    Invoke-TestarCaso -Numero 11 -Nome "Pedido Agendado" -Payload $caso11
    Invoke-TestarCaso -Numero 12 -Nome "Compensacao Total" -Payload $caso12

    Write-Host "`n Todos os 12 casos foram executados!" -ForegroundColor Green
}
else {
    # Testar caso especifico
    switch ($CasoUso) {
        1 { Invoke-TestarCaso -Numero 1 -Nome "Pedido Normal (Happy Path)" -Payload $caso1 }
        2 { Invoke-TestarCaso -Numero 2 -Nome "Restaurante Fechado" -Payload $caso2 }
        3 { Invoke-TestarCaso -Numero 3 -Nome "Item Indisponivel" -Payload $caso3 }
        4 { Invoke-TestarCaso -Numero 4 -Nome "Pagamento Recusado" -Payload $caso4 }
        5 { Invoke-TestarCaso -Numero 5 -Nome "Sem Entregador Disponivel" -Payload $caso5 }
        6 { Invoke-TestarCaso -Numero 6 -Nome "Timeout no Pagamento" -Payload $caso6 }
        7 { Invoke-TestarCaso -Numero 7 -Nome "Pedido Premium (VIP)" -Payload $caso7 }
        8 { Invoke-TestarCaso -Numero 8 -Nome "Múltiplos Itens" -Payload $caso8 }
        9 { Invoke-TestarCaso -Numero 9 -Nome "Endereco Longe" -Payload $caso9 }
        10 { Invoke-TestarCaso -Numero 10 -Nome "Falha na Notificacao" -Payload $caso10 }
        11 { Invoke-TestarCaso -Numero 11 -Nome "Pedido Agendado" -Payload $caso11 }
        12 { Invoke-TestarCaso -Numero 12 -Nome "Compensacao Total" -Payload $caso12 }
        default { Write-Host "❌ Caso de uso invalido. Use valores de 1 a 12." -ForegroundColor Red }
    }
}

Write-Host "`n📝 Para ver os logs detalhados, verifique o console dos servicos em execucao.`n" -ForegroundColor Yellow
