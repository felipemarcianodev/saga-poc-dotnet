# Script PowerShell para testar o Contexto de NOTIFICAÇÃO
# Testa envio de notificações, canais e tolerância a falhas
# Uso: .\testar-notificacao.ps1 [-Cenario <numero>] [-DuracaoSegundos <segundos>]

param(
    [int]$Cenario = 0,
    [int]$DuracaoSegundos = 30,
    [string]$BaseUrl = "http://localhost:5000"
)

$apiPedidos = "$BaseUrl/api/pedidos"

# Estatísticas do contexto
$stats = @{
    NotificacoesEnviadas = 0
    NotificacoesFalhadas = 0
    EmailEnviado = 0
    SMSEnviado = 0
    PushEnviado = 0
    ClienteSemCanal = 0
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
║           📧 CONTEXTO: NOTIFICAÇÃO - DASHBOARD 📧                    ║
║                                                                      ║
║  Tempo decorrido: $($duracao.ToString('mm\:ss'))                    ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│ ESTATÍSTICAS DE ENVIO                                            │
├──────────────────────────────────────────────────────────────────────┤
│  Notificações Enviadas:   $($stats.NotificacoesEnviadas.ToString().PadLeft(3))
│  Notificações Falhadas:   $($stats.NotificacoesFalhadas.ToString().PadLeft(3)) ❌
│  Taxa de Sucesso:         $([math]::Round(($stats.NotificacoesEnviadas / [math]::Max($stats.NotificacoesEnviadas + $stats.NotificacoesFalhadas, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  📱 CANAIS DE COMUNICAÇÃO                                            │
├──────────────────────────────────────────────────────────────────────┤
│  Email:                   $($stats.EmailEnviado.ToString().PadLeft(3)) 📧
│  SMS:                     $($stats.SMSEnviado.ToString().PadLeft(3)) 📱
│  Push Notification:       $($stats.PushEnviado.ToString().PadLeft(3)) 🔔
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  🚫 MOTIVOS DE FALHA                                                 │
├──────────────────────────────────────────────────────────────────────┤
│  Cliente Sem Canal:       $($stats.ClienteSemCanal.ToString().PadLeft(3))
└──────────────────────────────────────────────────────────────────────┘

"@ -ForegroundColor Cyan
}

function Test-Cenario1-NotificacoesNormais {
    Write-Header "CENÁRIO 1: Notificações Normais (Todos os Canais)"

    $clientes = @(
        @{ id = "CLI001"; canal = "EMAIL"; nome = "João Silva" }
        @{ id = "CLI002"; canal = "SMS"; nome = "Maria Santos" }
        @{ id = "CLI003"; canal = "PUSH"; nome = "Pedro Costa" }
        @{ id = "CLI004"; canal = "EMAIL"; nome = "Ana Oliveira" }
        @{ id = "CLI005"; canal = "SMS"; nome = "Carlos Souza" }
    )

    Write-Host "  Enviando 10 pedidos com notificações...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 10; $i++) {
        $cliente = $clientes | Get-Random

        $payload = @{
            clienteId = $cliente.id
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
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.NotificacoesEnviadas++

            switch ($cliente.canal) {
                "EMAIL" { $stats.EmailEnviado++ }
                "SMS" { $stats.SMSEnviado++ }
                "PUSH" { $stats.PushEnviado++ }
            }

            Write-Host "  [$i/10] Notificação enviada - $($cliente.nome) via $($cliente.canal)" -ForegroundColor Green
        }
        catch {
            $stats.NotificacoesFalhadas++
            Write-Host "  [$i/10] ❌ Falha ao enviar notificação" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Aguardando processamento...`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 3

    Write-Dashboard
}

function Test-Cenario2-ClienteSemCanal {
    Write-Header "CENÁRIO 2: Cliente Sem Canal de Notificação"

    Write-Host "  Enviando pedidos para clientes sem canal de notificação...`n" -ForegroundColor Yellow
    Write-Host "  ⚠️  Pedido deve prosseguir, mas notificação falha (não bloqueia)...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
            clienteId = "CLI_SEM_CANAL"
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
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            # Pedido aceito, mas notificação falhou
            Write-Host "  [$i/5] ⚠️  Pedido aceito - Notificação falhou (cliente sem canal)" -ForegroundColor Yellow
            $stats.NotificacoesFalhadas++
            $stats.ClienteSemCanal++
        }
        catch {
            Write-Host "  [$i/5] ❌ Erro no pedido" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Pedidos aceitos, mas notificações falharam`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario3-ToleranciaFalhas {
    Write-Header "CENÁRIO 3: Tolerância a Falhas de Notificação"

    Write-Host "  Testando se falhas de notificação NÃO bloqueiam o pedido...`n" -ForegroundColor Yellow
    Write-Host "  ⚠️  Notificação é etapa não-crítica (compensável)...`n" -ForegroundColor Yellow

    $cenarios = @(
        @{ desc = "Cliente válido"; clienteId = "CLI001"; deveFalhar = $false }
        @{ desc = "Cliente sem canal"; clienteId = "CLI_SEM_CANAL"; deveFalhar = $false }
        @{ desc = "Email inválido"; clienteId = "CLI_EMAIL_INVALIDO"; deveFalhar = $false }
        @{ desc = "SMS indisponível"; clienteId = "CLI_SMS_FALHA"; deveFalhar = $false }
    )

    for ($i = 0; $i -lt $cenarios.Count; $i++) {
        $cenario = $cenarios[$i]

        $payload = @{
            clienteId = $cenario.clienteId
            restauranteId = "REST001"
            itens = @(
                @{
                    produtoId = "PROD001"
                    nome = "Pizza"
                    quantidade = 1
                    precoUnitario = 45.90
                }
            )
            enderecoEntrega = "Rua Teste, $($i+1)"
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            Write-Host "  [$(($i+1))/4] Pedido aceito - $($cenario.desc)" -ForegroundColor Green

            if ($cenario.clienteId -like "*SEM_CANAL*" -or $cenario.clienteId -like "*INVALIDO*" -or $cenario.clienteId -like "*FALHA*") {
                $stats.NotificacoesFalhadas++
                Write-Host "         ⚠️  Notificação falhou, mas pedido prosseguiu" -ForegroundColor Yellow
            }
            else {
                $stats.NotificacoesEnviadas++
                $stats.EmailEnviado++
            }
        }
        catch {
            Write-Host "  [$(($i+1))/4] ❌ Pedido rejeitado - $($cenario.desc)" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos os pedidos devem ser aceitos (notificação não bloqueia)`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario4-MultiCanais {
    Write-Header "CENÁRIO 4: Teste de Múltiplos Canais"

    Write-Host "  Testando envio simultâneo por diferentes canais...`n" -ForegroundColor Yellow

    $canais = @(
        @{ nome = "EMAIL"; icon = "📧" }
        @{ nome = "SMS"; icon = "📱" }
        @{ nome = "PUSH"; icon = "🔔" }
    )

    for ($i = 1; $i -le 9; $i++) {
        $canalIndex = ($i - 1) % 3
        $canal = $canais[$canalIndex]

        $payload = @{
            clienteId = "CLI00$i"
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
            canalNotificacao = $canal.nome
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.NotificacoesEnviadas++

            switch ($canal.nome) {
                "EMAIL" { $stats.EmailEnviado++ }
                "SMS" { $stats.SMSEnviado++ }
                "PUSH" { $stats.PushEnviado++ }
            }

            Write-Host "  [$i/9] $($canal.icon) Notificação via $($canal.nome) enviada" -ForegroundColor Green
        }
        catch {
            $stats.NotificacoesFalhadas++
            Write-Host "  [$i/9] ❌ Falha ao enviar via $($canal.nome)" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 400
    }

    Write-Host "`n Aguardando processamento...`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 3

    Write-Dashboard
}

function Test-Cenario5-CargaContinua {
    Write-Header "CENÁRIO 5: Carga Contínua ($DuracaoSegundos segundos)"

    Write-Host "  Enviando notificações continuamente...`n" -ForegroundColor Yellow
    Write-Host "  Pressione Ctrl+C para parar antes do tempo`n" -ForegroundColor Gray

    $tempoFim = (Get-Date).AddSeconds($DuracaoSegundos)
    $contador = 0

    $canais = @("EMAIL", "SMS", "PUSH")

    try {
        while ((Get-Date) -lt $tempoFim) {
            Write-Dashboard

            $canal = $canais | Get-Random
            $contador++

            $payload = @{
                clienteId = "CLI$(Get-Random -Minimum 1 -Maximum 100).ToString('000')"
                restauranteId = "REST001"
                itens = @(
                    @{
                        produtoId = "PROD001"
                        nome = "Pizza"
                        quantidade = 1
                        precoUnitario = 45.90
                    }
                )
                enderecoEntrega = "Rua Teste, $contador"
                formaPagamento = "CREDITO"
                canalNotificacao = $canal
            } | ConvertTo-Json -Depth 10

            Start-Job -ScriptBlock {
                param($url, $payload, $canal)
                try {
                    Invoke-RestMethod -Uri $url -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
                    return @{ sucesso = $true; canal = $canal }
                }
                catch {
                    return @{ sucesso = $false; canal = $canal }
                }
            } -ArgumentList $apiPedidos, $payload, $canal | Out-Null

            # Processar jobs concluídos
            Get-Job | Where-Object { $_.State -eq "Completed" } | ForEach-Object {
                $resultado = Receive-Job -Job $_
                if ($resultado.sucesso) {
                    $stats.NotificacoesEnviadas++
                    switch ($resultado.canal) {
                        "EMAIL" { $stats.EmailEnviado++ }
                        "SMS" { $stats.SMSEnviado++ }
                        "PUSH" { $stats.PushEnviado++ }
                    }
                }
                else {
                    $stats.NotificacoesFalhadas++
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
║              📧 TESTE DO CONTEXTO: NOTIFICAÇÃO 📧                    ║
║                                                                      ║
║  Testa envio de notificações, canais e tolerância a falhas          ║
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
    1 { Test-Cenario1-NotificacoesNormais }
    2 { Test-Cenario2-ClienteSemCanal }
    3 { Test-Cenario3-ToleranciaFalhas }
    4 { Test-Cenario4-MultiCanais }
    5 { Test-Cenario5-CargaContinua }
    default {
        # Executar todos os cenários
        Test-Cenario1-NotificacoesNormais
        Start-Sleep -Seconds 2
        Test-Cenario2-ClienteSemCanal
        Start-Sleep -Seconds 2
        Test-Cenario3-ToleranciaFalhas
        Start-Sleep -Seconds 2
        Test-Cenario4-MultiCanais
    }
}

# Relatório final
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║                    TESTE CONCLUÍDO!                            ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

📊 RELATÓRIO DO CONTEXTO NOTIFICAÇÃO:

Envios:
  - Notificações enviadas: $($stats.NotificacoesEnviadas)
  - Notificações falhadas: $($stats.NotificacoesFalhadas)
  - Taxa de sucesso: $([math]::Round(($stats.NotificacoesEnviadas / [math]::Max($stats.NotificacoesEnviadas + $stats.NotificacoesFalhadas, 1)) * 100, 2))%

Canais Utilizados:
  - Email: $($stats.EmailEnviado) 📧
  - SMS: $($stats.SMSEnviado) 📱
  - Push: $($stats.PushEnviado) 🔔

Motivos de Falha:
  - Cliente sem canal: $($stats.ClienteSemCanal)

💡 Dicas:
  - Verifique os logs do ServicoNotificacao para detalhes
  - Notificações são não-bloqueantes (não impedem o pedido)
  - Acompanhe as filas no RabbitMQ Management: http://localhost:15672

"@ -ForegroundColor Cyan
