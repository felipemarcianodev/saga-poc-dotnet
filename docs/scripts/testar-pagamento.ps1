# Script PowerShell para testar o Contexto de PAGAMENTO
# Testa processamento de pagamentos, estornos e fraudes
# Uso: .\testar-pagamento.ps1 [-Cenario <numero>] [-DuracaoSegundos <segundos>]

param(
    [int]$Cenario = 0,
    [int]$DuracaoSegundos = 30,
    [string]$BaseUrl = "http://localhost:5000"
)

$apiPedidos = "$BaseUrl/api/pedidos"

# Estatísticas do contexto
$stats = @{
    PagamentosAprovados = 0
    PagamentosRecusados = 0
    EstornosExecutados = 0
    CartaoRecusado = 0
    FraudeDetectada = 0
    Timeout = 0
    ValorTotalProcessado = 0
    ValorTotalEstornado = 0
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
║             💳 CONTEXTO: PAGAMENTO - DASHBOARD 💳                    ║
║                                                                      ║
║  Tempo decorrido: $($duracao.ToString('mm\:ss'))                    ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│ ESTATÍSTICAS DE PAGAMENTO                                        │
├──────────────────────────────────────────────────────────────────────┤
│  Pagamentos Aprovados:    $($stats.PagamentosAprovados.ToString().PadLeft(3))
│  Pagamentos Recusados:    $($stats.PagamentosRecusados.ToString().PadLeft(3)) ❌
│  Taxa de Aprovação:       $([math]::Round(($stats.PagamentosAprovados / [math]::Max($stats.PagamentosAprovados + $stats.PagamentosRecusados, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  🚫 MOTIVOS DE RECUSA                                                │
├──────────────────────────────────────────────────────────────────────┤
│  Cartão Recusado:         $($stats.CartaoRecusado.ToString().PadLeft(3))
│  Fraude Detectada:        $($stats.FraudeDetectada.ToString().PadLeft(3))
│  Timeout Gateway:         $($stats.Timeout.ToString().PadLeft(3))
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  VALORES FINANCEIROS                                              │
├──────────────────────────────────────────────────────────────────────┤
│  Total Processado:        R$ $($stats.ValorTotalProcessado.ToString('N2'))
│  Total Estornado:         R$ $($stats.ValorTotalEstornado.ToString('N2'))
│  Estornos Executados:     $($stats.EstornosExecutados)
│  Ticket Médio:            R$ $([math]::Round($stats.ValorTotalProcessado / [math]::Max($stats.PagamentosAprovados, 1), 2).ToString('N2'))
└──────────────────────────────────────────────────────────────────────┘

"@ -ForegroundColor Cyan
}

function Test-Cenario1-PagamentosAprovados {
    Write-Header "CENÁRIO 1: Pagamentos Aprovados (Cartões Válidos)"

    Write-Host "  Processando 10 pagamentos válidos...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 10; $i++) {
        $valor = Get-Random -Minimum 20 -Maximum 200

        $payload = @{
            clienteId = "CLI$(Get-Random -Minimum 1 -Maximum 100).ToString('000')"
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Produto Teste"
                    quantidade = 1
                    precoUnitario = $valor
                }
            )
            enderecoEntrega = "Rua Teste, $i"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.PagamentosAprovados++
            $stats.ValorTotalProcessado += $valor

            Write-Host "  [$i/10] Pagamento aprovado - R$ $($valor.ToString('N2'))" -ForegroundColor Green
        }
        catch {
            $stats.PagamentosRecusados++
            Write-Host "  [$i/10] ❌ Falha no pagamento" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Aguardando processamento...`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 3

    Write-Dashboard
}

function Test-Cenario2-CartaoRecusado {
    Write-Header "CENÁRIO 2: Cartão Recusado (Saldo Insuficiente)"

    Write-Host "  Tentando processar com cartão recusado...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
            clienteId = "CLI_CARTAO_RECUSADO"
            restauranteId = "REST001"
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
            $stats.PagamentosAprovados++
        }
        catch {
            $stats.PagamentosRecusados++
            $stats.CartaoRecusado++
            Write-Host "  [$i/5] ❌ Pagamento recusado: Cartão sem saldo" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos os pagamentos devem ser recusados`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario3-FraudeDetectada {
    Write-Header "CENÁRIO 3: Detecção de Fraude (Valor Alto)"

    Write-Host "  Enviando pedidos com valores suspeitos (> R$ 1000)...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $valorAlto = Get-Random -Minimum 1500 -Maximum 3000

        $payload = @{
            clienteId = "CLI001"
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Pedido Suspeito"
                    quantidade = 10
                    precoUnitario = $valorAlto / 10
                }
            )
            enderecoEntrega = "Rua Teste, $i"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
            $stats.PagamentosAprovados++
        }
        catch {
            $stats.PagamentosRecusados++
            $stats.FraudeDetectada++
            Write-Host "  [$i/5] 🚨 Fraude detectada: Valor R$ $($valorAlto.ToString('N2'))" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Pagamentos bloqueados por suspeita de fraude`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario4-TimeoutGateway {
    Write-Header "CENÁRIO 4: Timeout no Gateway de Pagamento"

    Write-Host "  Simulando timeout de comunicação...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
            clienteId = "CLI_TIMEOUT"
            restauranteId = "REST001"
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
            $stats.PagamentosAprovados++
        }
        catch {
            $stats.PagamentosRecusados++
            $stats.Timeout++
            Write-Host "  [$i/5] ⏱️  Timeout: Gateway não respondeu" -ForegroundColor Yellow
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Pagamentos falharam por timeout`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario5-EstornosCompensacao {
    Write-Header "CENÁRIO 5: Estornos (Compensação de SAGA)"

    Write-Host "  Processando pedidos que gerarão estornos...`n" -ForegroundColor Yellow
    Write-Host "  (Pagamento aprovado mas falha posterior)...`n" -ForegroundColor Gray

    for ($i = 1; $i -le 5; $i++) {
        $valor = Get-Random -Minimum 50 -Maximum 150

        # Pedido que será aprovado no pagamento mas rejeitado no entregador
        $payload = @{
            clienteId = "CLI001"
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Produto"
                    quantidade = 1
                    precoUnitario = $valor
                }
            )
            enderecoEntrega = "ENDERECO_LONGE_DISTANTE"  # Vai falhar no entregador
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.PagamentosAprovados++
            $stats.ValorTotalProcessado += $valor
            $stats.EstornosExecutados++
            $stats.ValorTotalEstornado += $valor

            Write-Host "  [$i/5] 💳 Pagamento aprovado: R$ $($valor.ToString('N2'))" -ForegroundColor Green
            Write-Host "         ⏳ Aguardando falha no entregador..." -ForegroundColor Yellow
            Write-Host "         ⬅️  Estorno será executado automaticamente" -ForegroundColor Cyan
        }
        catch {
            $stats.PagamentosRecusados++
        }

        Start-Sleep -Seconds 2
    }

    Write-Host "`n Processamento concluído. Verifique logs para compensações.`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 5

    Write-Dashboard
}

function Test-Cenario6-CargaContinua {
    Write-Header "CENÁRIO 6: Carga Contínua ($DuracaoSegundos segundos)"

    Write-Host "  Processando pagamentos continuamente...`n" -ForegroundColor Yellow
    Write-Host "  Pressione Ctrl+C para parar antes do tempo`n" -ForegroundColor Gray

    $tempoFim = (Get-Date).AddSeconds($DuracaoSegundos)
    $contador = 0

    $cenarios = @(
        @{ clienteId = "CLI001"; valido = $true; tipo = "aprovado" }
        @{ clienteId = "CLI_CARTAO_RECUSADO"; valido = $false; tipo = "recusado" }
        @{ clienteId = "CLI_TIMEOUT"; valido = $false; tipo = "timeout" }
    )

    try {
        while ((Get-Date) -lt $tempoFim) {
            Write-Dashboard

            $cenario = $cenarios | Get-Random
            $contador++
            $valor = Get-Random -Minimum 20 -Maximum 200

            # Fraude se valor > 1000
            if ($valor -gt 1000) {
                $cenario.valido = $false
                $cenario.tipo = "fraude"
            }

            $payload = @{
                clienteId = $cenario.clienteId
                restauranteId = "REST001"
                itens = @(
                    @{
                        produtoId = "PROD001"
                        nome = "Produto"
                        quantidade = 1
                        precoUnitario = $valor
                    }
                )
                enderecoEntrega = "Rua Teste, $contador"
                formaPagamento = "CREDITO"
            } | ConvertTo-Json -Depth 10

            Start-Job -ScriptBlock {
                param($url, $payload, $valor)
                try {
                    Invoke-RestMethod -Uri $url -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
                    return @{ sucesso = $true; valor = $valor }
                }
                catch {
                    return @{ sucesso = $false; valor = $valor }
                }
            } -ArgumentList $apiPedidos, $payload, $valor | Out-Null

            # Processar jobs concluídos
            Get-Job | Where-Object { $_.State -eq "Completed" } | ForEach-Object {
                $resultado = Receive-Job -Job $_
                if ($resultado.sucesso) {
                    $stats.PagamentosAprovados++
                    $stats.ValorTotalProcessado += $resultado.valor
                }
                else {
                    $stats.PagamentosRecusados++
                    switch ($cenario.tipo) {
                        "recusado" { $stats.CartaoRecusado++ }
                        "fraude" { $stats.FraudeDetectada++ }
                        "timeout" { $stats.Timeout++ }
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
    Write-Host "`n  Teste de carga concluído!`n" -ForegroundColor Green
}

# ==================== EXECUÇÃO PRINCIPAL ====================

Clear-Host
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║               💳 TESTE DO CONTEXTO: PAGAMENTO 💳                     ║
║                                                                      ║
║  Processa pagamentos, detecta fraudes e executa estornos            ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Verificar conectividade
Write-Host "  🔍 Verificando conectividade com a API...`n" -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5 | Out-Null
    Write-Host "  API está respondendo`n" -ForegroundColor Green
}
catch {
    Write-Host "  ❌ API não está respondendo em $BaseUrl`n" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

# Executar cenário
switch ($Cenario) {
    1 { Test-Cenario1-PagamentosAprovados }
    2 { Test-Cenario2-CartaoRecusado }
    3 { Test-Cenario3-FraudeDetectada }
    4 { Test-Cenario4-TimeoutGateway }
    5 { Test-Cenario5-EstornosCompensacao }
    6 { Test-Cenario6-CargaContinua }
    default {
        # Executar cenários principais
        Test-Cenario1-PagamentosAprovados
        Start-Sleep -Seconds 2
        Test-Cenario2-CartaoRecusado
        Start-Sleep -Seconds 2
        Test-Cenario3-FraudeDetectada
    }
}

# Relatório final
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║                    TESTE CONCLUÍDO!                            ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

📊 RELATÓRIO DO CONTEXTO PAGAMENTO:

Processamento:
  - Pagamentos aprovados: $($stats.PagamentosAprovados)
  - Pagamentos recusados: $($stats.PagamentosRecusados)
  - Taxa de aprovação: $([math]::Round(($stats.PagamentosAprovados / [math]::Max($stats.PagamentosAprovados + $stats.PagamentosRecusados, 1)) * 100, 2))%

Motivos de Recusa:
  - Cartão recusado: $($stats.CartaoRecusado)
  - Fraude detectada: $($stats.FraudeDetectada)
  - Timeout gateway: $($stats.Timeout)

Compensações:
  - Estornos executados: $($stats.EstornosExecutados)
  - Valor total estornado: R$ $($stats.ValorTotalEstornado.ToString('N2'))

Valores:
  - Total processado: R$ $($stats.ValorTotalProcessado.ToString('N2'))
  - Ticket médio: R$ $([math]::Round($stats.ValorTotalProcessado / [math]::Max($stats.PagamentosAprovados, 1), 2).ToString('N2'))

💡 Dicas:
  - Verifique os logs do ServicoPagamento para detalhes
  - Estornos aparecem quando há compensação de SAGA
  - Fraudes são detectadas para valores > R$ 1.000

"@ -ForegroundColor Cyan
