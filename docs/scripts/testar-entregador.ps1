# Script PowerShell para testar o Contexto de ENTREGADOR
# Testa alocação de entregadores, disponibilidade e cancelamentos
# Uso: .\testar-entregador.ps1 [-Cenario <numero>] [-DuracaoSegundos <segundos>]

param(
    [int]$Cenario = 0,
    [int]$DuracaoSegundos = 30,
    [string]$BaseUrl = "http://localhost:5000"
)

$apiPedidos = "$BaseUrl/api/pedidos"

# Estatísticas do contexto
$stats = @{
    EntregadoresAlocados = 0
    AlocacoesFalhadas = 0
    EntregadorIndisponivel = 0
    ForaDeArea = 0
    ValorFretes = 0
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
║           🚴 CONTEXTO: ENTREGADOR - DASHBOARD 🚴                     ║
║                                                                      ║
║  Tempo decorrido: $($duracao.ToString('mm\:ss'))                    ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│ ESTATÍSTICAS DE ALOCAÇÃO                                         │
├──────────────────────────────────────────────────────────────────────┤
│  Entregadores Alocados:   $($stats.EntregadoresAlocados.ToString().PadLeft(3))
│  Alocações Falhadas:      $($stats.AlocacoesFalhadas.ToString().PadLeft(3)) ❌
│  Taxa de Alocação:        $([math]::Round(($stats.EntregadoresAlocados / [math]::Max($stats.EntregadoresAlocados + $stats.AlocacoesFalhadas, 1)) * 100, 2))%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  🚫 MOTIVOS DE FALHA                                                 │
├──────────────────────────────────────────────────────────────────────┤
│  Entregador Indisponível: $($stats.EntregadorIndisponivel.ToString().PadLeft(3))
│  Fora de Área:            $($stats.ForaDeArea.ToString().PadLeft(3))
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  VALORES DE FRETE                                                 │
├──────────────────────────────────────────────────────────────────────┤
│  Valor Total Fretes:      R$ $($stats.ValorFretes.ToString('N2'))
│  Frete Médio:             R$ $([math]::Round($stats.ValorFretes / [math]::Max($stats.EntregadoresAlocados, 1), 2).ToString('N2'))
└──────────────────────────────────────────────────────────────────────┘

"@ -ForegroundColor Cyan
}

function Test-Cenario1-EntregadoresDisponiveis {
    Write-Header "CENÁRIO 1: Entregadores Disponíveis (Alocação Total)"

    $zonas = @("NORTE", "SUL", "LESTE", "OESTE", "CENTRO")
    $enderecos = @(
        @{ zona = "NORTE"; endereco = "Rua das Palmeiras, 123" }
        @{ zona = "SUL"; endereco = "Av. das Acácias, 456" }
        @{ zona = "LESTE"; endereco = "Rua do Sol, 789" }
        @{ zona = "OESTE"; endereco = "Av. da Lua, 321" }
        @{ zona = "CENTRO"; endereco = "Praça Central, 100" }
    )

    Write-Host "  Enviando 10 pedidos com entregadores disponíveis...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 10; $i++) {
        $local = $enderecos | Get-Random
        $frete = Get-Random -Minimum 5 -Maximum 15

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
            enderecoEntrega = $local.endereco
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            $response = Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5

            $stats.EntregadoresAlocados++
            $stats.ValorFretes += $frete

            Write-Host "  [$i/10] Entregador alocado - Zona: $($local.zona) - Frete: R$ $($frete.ToString('N2'))" -ForegroundColor Green
        }
        catch {
            $stats.AlocacoesFalhadas++
            Write-Host "  [$i/10] ❌ Falha ao alocar entregador" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Aguardando processamento...`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 3

    Write-Dashboard
}

function Test-Cenario2-EntregadorIndisponivel {
    Write-Header "CENÁRIO 2: Sem Entregador Disponível (Falha Esperada)"

    Write-Host "  Enviando pedidos quando não há entregadores disponíveis...`n" -ForegroundColor Yellow

    for ($i = 1; $i -le 5; $i++) {
        $payload = @{
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
            enderecoEntrega = "Rua Sem Cobertura, 999"
            formaPagamento = "CREDITO"
            forcarSemEntregador = $true
        } | ConvertTo-Json -Depth 10

        try {
            Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
            $stats.EntregadoresAlocados++
        }
        catch {
            $stats.AlocacoesFalhadas++
            $stats.EntregadorIndisponivel++
            Write-Host "  [$i/5] ❌ Falha: Nenhum entregador disponível" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos devem falhar por falta de entregador`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario3-ForaDeArea {
    Write-Header "CENÁRIO 3: Endereço Fora de Área (Falha Esperada)"

    Write-Host "  Enviando pedidos para endereços fora da área de entrega...`n" -ForegroundColor Yellow

    $enderecosForaDeArea = @(
        "Rua Muito Longe, 9999 - Cidade Distante",
        "Av. Impossível, 8888 - Estado Remoto",
        "Travessa Inacessível, 7777 - Interior",
        "Rodovia BR-000, Km 500",
        "Ilha Isolada, 1111"
    )

    for ($i = 1; $i -le 5; $i++) {
        $endereco = $enderecosForaDeArea | Get-Random

        $payload = @{
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
            enderecoEntrega = $endereco
            formaPagamento = "CREDITO"
        } | ConvertTo-Json -Depth 10

        try {
            Invoke-RestMethod -Uri $apiPedidos -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 | Out-Null
            $stats.EntregadoresAlocados++
        }
        catch {
            $stats.AlocacoesFalhadas++
            $stats.ForaDeArea++
            Write-Host "  [$i/5] ❌ Falha: Endereço fora de área - $endereco" -ForegroundColor Red
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n Resultado: Todos devem falhar por endereço fora de área`n" -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    Write-Dashboard
}

function Test-Cenario4-CargaContinua {
    Write-Header "CENÁRIO 4: Carga Contínua ($DuracaoSegundos segundos)"

    Write-Host "  Enviando pedidos continuamente...`n" -ForegroundColor Yellow
    Write-Host "  Pressione Ctrl+C para parar antes do tempo`n" -ForegroundColor Gray

    $tempoFim = (Get-Date).AddSeconds($DuracaoSegundos)
    $contador = 0

    $cenarios = @(
        @{ zona = "NORTE"; endereco = "Rua Norte, 100"; valido = $true }
        @{ zona = "SUL"; endereco = "Rua Sul, 200"; valido = $true }
        @{ zona = "LESTE"; endereco = "Rua Leste, 300"; valido = $true }
        @{ zona = "FORA"; endereco = "Rua Muito Longe, 9999"; valido = $false }
    )

    try {
        while ((Get-Date) -lt $tempoFim) {
            Write-Dashboard

            $cenario = $cenarios | Get-Random
            $contador++
            $frete = Get-Random -Minimum 5 -Maximum 15

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
                enderecoEntrega = $cenario.endereco
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

            # Processar jobs concluídos
            Get-Job | Where-Object { $_.State -eq "Completed" } | ForEach-Object {
                $resultado = Receive-Job -Job $_
                if ($resultado -and $cenario.valido) {
                    $stats.EntregadoresAlocados++
                    $stats.ValorFretes += $frete
                }
                else {
                    $stats.AlocacoesFalhadas++
                    if ($cenario.zona -eq "FORA") {
                        $stats.ForaDeArea++
                    }
                    else {
                        $stats.EntregadorIndisponivel++
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
║              🚴 TESTE DO CONTEXTO: ENTREGADOR 🚴                     ║
║                                                                      ║
║  Testa alocação de entregadores, disponibilidade e cobertura        ║
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
    1 { Test-Cenario1-EntregadoresDisponiveis }
    2 { Test-Cenario2-EntregadorIndisponivel }
    3 { Test-Cenario3-ForaDeArea }
    4 { Test-Cenario4-CargaContinua }
    default {
        # Executar todos os cenários
        Test-Cenario1-EntregadoresDisponiveis
        Start-Sleep -Seconds 2
        Test-Cenario2-EntregadorIndisponivel
        Start-Sleep -Seconds 2
        Test-Cenario3-ForaDeArea
    }
}

# Relatório final
Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                                                                      ║
║                    TESTE CONCLUÍDO!                            ║
║                                                                      ║
╚══════════════════════════════════════════════════════════════════════╝

📊 RELATÓRIO DO CONTEXTO ENTREGADOR:

Alocações:
  - Entregadores alocados: $($stats.EntregadoresAlocados)
  - Alocações falhadas: $($stats.AlocacoesFalhadas)
  - Taxa de alocação: $([math]::Round(($stats.EntregadoresAlocados / [math]::Max($stats.EntregadoresAlocados + $stats.AlocacoesFalhadas, 1)) * 100, 2))%

Motivos de Falha:
  - Entregador indisponível: $($stats.EntregadorIndisponivel)
  - Fora de área: $($stats.ForaDeArea)

Valores:
  - Valor total de fretes: R$ $($stats.ValorFretes.ToString('N2'))
  - Frete médio: R$ $([math]::Round($stats.ValorFretes / [math]::Max($stats.EntregadoresAlocados, 1), 2).ToString('N2'))

💡 Dicas:
  - Verifique os logs do ServicoEntregador para detalhes
  - Acompanhe as filas no RabbitMQ Management: http://localhost:15672

"@ -ForegroundColor Cyan
