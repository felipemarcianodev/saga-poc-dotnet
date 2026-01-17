# Script PowerShell para testar TODO o sistema (SAGA + Fluxo de Caixa)
# Mostra dashboard em tempo real com estatisticas
# Uso: .\testar-sistema-completo.ps1 [-DuracaoSegundos <segundos>]

param(
    [int]$DuracaoSegundos = 60,
    [string]$BaseUrlSaga = "http://localhost:5000",
    [string]$BaseUrlFluxoCaixa = "http://localhost:5000",
    [string]$RabbitMQUrl = "http://localhost:15672",
    [string]$RabbitMQUser = "saga",
    [string]$RabbitMQPass = "saga123"
)

$ErrorActionPreference = "SilentlyContinue"

# Estatisticas globais
$stats = @{
    SagaPedidosSucesso = 0
    SagaPedidosFalha = 0
    FluxoCaixaLancamentos = 0
    FluxoCaixaErros = 0
    TempoInicioTeste = Get-Date
}

function Write-Dashboard {
    Clear-Host
    $duracao = (Get-Date) - $stats.TempoInicioTeste

    Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║               DASHBOARD DO SISTEMA COMPLETO                    ║
║                                                                      ║
║  Tempo decorrido: $($duracao.ToString('mm\:ss'))                    ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│  SAGA PATTERN (Delivery de Comida)                                │
├──────────────────────────────────────────────────────────────────────┤
│  Pedidos com Sucesso:  $($stats.SagaPedidosSucesso.ToString().PadLeft(3))
│  Pedidos com Falha:    $($stats.SagaPedidosFalha.ToString().PadLeft(3)) ❌
│  Taxa de Sucesso:      $([math]::Round(($stats.SagaPedidosSucesso / [math]::Max($stats.SagaPedidosSucesso + $stats.SagaPedidosFalha, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  FLUXO DE CAIXA (CQRS + Event-Driven)                             │
├──────────────────────────────────────────────────────────────────────┤
│  Lancamentos Criados:  $($stats.FluxoCaixaLancamentos.ToString().PadLeft(3)) 📊
│  Erros de Validacao:   $($stats.FluxoCaixaErros.ToString().PadLeft(3)) ⚠️
│  Taxa de Sucesso:      $([math]::Round(($stats.FluxoCaixaLancamentos / [math]::Max($stats.FluxoCaixaLancamentos + $stats.FluxoCaixaErros, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

"@ -ForegroundColor Cyan

    # Obter estatisticas do RabbitMQ
    try {
        $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitMQUser}:${RabbitMQPass}"))
        $headers = @{ Authorization = "Basic $cred" }
        $queues = Invoke-RestMethod -Uri "$RabbitMQUrl/api/queues" -Headers $headers -Method Get

        Write-Host "┌──────────────────────────────────────────────────────────────────────┐" -ForegroundColor Yellow
        Write-Host "│  🐰 RABBITMQ - Filas                                                 │" -ForegroundColor Yellow
        Write-Host "├──────────────────────────────────────────────────────────────────────┤" -ForegroundColor Yellow

        foreach ($queue in $queues | Where-Object { $_.name -match "saga|fluxocaixa" }) {
            $nome = $queue.name.PadRight(35)
            $ready = $queue.messages_ready.ToString().PadLeft(5)
            $unacked = $queue.messages_unacknowledged.ToString().PadLeft(5)

            Write-Host "│  $nome Ready: $ready Unacked: $unacked │" -ForegroundColor White
        }
        Write-Host "└──────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow
    }
    catch {
        Write-Host "┌──────────────────────────────────────────────────────────────────────┐" -ForegroundColor Red
        Write-Host "│  ❌ RabbitMQ nao acessivel                                           │" -ForegroundColor Red
        Write-Host "└──────────────────────────────────────────────────────────────────────┘" -ForegroundColor Red
    }

    Write-Host "`n⏱️  Próxima atualizacao em 5 segundos...`n" -ForegroundColor Gray
}

function Send-PedidoSaga {
    $casos = @(
        # Caso sucesso
        @{
            clienteId = "CLI001"
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Pizza"
                    quantidade = 1
                    precoUnitario = 45.90
                }
            )
            enderecoEntrega = "Rua das Flores, 123"
            formaPagamento = "CREDITO"
        }
        # Caso falha (restaurante fechado)
        @{
            clienteId = "CLI001"
            restauranteId = "REST_FECHADO"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Hambúrguer"
                    quantidade = 1
                    precoUnitario = 28.50
                }
            )
            enderecoEntrega = "Av. Principal, 456"
            formaPagamento = "DEBITO"
        }
    )

    $caso = $casos | Get-Random
    $payload = $caso | ConvertTo-Json -Depth 10

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrlSaga/api/pedidos" -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

        if ($caso.restauranteId -eq "REST_FECHADO") {
            $stats.SagaPedidosFalha++
        } else {
            $stats.SagaPedidosSucesso++
        }
    }
    catch {
        $stats.SagaPedidosFalha++
    }
}

function Send-LancamentoFluxoCaixa {
    $tipos = @("Credito", "Debito")
    $categorias = @("Vendas", "Fornecedores", "Salarios", "Impostos")

    $tipo = $tipos | Get-Random
    $tipoEnum = if ($tipo -eq "Credito") { 2 } else { 1 }
    $valor = Get-Random -Minimum 50 -Maximum 1000
    $comerciante = "COM$(Get-Random -Minimum 1 -Maximum 5).ToString('000')"
    $categoria = $categorias | Get-Random

    $payload = @{
        tipo = $tipoEnum
        valor = $valor
        dataLancamento = (Get-Date -Format "yyyy-MM-dd")
        descricao = "Teste automatico - $tipo"
        comerciante = $comerciante
        categoria = $categoria
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrlFluxoCaixa/api/lancamentos" -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5
        $stats.FluxoCaixaLancamentos++
    }
    catch {
        $stats.FluxoCaixaErros++
    }
}

function Test-Connectivity {
    Write-Host "🔍 Verificando conectividade dos servicos...`n" -ForegroundColor Yellow

    # Testar SAGA
    try {
        Invoke-RestMethod -Uri "$BaseUrlSaga/health" -Method Get -TimeoutSec 5
        Write-Host "  SAGA API: OK" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ SAGA API: Offline" -ForegroundColor Red
        Write-Host "     Certifique-se de que a API esta rodando em $BaseUrlSaga" -ForegroundColor Red
        return $false
    }

    # Testar Fluxo de Caixa
    try {
        Invoke-RestMethod -Uri "$BaseUrlFluxoCaixa/health" -Method Get -TimeoutSec 5
        Write-Host "  Fluxo de Caixa API: OK" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ Fluxo de Caixa API: Offline" -ForegroundColor Red
        Write-Host "     Certifique-se de que a API esta rodando em $BaseUrlFluxoCaixa" -ForegroundColor Red
        return $false
    }

    # Testar RabbitMQ
    try {
        $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitMQUser}:${RabbitMQPass}"))
        $headers = @{ Authorization = "Basic $cred" }
        Invoke-RestMethod -Uri "$RabbitMQUrl/api/overview" -Headers $headers -Method Get -TimeoutSec 5
        Write-Host "  RabbitMQ: OK" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️  RabbitMQ: Offline (estatisticas de filas nao disponiveis)" -ForegroundColor Yellow
    }

    Write-Host "`n✅ Conectividade OK! Iniciando testes...`n" -ForegroundColor Green
    return $true
}

# ==================== EXECUÇÃO PRINCIPAL ====================

Clear-Host
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║          TESTE COMPLETO DO SISTEMA                             ║
║                                                                      ║
║  SAGA Pattern + Fluxo de Caixa                                       ║
║  Duracao: $DuracaoSegundos segundos                                  ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

if (-not (Test-Connectivity)) {
    Write-Host "`n❌ Alguns servicos estao offline. Corrija e tente novamente.`n" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

Write-Host "Iniciando geracao de carga em 3 segundos..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

$tempoFim = (Get-Date).AddSeconds($DuracaoSegundos)

# Job para atualizar dashboard
$dashboardJob = Start-Job -ScriptBlock {
    param($stats, $RabbitMQUrl, $RabbitMQUser, $RabbitMQPass)

    while ($true) {
        # Este job apenas mantém o loop, o dashboard é atualizado no processo principal
        Start-Sleep -Seconds 5
    }
} -ArgumentList $stats, $RabbitMQUrl, $RabbitMQUser, $RabbitMQPass

try {
    while ((Get-Date) -lt $tempoFim) {
        Write-Dashboard

        # Enviar requisicões em paralelo
        $jobs = @()

        # 2 pedidos SAGA
        $jobs += Start-Job -ScriptBlock {
            param($BaseUrl, $stats)
            & $using:Function:Send-PedidoSaga
        } -ArgumentList $BaseUrlSaga, $stats

        # 3 lancamentos Fluxo de Caixa
        for ($i = 0; $i -lt 3; $i++) {
            $jobs += Start-Job -ScriptBlock {
                param($BaseUrl, $stats)
                & $using:Function:Send-LancamentoFluxoCaixa
            } -ArgumentList $BaseUrlFluxoCaixa, $stats
        }

        # Aguardar conclusao
        $jobs | Wait-Job -Timeout 10 | Out-Null
        $jobs | Remove-Job -Force

        Start-Sleep -Seconds 5
    }
}
finally {
    Stop-Job $dashboardJob
    Remove-Job $dashboardJob -Force
}

# Dashboard final
Write-Dashboard

Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║                    TESTE CONCLUÍDO COM SUCESSO!                ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

📊 ESTATÍSTICAS FINAIS:

SAGA Pattern:
  - Pedidos processados: $($stats.SagaPedidosSucesso + $stats.SagaPedidosFalha)
  - Taxa de sucesso: $([math]::Round(($stats.SagaPedidosSucesso / [math]::Max($stats.SagaPedidosSucesso + $stats.SagaPedidosFalha, 1)) * 100, 2))%

Fluxo de Caixa:
  - Lancamentos criados: $($stats.FluxoCaixaLancamentos)
  - Taxa de sucesso: $([math]::Round(($stats.FluxoCaixaLancamentos / [math]::Max($stats.FluxoCaixaLancamentos + $stats.FluxoCaixaErros, 1)) * 100, 2))%

💡 Dicas:
  - Verifique os logs dos servicos para detalhes
  - Acesse RabbitMQ Management: $RabbitMQUrl
  - Consulte consolidados: GET $BaseUrlFluxoCaixa/api/consolidado/COM001/$(Get-Date -Format 'yyyy-MM-dd')

"@ -ForegroundColor Cyan
