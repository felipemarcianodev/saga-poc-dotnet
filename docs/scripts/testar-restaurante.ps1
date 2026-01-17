# Script PowerShell para testar o Contexto de RESTAURANTE
# Testa validacões de pedidos, disponibilidade e cancelamentos
# Uso: .\testar-restaurante.ps1 [-Cenario <numero>] [-DuracaoSegundos <segundos>]

param(
    [int]$Cenario = 0,
    [int]$DuracaoSegundos = 30,
    [string]$BaseUrl = "http://localhost:5000"
)

$apiPedidos = "$BaseUrl/api/pedidos"

# Estatisticas do contexto
$stats = @{
    PedidosValidados = 0
    PedidosRejeitados = 0
    RestauranteFechado = 0
    ItemIndisponivel = 0
    ValorTotal = 0
    TempoInicioTeste = Get-Date
}

function Write-Header {
    param([string]$Text)
    Write-Host "`n╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $Text" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
}

function Write-Dashboard {
    Clear-Host
    $duracao = (Get-Date) - $stats.TempoInicioTeste

    Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║           🍕 CONTEXTO: RESTAURANTE - DASHBOARD 🍕                    ║
║                                                                      ║
║  Tempo decorrido: $($duracao.ToString('mm\:ss'))                    ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│ ESTATÍSTICAS DE VALIDAÇÃO                                        │
├──────────────────────────────────────────────────────────────────────┤
│  Pedidos Validados:       $($stats.PedidosValidados.ToString().PadLeft(3))
│  Pedidos Rejeitados:      $($stats.PedidosRejeitados.ToString().PadLeft(3)) ❌
│  Taxa de Aprovacao:       $([math]::Round(($stats.PedidosValidados / [math]::Max($stats.PedidosValidados + $stats.PedidosRejeitados, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  🚫 MOTIVOS DE REJEIÇÃO                                              │
├──────────────────────────────────────────────────────────────────────┤
│  Restaurante Fechado:     $($stats.RestauranteFechado.ToString().PadLeft(3))
│  Item Indisponivel:       $($stats.ItemIndisponivel.ToString().PadLeft(3))
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  VALORES PROCESSADOS                                              │
├──────────────────────────────────────────────────────────────────────┤
│  Valor Total Validado:    R$ $($stats.ValorTotal.ToString('N2'))
│  Ticket Médio:            R$ $([math]::Round($stats.ValorTotal / [math]::Max($stats.PedidosValidados, 1), 2).ToString('N2'))
└──────────────────────────────────────────────────────────────────────┘

"@ -ForegroundColor Cyan
}

function Test-Cenario1-PedidosValidos {
    Write-Header "CENÁRIO 1: Pedidos Validos (Aprovacao Total)"

    $restaurantes = @("REST001", "REST002", "REST003")
    $produtos = @(
        @{ id = "PROD001"; nome = "Pizza Margherita"; preco = 45.90 }
        @{ id = "PROD002"; nome = "Hambúrguer Artesanal"; preco = 38.50 }
        @{ id = "PROD003"; nome = "Sushi Combo"; preco = 68.00 }
    )

    Write-Host "  Enviando 10 pedidos validos...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 10; $i++) {
        $restaurante = $restaurantes | Get-Random
        $produto = $produtos | Get-Random

        $payload = @{
            clienteId = "CLI$(Get-Random -Minimum 1 -Maximum 100).ToString('000')"
            restauranteId = $restaurante
            itens = @(
                @{
                    produtoId = $produto.id
                    nome = $produto.nome
                    quantidade = Get-Random -Minimum 1 -Maximum 3
                    precoUnitario = $produto.preco
                }
            )
            enderecoEntrega = "Rua Teste, $i"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.PedidosValidados++
            $stats.ValorTotal += $produto.preco

            Write-Host "  [$i/10] Pedido $($response.pedidoId.Substring(0,8))... - $restaurante - R$ $($produto.preco.ToString('N2'))" -ForegroundColor Green
        }
        catch {
            $stats.PedidosRejeitados++
            Write-Host "  [$i/10] ❌ Falha ao enviar pedido" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Aguardando processamento...`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 3

    Write-Dashboard
}

function Test-Cenario2-RestauranteFechado {
    Write-Header "CENÁRIO 2: Restaurante Fechado (Rejeicao Esperada)"

    Write-Host "  Enviando pedidos para restaurante fechado...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
            clienteId = "CLI001"
            restauranteId = "REST_FECHADO"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Pizza"
                    quantidade = 1
                    precoUnitario = 45.90
                }
            )
            enderecoEntrega = "Rua Teste, $i"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
            $stats.PedidosValidados++
        }
        catch {
            $stats.PedidosRejeitados++
            $stats.RestauranteFechado++
            Write-Host "  [$i/5] ❌ Pedido rejeitado: Restaurante fechado" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos os pedidos devem ser rejeitados`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario3-ItemIndisponivel {
    Write-Header "CENÁRIO 3: Item Indisponivel (Estoque Zerado)"

    Write-Host "  Enviando pedidos com itens indisponiveis...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
            clienteId = "CLI001"
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "INDISPONIVEL"
                    nome = "Produto Esgotado"
                    quantidade = 1
                    precoUnitario = 35.00
                }
            )
            enderecoEntrega = "Rua Teste, $i"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
            $stats.PedidosValidados++
        }
        catch {
            $stats.PedidosRejeitados++
            $stats.ItemIndisponivel++
            Write-Host "  [$i/5] ❌ Pedido rejeitado: Item indisponivel" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos os pedidos devem ser rejeitados`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario4-CargaContinua {
    Write-Header "CENÁRIO 4: Carga Continua ($DuracaoSegundos segundos)"

    Write-Host "  Enviando pedidos continuamente...`n" -ForegroundColor Yellow
    Write-Host "  Pressione Ctrl+C para parar antes do tempo`n" -ForegroundColor Gray

    $tempoFim = (Get-Date).AddSeconds($DuracaoSegundos)
    $contador = 0

    $cenarios = @(
        @{ restaurante = "REST001"; produto = "PROD001"; nome = "Pizza"; preco = 45.90; valido = $true }
        @{ restaurante = "REST002"; produto = "PROD002"; nome = "Hambúrguer"; preco = 38.50; valido = $true }
        @{ restaurante = "REST_FECHADO"; produto = "PROD001"; nome = "Pizza"; preco = 45.90; valido = $false }
        @{ restaurante = "REST001"; produto = "INDISPONIVEL"; nome = "Esgotado"; preco = 35.00; valido = $false }
    )

    try {
        while ((Get-Date) -lt $tempoFim) {
            Write-Dashboard

            $cenario = $cenarios | Get-Random
            $contador++

            $payload = @{
                clienteId = "CLI$(Get-Random -Minimum 1 -Maximum 100).ToString('000')"
                restauranteId = $cenario.restaurante
                itens = @(
                    @{
                        produtoId = $cenario.produto
                        nome = $cenario.nome
                        quantidade = Get-Random -Minimum 1 -Maximum 3
                        precoUnitario = $cenario.preco
                    }
                )
                enderecoEntrega = "Rua Teste, $contador"
                formaPagamento = "CREDITO"
            } | ConvertTo-Json -Depth 10

            Start-Job -ScriptBlock {
                param($url, $payload)
                try {
                    Invoke-RestMethod -Uri $url -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
                    return $true
                }
                catch {
                    return $false
                }
            } -ArgumentList $apiPedidos, $payload | Out-Null

            # Processar jobs concluidos
            Get-Job | Where-Object { $_.State -eq "Completed" } | ForEach-Object {
                $resultado = Receive-Job -Job $_
                if ($resultado) {
                    $stats.PedidosValidados++
                    $stats.ValorTotal += 45.90
                }
                else {
                    $stats.PedidosRejeitados++
                    if ($cenario.restaurante -eq "REST_FECHADO") {
                        $stats.RestauranteFechado++
                    }
                    else {
                        $stats.ItemIndisponivel++
                    }
                }
                Remove-Job -Job $_
            }

            Start-Sleep -Seconds 2
        }
    }
    finally {
        Get-Job | Stop-Job
        Get-Job | Remove-Job -Force
    }

    Write-Dashboard
    Write-Host "`n  Teste de carga concluido!`n" -ForegroundColor Green
}

# ==================== EXECUÇÃO PRINCIPAL ====================

Clear-Host
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║              🍕 TESTE DO CONTEXTO: RESTAURANTE 🍕                    ║
║                                                                      ║
║  Valida pedidos, verifica disponibilidade e gerencia estoque        ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Verificar conectividade
Write-Host "  🔍 Verificando conectividade com a API...`n" -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5 | Out-Null
    Write-Host "  API esta respondendo`n" -ForegroundColor Green
}
catch {
    Write-Host "  ❌ API nao esta respondendo em $BaseUrl`n" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

# Executar cenario
switch ($Cenario) {
    1 { Test-Cenario1-PedidosValidos }
    2 { Test-Cenario2-RestauranteFechado }
    3 { Test-Cenario3-ItemIndisponivel }
    4 { Test-Cenario4-CargaContinua }
    default {
        # Executar todos os cenarios
        Test-Cenario1-PedidosValidos
        Start-Sleep -Seconds 2
        Test-Cenario2-RestauranteFechado
        Start-Sleep -Seconds 2
        Test-Cenario3-ItemIndisponivel
    }
}

# Relatório final
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║                    TESTE CONCLUÍDO!                            ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

📊 RELATÓRIO DO CONTEXTO RESTAURANTE:

Validacões:
  - Pedidos validados: $($stats.PedidosValidados)
  - Pedidos rejeitados: $($stats.PedidosRejeitados)
  - Taxa de aprovacao: $([math]::Round(($stats.PedidosValidados / [math]::Max($stats.PedidosValidados + $stats.PedidosRejeitados, 1)) * 100, 2))%

Motivos de Rejeicao:
  - Restaurante fechado: $($stats.RestauranteFechado)
  - Item indisponivel: $($stats.ItemIndisponivel)

Valores:
  - Valor total validado: R$ $($stats.ValorTotal.ToString('N2'))
  - Ticket médio: R$ $([math]::Round($stats.ValorTotal / [math]::Max($stats.PedidosValidados, 1), 2).ToString('N2'))

💡 Dicas:
  - Verifique os logs do ServicoRestaurante para detalhes
  - Acompanhe as filas no RabbitMQ Management: http://localhost:15672

"@ -ForegroundColor Cyan
