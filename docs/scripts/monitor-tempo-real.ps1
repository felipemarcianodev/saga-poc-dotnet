# Monitor em Tempo Real do Sistema Completo
# Exibe dashboard atualizado a cada 2 segundos
# Uso: .\monitor-tempo-real.ps1 [-IntervalSegundos <segundos>]

param(
    [int]$IntervalSegundos = 2,
    [string]$BaseUrl = "http://localhost:5000",
    [string]$RabbitMQUrl = "http://localhost:15672",
    [string]$RabbitMQUser = "saga",
    [string]$RabbitMQPass = "saga123"
)

$ErrorActionPreference = "SilentlyContinue"

function Get-ColorByPercentage {
    param([decimal]$Percentage)
    if ($Percentage -ge 90) { return "Green" }
    if ($Percentage -ge 70) { return "Yellow" }
    return "Red"
}

function Get-BarGraph {
    param(
        [decimal]$Value,
        [decimal]$Max,
        [int]$Width = 20
    )

    $filled = [math]::Round(($Value / $Max) * $Width)
    $empty = $Width - $filled

    $bar = ("█" * $filled) + ("░" * $empty)
    return $bar
}

function Get-RabbitMQStats {
    try {
        $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitMQUser}:${RabbitMQPass}"))
        $headers = @{ Authorization = "Basic $cred" }

        $overview = Invoke-RestMethod -Uri "$RabbitMQUrl/api/overview" -Headers $headers -Method Get -TimeoutSec 3
        $queues = Invoke-RestMethod -Uri "$RabbitMQUrl/api/queues" -Headers $headers -Method Get -TimeoutSec 3

        return @{
            Connected = $true
            Overview = $overview
            Queues = $queues
        }
    }
    catch {
        return @{
            Connected = $false
        }
    }
}

function Get-APIHealth {
    param([string]$Url)

    try {
        $response = Invoke-RestMethod -Uri "$Url/health" -Method Get -TimeoutSec 3
        return @{
            Online = $true
            Status = $response.status
        }
    }
    catch {
        return @{
            Online = $false
        }
    }
}

function Show-Dashboard {
    $timestamp = Get-Date -Format "HH:mm:ss"

    Clear-Host

    Write-Host @"

╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║                  🔄 MONITOR EM TEMPO REAL - SISTEMA COMPLETO                 ║
║                                                                              ║
║  Atualizado às: $timestamp                                                   ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

    # ==================== APIs ====================
    Write-Host "┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Yellow
    Write-Host "│  🌐 APIS                                                                    │" -ForegroundColor Yellow
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Yellow

    $sagaHealth = Get-APIHealth -Url $BaseUrl
    $statusSaga = if ($sagaHealth.Online) { "✅ ONLINE " } else { "❌ OFFLINE" }
    $corSaga = if ($sagaHealth.Online) { "Green" } else { "Red" }
    Write-Host "│  SAGA Pattern API        " -NoNewline -ForegroundColor White
    Write-Host "$statusSaga" -NoNewline -ForegroundColor $corSaga
    Write-Host "  $BaseUrl" -ForegroundColor Gray
    Write-Host "│                                                                             │" -ForegroundColor Yellow

    $fluxoHealth = Get-APIHealth -Url $BaseUrl
    $statusFluxo = if ($fluxoHealth.Online) { "✅ ONLINE " } else { "❌ OFFLINE" }
    $corFluxo = if ($fluxoHealth.Online) { "Green" } else { "Red" }
    Write-Host "│  Fluxo de Caixa API      " -NoNewline -ForegroundColor White
    Write-Host "$statusFluxo" -NoNewline -ForegroundColor $corFluxo
    Write-Host "  $BaseUrl" -ForegroundColor Gray

    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow

    # ==================== RabbitMQ ====================
    $rabbitStats = Get-RabbitMQStats

    Write-Host "`n┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Magenta
    Write-Host "│  🐰 RABBITMQ                                                                │" -ForegroundColor Magenta
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Magenta

    if ($rabbitStats.Connected) {
        Write-Host "│  Status: " -NoNewline -ForegroundColor White
        Write-Host "✅ ONLINE" -ForegroundColor Green
        Write-Host "│  " -ForegroundColor Magenta

        $overview = $rabbitStats.Overview
        $connections = $overview.object_totals.connections
        $channels = $overview.object_totals.channels
        $queues = $overview.object_totals.queues

        Write-Host "│  Conexões:  $($connections.ToString().PadLeft(3))    Canais: $($channels.ToString().PadLeft(3))    Filas: $($queues.ToString().PadLeft(3))" -ForegroundColor White
        Write-Host "│  " -ForegroundColor Magenta

        # Filas do sistema
        Write-Host "│  FILAS DO SISTEMA:" -ForegroundColor Cyan
        Write-Host "│  " -ForegroundColor Magenta

        $filasRelevantes = $rabbitStats.Queues | Where-Object {
            $_.name -match "saga|fluxocaixa|orquestrador|lancamentos|consolidado"
        } | Sort-Object name

        foreach ($fila in $filasRelevantes) {
            $nome = $fila.name.Substring(0, [math]::Min(30, $fila.name.Length)).PadRight(30)
            $ready = $fila.messages_ready
            $unacked = $fila.messages_unacknowledged
            $total = $ready + $unacked

            $barReady = Get-BarGraph -Value $ready -Max ([math]::Max($total, 10)) -Width 15
            $barUnacked = Get-BarGraph -Value $unacked -Max ([math]::Max($total, 10)) -Width 15

            Write-Host "│    $nome" -ForegroundColor White
            Write-Host "│      Ready: " -NoNewline -ForegroundColor Gray
            Write-Host "$($ready.ToString().PadLeft(4)) " -NoNewline -ForegroundColor Green
            Write-Host "$barReady" -ForegroundColor Green
            Write-Host "│      Unack: " -NoNewline -ForegroundColor Gray
            Write-Host "$($unacked.ToString().PadLeft(4)) " -NoNewline -ForegroundColor Yellow
            Write-Host "$barUnacked" -ForegroundColor Yellow
            Write-Host "│  " -ForegroundColor Magenta
        }

        # Taxa de processamento
        $messageStats = $overview.message_stats
        if ($messageStats) {
            $publishRate = [math]::Round($messageStats.publish_details.rate, 2)
            $deliverRate = [math]::Round($messageStats.deliver_get_details.rate, 2)

            Write-Host "│  THROUGHPUT:" -ForegroundColor Cyan
            Write-Host "│    Publicação: " -NoNewline -ForegroundColor White
            Write-Host "$($publishRate.ToString('N2')) msg/s" -ForegroundColor Green
            Write-Host "│    Consumo:    " -NoNewline -ForegroundColor White
            Write-Host "$($deliverRate.ToString('N2')) msg/s" -ForegroundColor Green
        }
    }
    else {
        Write-Host "│  Status: " -NoNewline -ForegroundColor White
        Write-Host "❌ OFFLINE" -ForegroundColor Red
        Write-Host "│  " -ForegroundColor Magenta
        Write-Host "│  Não foi possível conectar ao RabbitMQ Management" -ForegroundColor Red
        Write-Host "│  URL: $RabbitMQUrl" -ForegroundColor Gray
    }

    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Magenta

    # ==================== Consolidado do Dia ====================
    if ($fluxoHealth.Online) {
        Write-Host "`n┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Green
        Write-Host "│  FLUXO DE CAIXA - CONSOLIDADO DO DIA                                     │" -ForegroundColor Green
        Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Green

        try {
            $data = Get-Date -Format "yyyy-MM-dd"
            $comerciantes = @("COM001", "COM002", "COM003")

            foreach ($com in $comerciantes) {
                try {
                    $consolidado = Invoke-RestMethod -Uri "$BaseUrl/api/consolidado/$com/$data" -Method Get -TimeoutSec 3

                    $saldoCor = if ($consolidado.saldoDiario -ge 0) { "Green" } else { "Red" }

                    Write-Host "│  " -ForegroundColor Green
                    Write-Host "│  $com - $data" -ForegroundColor Cyan
                    Write-Host "│    Créditos:  R$ " -NoNewline -ForegroundColor White
                    Write-Host "$($consolidado.totalCreditos.ToString('N2').PadLeft(12))" -NoNewline -ForegroundColor Green
                    Write-Host "  ($($consolidado.quantidadeCreditos) lançtos)" -ForegroundColor Gray
                    Write-Host "│    Débitos:   R$ " -NoNewline -ForegroundColor White
                    Write-Host "$($consolidado.totalDebitos.ToString('N2').PadLeft(12))" -NoNewline -ForegroundColor Red
                    Write-Host "  ($($consolidado.quantidadeDebitos) lançtos)" -ForegroundColor Gray
                    Write-Host "│    ─────────────────────────────────" -ForegroundColor Gray
                    Write-Host "│    Saldo:     R$ " -NoNewline -ForegroundColor White
                    Write-Host "$($consolidado.saldoDiario.ToString('N2').PadLeft(12))" -ForegroundColor $saldoCor
                }
                catch {
                    Write-Host "│  " -ForegroundColor Green
                    Write-Host "│  $com - Sem dados para hoje" -ForegroundColor Gray
                }
            }
        }
        catch {
            Write-Host "│  Erro ao consultar consolidados" -ForegroundColor Red
        }

        Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Green
    }

    # ==================== Instruções ====================
    Write-Host "`n" -NoNewline
    Write-Host "  💡 " -NoNewline -ForegroundColor Yellow
    Write-Host "Pressione " -NoNewline -ForegroundColor Gray
    Write-Host "Ctrl+C" -NoNewline -ForegroundColor White
    Write-Host " para sair  |  " -NoNewline -ForegroundColor Gray
    Write-Host "Próxima atualização em $IntervalSegundos segundos..." -ForegroundColor Gray
    Write-Host ""
}

# ==================== EXECUÇÃO PRINCIPAL ====================

Clear-Host
Write-Host @"

╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║                   🔄 INICIANDO MONITOR EM TEMPO REAL...                      ║
║                                                                              ║
║  Intervalo de atualização: $IntervalSegundos segundos                        ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

Write-Host "Verificando conectividade inicial...`n" -ForegroundColor Yellow
Start-Sleep -Seconds 2

# Loop principal
try {
    while ($true) {
        Show-Dashboard
        Start-Sleep -Seconds $IntervalSegundos
    }
}
catch {
    Write-Host "`n`n✅ Monitor encerrado pelo usuário.`n" -ForegroundColor Green
}
