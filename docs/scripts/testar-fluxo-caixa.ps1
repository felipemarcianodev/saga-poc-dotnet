# Script PowerShell para testar o Sistema de Fluxo de Caixa
# Testa lançamentos, consolidado e visualiza resultados em tempo real
# Uso: .\testar-fluxo-caixa.ps1 [-Cenario <numero>] [-BaseUrl <url>]

param(
    [int]$Cenario = 0,
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$Verbose
)

$apiLancamentos = "$BaseUrl/api/lancamentos"
$apiConsolidado = "$BaseUrl/api/consolidado"

# Cores e símbolos
$symbols = @{
    success = "✅"
    error = "❌"
    warning = "⚠️"
    info = "ℹ️"
    money = "💰"
    chart = "📊"
    clock = "⏱️"
    rocket = "🚀"
}

function Write-ColoredHeader {
    param([string]$Text)
    Write-Host "`n╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $Text" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Text)
    Write-Host "  $($symbols.rocket) $Text" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Text)
    Write-Host "  $($symbols.success) $Text" -ForegroundColor Green
}

function Write-Error {
    param([string]$Text)
    Write-Host "  $($symbols.error) $Text" -ForegroundColor Red
}

function Write-Info {
    param([string]$Text)
    Write-Host "  $($symbols.info) $Text" -ForegroundColor Gray
}

function Write-Money {
    param([string]$Text, [decimal]$Valor)
    $cor = if ($Valor -ge 0) { "Green" } else { "Red" }
    Write-Host "  $($symbols.money) $Text R$ " -NoNewline -ForegroundColor Yellow
    Write-Host "$($Valor.ToString('N2'))" -ForegroundColor $cor
}

function Invoke-RegistrarLancamento {
    param(
        [string]$Tipo,
        [decimal]$Valor,
        [string]$Descricao,
        [string]$Comerciante,
        [string]$Categoria = "Geral"
    )

    $tipoEnum = if ($Tipo -eq "Credito") { 2 } else { 1 }
    $data = Get-Date -Format "yyyy-MM-dd"

    $payload = @{
        tipo = $tipoEnum
        valor = $Valor
        dataLancamento = $data
        descricao = $Descricao
        comerciante = $Comerciante
        categoria = $Categoria
    } | ConvertTo-Json

    Write-Step "Registrando lançamento de $Tipo..."
    Write-Info "Comerciante: $Comerciante"
    Write-Money "Valor" $Valor
    Write-Info "Descrição: $Descricao"

    try {
        $response = Invoke-RestMethod -Uri $apiLancamentos -Method Post -Body $payload -ContentType "application/json"
        Write-Success "Lançamento registrado com sucesso!"
        Write-Info "Correlation ID: $($response.correlationId)"
        return $response
    }
    catch {
        Write-Error "Falha ao registrar lançamento"
        Write-Host "  Erro: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Get-Consolidado {
    param(
        [string]$Comerciante,
        [string]$Data
    )

    $url = "$apiConsolidado/$Comerciante/$Data"

    Write-Step "Consultando consolidado..."
    Write-Info "Comerciante: $Comerciante"
    Write-Info "Data: $Data"

    try {
        $inicio = Get-Date
        $response = Invoke-RestMethod -Uri $url -Method Get
        $duracao = (Get-Date) - $inicio

        Write-Success "Consolidado obtido com sucesso!"
        Write-Host "  $($symbols.clock) Tempo de resposta: $($duracao.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Magenta

        Write-Host "`n  ╭─────────────────────────────────────────╮" -ForegroundColor Cyan
        Write-Host "  │         CONSOLIDADO DIÁRIO              │" -ForegroundColor Cyan
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Data: $($response.data)                 │" -ForegroundColor White
        Write-Host "  │ Comerciante: $($response.comerciante)   │" -ForegroundColor White
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Créditos:     R$ " -NoNewline -ForegroundColor White
        Write-Host "$($response.totalCreditos.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor Green
        Write-Host " │" -ForegroundColor White
        Write-Host "  │ Débitos:      R$ " -NoNewline -ForegroundColor White
        Write-Host "$($response.totalDebitos.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor Red
        Write-Host " │" -ForegroundColor White
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Saldo Diário: R$ " -NoNewline -ForegroundColor Yellow
        $corSaldo = if ($response.saldoDiario -ge 0) { "Green" } else { "Red" }
        Write-Host "$($response.saldoDiario.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor $corSaldo
        Write-Host " │" -ForegroundColor Yellow
        Write-Host "  ├─────────────────────────────────────────┤" -ForegroundColor Cyan
        Write-Host "  │ Qtd Créditos:  " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeCreditos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Green
        Write-Host " │" -ForegroundColor White
        Write-Host "  │ Qtd Débitos:   " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeDebitos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Red
        Write-Host " │" -ForegroundColor White
        Write-Host "  │ Total Lançtos: " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeTotalLancamentos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Cyan
        Write-Host " │" -ForegroundColor White
        Write-Host "  ╰─────────────────────────────────────────╯`n" -ForegroundColor Cyan

        return $response
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Info "Nenhum consolidado encontrado para esta data"
        }
        else {
            Write-Error "Falha ao consultar consolidado"
            Write-Host "  Erro: $($_.Exception.Message)" -ForegroundColor Red
        }
        return $null
    }
}

function Test-Cenario1-FluxoDiarioCompleto {
    Write-ColoredHeader "CENÁRIO 1: Fluxo Diário Completo (Happy Path)"

    $comerciante = "COM001"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Simulando um dia de operações para $comerciante`n" -ForegroundColor White

    # Manhã - Vendas
    Write-Host "`n📅 MANHÃ - VENDAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 150.00 -Descricao "Venda produto A" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 230.50 -Descricao "Venda produto B" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    # Tarde - Despesas
    Write-Host "`n📅 TARDE - DESPESAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Debito" -Valor 80.00 -Descricao "Compra de insumos" -Comerciante $comerciante -Categoria "Fornecedores"
    Start-Sleep -Seconds 2

    # Noite - Mais vendas
    Write-Host "`n📅 NOITE - VENDAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 320.00 -Descricao "Venda produto C" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    # Consultar consolidado
    Write-Host "`n📊 CONSULTANDO CONSOLIDADO DO DIA" -ForegroundColor Magenta
    Start-Sleep -Seconds 3  # Aguardar processamento dos eventos
    Get-Consolidado -Comerciante $comerciante -Data $data

    Write-Host "`n  Esperado:" -ForegroundColor Yellow
    Write-Host "  - Total Créditos: R$ 700,50" -ForegroundColor Green
    Write-Host "  - Total Débitos:  R$ 80,00" -ForegroundColor Red
    Write-Host "  - Saldo Diário:   R$ 620,50" -ForegroundColor Green
}

function Test-Cenario2-AltaFrequencia {
    Write-ColoredHeader "CENÁRIO 2: Alta Frequência de Lançamentos"

    $comerciante = "COM002"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Simulando 10 lançamentos rápidos...`n" -ForegroundColor White

    $totalCreditos = 0
    $totalDebitos = 0

    for ($i = 1; $i -le 10; $i++) {
        $tipo = if ($i % 3 -eq 0) { "Debito" } else { "Credito" }
        $valor = Get-Random -Minimum 50 -Maximum 500

        if ($tipo -eq "Credito") {
            $totalCreditos += $valor
        } else {
            $totalDebitos += $valor
        }

        Write-Host "`n  Lançamento $i/10:" -ForegroundColor Cyan
        Invoke-RegistrarLancamento -Tipo $tipo -Valor $valor -Descricao "Lançamento teste #$i" -Comerciante $comerciante
        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n📊 AGUARDANDO CONSOLIDAÇÃO..." -ForegroundColor Magenta
    Start-Sleep -Seconds 5

    Get-Consolidado -Comerciante $comerciante -Data $data

    Write-Host "`n  Totais esperados:" -ForegroundColor Yellow
    Write-Host "  - Créditos: R$ $($totalCreditos.ToString('N2'))" -ForegroundColor Green
    Write-Host "  - Débitos:  R$ $($totalDebitos.ToString('N2'))" -ForegroundColor Red
}

function Test-Cenario3-CachePerformance {
    Write-ColoredHeader "CENÁRIO 3: Performance de Cache"

    $comerciante = "COM001"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Testando cache em 3 camadas (L1, L2, L3)...`n" -ForegroundColor White

    Write-Host "`n  1ª Requisição (MISS - consulta banco):" -ForegroundColor Yellow
    $tempo1 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n  2ª Requisição (HIT L1 - memory cache):" -ForegroundColor Yellow
    $tempo2 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n  3ª Requisição (HIT L1 - memory cache):" -ForegroundColor Yellow
    $tempo3 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n ANÁLISE DE PERFORMANCE:" -ForegroundColor Magenta
    Write-Host "  ├─ 1ª requisição (DB):    $($tempo1.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Red
    Write-Host "  ├─ 2ª requisição (Cache): $($tempo2.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Green
    Write-Host "  └─ 3ª requisição (Cache): $($tempo3.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Green

    $melhoria = [math]::Round((($tempo1.TotalMilliseconds - $tempo2.TotalMilliseconds) / $tempo1.TotalMilliseconds) * 100, 2)
    Write-Host "`n  Melhoria com cache: $melhoria%" -ForegroundColor Cyan
}

function Test-Cenario4-ValidacaoErros {
    Write-ColoredHeader "CENÁRIO 4: Validação de Erros"

    Write-Host "  Testando validações e tratamento de erros...`n" -ForegroundColor White

    # Teste 1: Valor negativo
    Write-Host "`n  Teste 1: Valor negativo (deve falhar)" -ForegroundColor Yellow
    $payload = @{
        tipo = 2
        valor = -100.00
        dataLancamento = (Get-Date -Format "yyyy-MM-dd")
        descricao = "Teste valor negativo"
        comerciante = "COM001"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri $apiLancamentos -Method Post -Body $payload -ContentType "application/json"
        Write-Error "Deveria ter falhado!"
    }
    catch {
        Write-Success "Validação funcionou corretamente"
        Write-Info "Erro esperado: $($_.Exception.Message)"
    }

    # Teste 2: Comerciante vazio
    Write-Host "`n  Teste 2: Comerciante vazio (deve falhar)" -ForegroundColor Yellow
    $payload = @{
        tipo = 2
        valor = 100.00
        dataLancamento = (Get-Date -Format "yyyy-MM-dd")
        descricao = "Teste comerciante vazio"
        comerciante = ""
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri $apiLancamentos -Method Post -Body $payload -ContentType "application/json"
        Write-Error "Deveria ter falhado!"
    }
    catch {
        Write-Success "Validação funcionou corretamente"
        Write-Info "Erro esperado: $($_.Exception.Message)"
    }
}

function Test-TodosOsCenarios {
    Write-ColoredHeader "EXECUTANDO TODOS OS CENÁRIOS DE TESTE"

    Test-Cenario1-FluxoDiarioCompleto
    Start-Sleep -Seconds 2

    Test-Cenario2-AltaFrequencia
    Start-Sleep -Seconds 2

    Test-Cenario3-CachePerformance
    Start-Sleep -Seconds 2

    Test-Cenario4-ValidacaoErros

    Write-ColoredHeader "TESTES CONCLUÍDOS!"
    Write-Host "  $($symbols.success) Todos os cenários foram executados" -ForegroundColor Green
}

# Execução principal
Clear-Host
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║           TESTE DO SISTEMA DE FLUXO DE CAIXA                  ║
║                                                               ║
║  Sistema: CQRS + Event-Driven                                 ║
║  API: $BaseUrl                                                ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Verificar conectividade
Write-Step "Verificando conectividade com a API..."
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5
    Write-Success "API está respondendo"
}
catch {
    Write-Error "API não está respondendo"
    Write-Host "  Certifique-se de que a API está rodando em $BaseUrl" -ForegroundColor Red
    exit 1
}

# Executar cenário específico ou todos
switch ($Cenario) {
    1 { Test-Cenario1-FluxoDiarioCompleto }
    2 { Test-Cenario2-AltaFrequencia }
    3 { Test-Cenario3-CachePerformance }
    4 { Test-Cenario4-ValidacaoErros }
    default { Test-TodosOsCenarios }
}

Write-Host "`n"
