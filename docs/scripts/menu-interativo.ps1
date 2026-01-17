# Menu Interativo para Testes do Sistema Completo
# Interface amigável para escolher e executar testes
# Uso: .\menu-interativo.ps1

$ErrorActionPreference = "SilentlyContinue"

function Show-Menu {
    Clear-Host
    Write-Host @"

╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║                     MENU INTERATIVO DE TESTES                             ║
║                                                                              ║
║              Sistema SAGA Pattern + Fluxo de Caixa (CQRS)                    ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

    Write-Host "┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Yellow
    Write-Host "│  SAGA PATTERN (Delivery de Comida)                                       │" -ForegroundColor Yellow
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Yellow
    Write-Host "│                                                                             │" -ForegroundColor Yellow
    Write-Host "│  [1] Testar Caso de Uso Específico (12 cenários)                           │" -ForegroundColor White
    Write-Host "│  [2] Testar Todos os Casos de Uso SAGA                                     │" -ForegroundColor White
    Write-Host "│                                                                             │" -ForegroundColor Yellow
    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow

    Write-Host ""

    Write-Host "┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Green
    Write-Host "│  FLUXO DE CAIXA (CQRS + Event-Driven)                                    │" -ForegroundColor Green
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Green
    Write-Host "│                                                                             │" -ForegroundColor Green
    Write-Host "│  [3] Cenário 1: Fluxo Diário Completo                                      │" -ForegroundColor White
    Write-Host "│  [4] Cenário 2: Alta Frequência de Lançamentos                             │" -ForegroundColor White
    Write-Host "│  [5] Cenário 3: Performance de Cache                                       │" -ForegroundColor White
    Write-Host "│  [6] Cenário 4: Validação de Erros                                         │" -ForegroundColor White
    Write-Host "│  [7] Todos os Cenários de Fluxo de Caixa                                   │" -ForegroundColor White
    Write-Host "│                                                                             │" -ForegroundColor Green
    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Green

    Write-Host ""

    Write-Host "┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Magenta
    Write-Host "│  SISTEMA COMPLETO                                                        │" -ForegroundColor Magenta
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Magenta
    Write-Host "│                                                                             │" -ForegroundColor Magenta
    Write-Host "│  [8] Teste de Carga - SAGA + Fluxo de Caixa (60 segundos)                  │" -ForegroundColor White
    Write-Host "│  [9] Monitor em Tempo Real (atualização a cada 2s)                         │" -ForegroundColor White
    Write-Host "│                                                                             │" -ForegroundColor Magenta
    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Magenta

    Write-Host ""

    Write-Host "┌─────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Cyan
    Write-Host "│  🛠️  UTILITÁRIOS                                                             │" -ForegroundColor Cyan
    Write-Host "├─────────────────────────────────────────────────────────────────────────────┤" -ForegroundColor Cyan
    Write-Host "│                                                                             │" -ForegroundColor Cyan
    Write-Host "│  [10] Verificar Saúde dos Serviços                                         │" -ForegroundColor White
    Write-Host "│  [11] Ver Estatísticas do RabbitMQ                                         │" -ForegroundColor White
    Write-Host "│  [12] Consultar Consolidado do Dia                                         │" -ForegroundColor White
    Write-Host "│                                                                             │" -ForegroundColor Cyan
    Write-Host "└─────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "  [0] Sair" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Escolha uma opção: " -NoNewline -ForegroundColor Yellow
}

function Test-SagaEspecifico {
    Write-Host ""
    Write-Host "  Casos de Uso Disponíveis:" -ForegroundColor Cyan
    Write-Host "  [1] Pedido Normal (Happy Path)" -ForegroundColor White
    Write-Host "  [2] Restaurante Fechado" -ForegroundColor White
    Write-Host "  [3] Item Indisponível" -ForegroundColor White
    Write-Host "  [4] Cartão Recusado" -ForegroundColor White
    Write-Host "  [5] Sem Entregador" -ForegroundColor White
    Write-Host "  [6] Cliente Sem Notificação" -ForegroundColor White
    Write-Host "  [7] Timeout no Pagamento" -ForegroundColor White
    Write-Host "  [8] Valor Muito Alto (Fraude)" -ForegroundColor White
    Write-Host "  [9] Endereço Fora de Área" -ForegroundColor White
    Write-Host "  [10] Pedido VIP" -ForegroundColor White
    Write-Host "  [11] Múltiplos Itens" -ForegroundColor White
    Write-Host "  [12] Pedido Complexo" -ForegroundColor White
    Write-Host ""
    Write-Host "  Número do caso (1-12): " -NoNewline -ForegroundColor Yellow
    $caso = Read-Host

    if ($caso -match '^\d+$' -and [int]$caso -ge 1 -and [int]$caso -le 12) {
        Write-Host ""
        & "$PSScriptRoot\testar-casos-de-uso.ps1" -CasoUso $caso
    }
    else {
        Write-Host "  ❌ Caso inválido!" -ForegroundColor Red
    }

    Pause
}

function Test-HealthCheck {
    Write-Host ""
    Write-Host "  🔍 Verificando saúde dos serviços...`n" -ForegroundColor Yellow

    $servicos = @{
        "SAGA API" = "http://localhost:5000/health"
        "Fluxo de Caixa API" = "http://localhost:5000/health"
        "RabbitMQ Management" = "http://localhost:15672"
    }

    foreach ($servico in $servicos.GetEnumerator()) {
        Write-Host "  Testando $($servico.Key)..." -NoNewline
        try {
            if ($servico.Key -eq "RabbitMQ Management") {
                $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("saga:saga123"))
                $headers = @{ Authorization = "Basic $cred" }
                Invoke-RestMethod -Uri "$($servico.Value)/api/overview" -Headers $headers -Method Get -TimeoutSec 5 | Out-Null
            }
            else {
                Invoke-RestMethod -Uri $servico.Value -Method Get -TimeoutSec 5 | Out-Null
            }
            Write-Host " ONLINE" -ForegroundColor Green
        }
        catch {
            Write-Host " ❌ OFFLINE" -ForegroundColor Red
        }
    }

    Write-Host ""
    Pause
}

function Show-RabbitMQStats {
    Write-Host ""
    Write-Host " Estatísticas do RabbitMQ...`n" -ForegroundColor Yellow

    try {
        $cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("saga:saga123"))
        $headers = @{ Authorization = "Basic $cred" }

        $overview = Invoke-RestMethod -Uri "http://localhost:15672/api/overview" -Headers $headers -Method Get
        $queues = Invoke-RestMethod -Uri "http://localhost:15672/api/queues" -Headers $headers -Method Get

        Write-Host "  Conexões: $($overview.object_totals.connections)" -ForegroundColor White
        Write-Host "  Canais: $($overview.object_totals.channels)" -ForegroundColor White
        Write-Host "  Filas: $($overview.object_totals.queues)" -ForegroundColor White
        Write-Host ""

        Write-Host "  Filas do Sistema:" -ForegroundColor Cyan
        foreach ($queue in $queues | Where-Object { $_.name -match "saga|fluxocaixa" } | Sort-Object name) {
            Write-Host "  - $($queue.name.PadRight(40)) Ready: $($queue.messages_ready.ToString().PadLeft(5))  Unacked: $($queue.messages_unacknowledged.ToString().PadLeft(5))" -ForegroundColor White
        }
    }
    catch {
        Write-Host "  ❌ Não foi possível conectar ao RabbitMQ" -ForegroundColor Red
    }

    Write-Host ""
    Pause
}

function Show-ConsolidadoDia {
    Write-Host ""
    Write-Host "  Comerciante (ex: COM001): " -NoNewline -ForegroundColor Yellow
    $comerciante = Read-Host

    if ([string]::IsNullOrWhiteSpace($comerciante)) {
        $comerciante = "COM001"
    }

    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host ""
    Write-Host " Consultando consolidado de $comerciante em $data...`n" -ForegroundColor Yellow

    try {
        $consolidado = Invoke-RestMethod -Uri "http://localhost:5000/api/consolidado/$comerciante/$data" -Method Get

        Write-Host "  ╭─────────────────────────────────────────╮" -ForegroundColor Cyan
        Write-Host "  │         CONSOLIDADO DIÁRIO              │" -ForegroundColor Cyan
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Data: $($consolidado.data)              │" -ForegroundColor White
        Write-Host "  │ Comerciante: $($consolidado.comerciante)│" -ForegroundColor White
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Créditos:     R$ " -NoNewline -ForegroundColor White
        Write-Host "$($consolidado.totalCreditos.ToString('N2').PadLeft(10))" -ForegroundColor Green
        Write-Host "  │ Débitos:      R$ " -NoNewline -ForegroundColor White
        Write-Host "$($consolidado.totalDebitos.ToString('N2').PadLeft(10))" -ForegroundColor Red
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Saldo Diário: R$ " -NoNewline -ForegroundColor Yellow
        $corSaldo = if ($consolidado.saldoDiario -ge 0) { "Green" } else { "Red" }
        Write-Host "$($consolidado.saldoDiario.ToString('N2').PadLeft(10))" -ForegroundColor $corSaldo
        Write-Host "  ╰─────────────────────────────────────────╯" -ForegroundColor Cyan
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Host "  ℹ️  Nenhum consolidado encontrado para esta data" -ForegroundColor Gray
        }
        else {
            Write-Host "  ❌ Erro ao consultar consolidado: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Pause
}

function Pause {
    Write-Host ""
    Write-Host "  Pressione qualquer tecla para continuar..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# ==================== LOOP PRINCIPAL ====================

while ($true) {
    Show-Menu
    $opcao = Read-Host

    switch ($opcao) {
        "1" { Test-SagaEspecifico }
        "2" {
            Write-Host ""
            & "$PSScriptRoot\testar-casos-de-uso.ps1"
            Pause
        }
        "3" {
            Write-Host ""
            & "$PSScriptRoot\testar-fluxo-caixa.ps1" -Cenario 1
            Pause
        }
        "4" {
            Write-Host ""
            & "$PSScriptRoot\testar-fluxo-caixa.ps1" -Cenario 2
            Pause
        }
        "5" {
            Write-Host ""
            & "$PSScriptRoot\testar-fluxo-caixa.ps1" -Cenario 3
            Pause
        }
        "6" {
            Write-Host ""
            & "$PSScriptRoot\testar-fluxo-caixa.ps1" -Cenario 4
            Pause
        }
        "7" {
            Write-Host ""
            & "$PSScriptRoot\testar-fluxo-caixa.ps1"
            Pause
        }
        "8" {
            Write-Host ""
            & "$PSScriptRoot\testar-sistema-completo.ps1" -DuracaoSegundos 60
            Pause
        }
        "9" {
            Write-Host ""
            & "$PSScriptRoot\monitor-tempo-real.ps1"
        }
        "10" { Test-HealthCheck }
        "11" { Show-RabbitMQStats }
        "12" { Show-ConsolidadoDia }
        "0" {
            Clear-Host
            Write-Host "`n  Até logo!`n" -ForegroundColor Green
            exit
        }
        default {
            Write-Host "`n  ❌ Opção inválida! Escolha um número entre 0 e 12.`n" -ForegroundColor Red
            Start-Sleep -Seconds 2
        }
    }
}
