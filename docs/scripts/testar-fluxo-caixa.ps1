# Script PowerShell para testar o Sistema de Fluxo de Caixa
# Testa lancamentos, consolidado e visualiza resultados em tempo real
# Uso: .\testar-fluxo-caixa.ps1 [-Cenario <numero>] [-BaseUrl <url>]

param(
    [int]$Cenario = 0,
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$Verbose
)

$apiLancamentos = "$BaseUrl/api/lancamentos"
$apiConsolidado = "$BaseUrl/api/consolidado"

# Cores e simbolos
$symbols = @{
    success = "[OK]"
    error = "[ERRO]"
    warning = "[AVISO]"
    info = "[INFO]"
    money = "[$]"
    chart = "[CHART]"
    clock = "[TIME]"
    rocket = ">>>"
}

function Write-ColoredHeader {
    param([string]$Text)
    Write-Host "`n=================================================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "=================================================================`n" -ForegroundColor Cyan
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
        [string]$Categoria = "Geral",
        [string]$DataLancamento = ""
    )

    $tipoEnum = if ($Tipo -eq "Credito") { 2 } else { 1 }
    $data = if ($DataLancamento) { $DataLancamento } else { Get-Date -Format "yyyy-MM-dd" }

    $payload = @{
        tipo = $tipoEnum
        valor = $Valor
        dataLancamento = $data
        descricao = $Descricao
        comerciante = $Comerciante
        categoria = $Categoria
    } | ConvertTo-Json

    Write-Step "Registrando lancamento de $Tipo..."
    Write-Info "Comerciante: $Comerciante"
    Write-Money "Valor" $Valor
    Write-Info "Descricao: $Descricao"

    try {
        $response = Invoke-RestMethod -Uri $apiLancamentos -Method Post -Body $payload -ContentType "application/json"
        Write-Success "Lancamento registrado com sucesso!"
        Write-Info "Correlation ID: $($response.correlationId)"
        return $response
    }
    catch {
        Write-Error "Falha ao registrar lancamento"
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

        Write-Host "`n  +-----------------------------------------+" -ForegroundColor Cyan
        Write-Host "  |         CONSOLIDADO DIARIO              |" -ForegroundColor Cyan
        Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
        Write-Host "  | Data: $($response.data)                 |" -ForegroundColor White
        Write-Host "  | Comerciante: $($response.comerciante)   |" -ForegroundColor White
        Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
        Write-Host "  | Creditos:     R$ " -NoNewline -ForegroundColor White
        Write-Host "$($response.totalCreditos.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor Green
        Write-Host " |" -ForegroundColor White
        Write-Host "  | Debitos:      R$ " -NoNewline -ForegroundColor White
        Write-Host "$($response.totalDebitos.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor Red
        Write-Host " |" -ForegroundColor White
        Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
        Write-Host "  | Saldo Diario: R$ " -NoNewline -ForegroundColor Yellow
        $corSaldo = if ($response.saldoDiario -ge 0) { "Green" } else { "Red" }
        Write-Host "$($response.saldoDiario.ToString('N2').PadLeft(10))" -NoNewline -ForegroundColor $corSaldo
        Write-Host " |" -ForegroundColor Yellow
        Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
        Write-Host "  | Qtd Creditos:  " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeCreditos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Green
        Write-Host " |" -ForegroundColor White
        Write-Host "  | Qtd Debitos:   " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeDebitos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Red
        Write-Host " |" -ForegroundColor White
        Write-Host "  | Total Lanctos: " -NoNewline -ForegroundColor White
        Write-Host "$($response.quantidadeTotalLancamentos.ToString().PadLeft(16))" -NoNewline -ForegroundColor Cyan
        Write-Host " |" -ForegroundColor White
        Write-Host "  +-----------------------------------------+`n" -ForegroundColor Cyan

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

function Get-LancamentoPorId {
    param(
        [string]$LancamentoId
    )

    $url = "$apiLancamentos/$LancamentoId"

    Write-Step "Consultando lancamento por ID..."
    Write-Info "ID: $LancamentoId"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        Write-Success "Lancamento encontrado!"
        Write-Info "Tipo: $($response.tipo)"
        Write-Money "Valor" $response.valor
        Write-Info "Status: $($response.status)"
        Write-Info "Comerciante: $($response.comerciante)"
        Write-Info "Data: $($response.dataLancamento)"
        return $response
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Error "Lancamento nao encontrado"
        }
        else {
            Write-Error "Falha ao consultar lancamento"
            Write-Host "  Erro: $($_.Exception.Message)" -ForegroundColor Red
        }
        return $null
    }
}

function Get-LancamentosPorPeriodo {
    param(
        [string]$Comerciante,
        [string]$Inicio,
        [string]$Fim
    )

    $url = "$apiLancamentos`?comerciante=$Comerciante&inicio=$Inicio&fim=$Fim"

    Write-Step "Listando lancamentos por periodo..."
    Write-Info "Comerciante: $Comerciante"
    Write-Info "Periodo: $Inicio ate $Fim"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        $count = if ($response -is [Array]) { $response.Count } else { 1 }
        Write-Success "Encontrados $count lancamentos"

        if ($count -gt 0) {
            $totalCreditos = ($response | Where-Object { $_.tipo -eq "Credito" } | Measure-Object -Property valor -Sum).Sum
            $totalDebitos = ($response | Where-Object { $_.tipo -eq "Debito" } | Measure-Object -Property valor -Sum).Sum

            if (-not $totalCreditos) { $totalCreditos = 0 }
            if (-not $totalDebitos) { $totalDebitos = 0 }

            Write-Host "`n  Resumo do periodo:" -ForegroundColor Cyan
            Write-Money "  Total Creditos" $totalCreditos
            Write-Money "  Total Debitos" $totalDebitos
            Write-Host ""
        }

        return $response
    }
    catch {
        Write-Error "Falha ao listar lancamentos"
        Write-Host "  Erro: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Get-ConsolidadoPorPeriodo {
    param(
        [string]$Comerciante,
        [string]$Inicio,
        [string]$Fim
    )

    $url = "$apiConsolidado/$Comerciante/periodo?inicio=$Inicio&fim=$Fim"

    Write-Step "Consultando consolidado por periodo..."
    Write-Info "Comerciante: $Comerciante"
    Write-Info "Periodo: $Inicio ate $Fim"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        $count = if ($response -is [Array]) { $response.Count } else { 1 }
        Write-Success "Encontrados $count dias consolidados"

        if ($count -gt 0) {
            $somaCreditos = ($response | Measure-Object -Property totalCreditos -Sum).Sum
            $somaDebitos = ($response | Measure-Object -Property totalDebitos -Sum).Sum
            $somaSaldo = ($response | Measure-Object -Property saldoDiario -Sum).Sum

            Write-Host "`n  +-----------------------------------------+" -ForegroundColor Cyan
            Write-Host "  |      CONSOLIDADO DO PERIODO             |" -ForegroundColor Cyan
            Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
            Write-Host "  | Dias: $($count.ToString().PadLeft(32)) |" -ForegroundColor White
            Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
            Write-Host "  | Total Creditos: R$ " -NoNewline -ForegroundColor White
            Write-Host "$($somaCreditos.ToString('N2').PadLeft(15))" -NoNewline -ForegroundColor Green
            Write-Host " |" -ForegroundColor White
            Write-Host "  | Total Debitos:  R$ " -NoNewline -ForegroundColor White
            Write-Host "$($somaDebitos.ToString('N2').PadLeft(15))" -NoNewline -ForegroundColor Red
            Write-Host " |" -ForegroundColor White
            Write-Host "  +-----------------------------------------+" -ForegroundColor Cyan
            Write-Host "  | Saldo Periodo:  R$ " -NoNewline -ForegroundColor Yellow
            $corSaldo = if ($somaSaldo -ge 0) { "Green" } else { "Red" }
            Write-Host "$($somaSaldo.ToString('N2').PadLeft(15))" -NoNewline -ForegroundColor $corSaldo
            Write-Host " |" -ForegroundColor Yellow
            Write-Host "  +-----------------------------------------+`n" -ForegroundColor Cyan
        }

        return $response
    }
    catch {
        Write-Error "Falha ao consultar consolidado por periodo"
        Write-Host "  Erro: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Test-Cenario1-FluxoDiarioCompleto {
    Write-ColoredHeader "CENARIO 1: Fluxo Diario Completo (Happy Path)"

    $comerciante = "COM001"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Simulando um dia de operacões para $comerciante`n" -ForegroundColor White

    # Manha - Vendas
    Write-Host "`n[DIA] MANHÃ - VENDAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 150.00 -Descricao "Venda produto A" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 230.50 -Descricao "Venda produto B" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    # Tarde - Despesas
    Write-Host "`n[DIA] TARDE - DESPESAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Debito" -Valor 80.00 -Descricao "Compra de insumos" -Comerciante $comerciante -Categoria "Fornecedores"
    Start-Sleep -Seconds 2

    # Noite - Mais vendas
    Write-Host "`n[DIA] NOITE - VENDAS" -ForegroundColor Magenta
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 320.00 -Descricao "Venda produto C" -Comerciante $comerciante -Categoria "Vendas"
    Start-Sleep -Seconds 2

    # Consultar consolidado
    Write-Host "`n[CONSOLIDADO] CONSULTANDO CONSOLIDADO DO DIA" -ForegroundColor Magenta
    Write-Host "  Aguardando processamento dos eventos (10 segundos)..." -ForegroundColor Gray
    Start-Sleep -Seconds 10  # Aguardar processamento dos eventos
    Get-Consolidado -Comerciante $comerciante -Data $data

    Write-Host "`n  Esperado:" -ForegroundColor Yellow
    Write-Host "  - Total Creditos: R$ 700,50" -ForegroundColor Green
    Write-Host "  - Total Debitos:  R$ 80,00" -ForegroundColor Red
    Write-Host "  - Saldo Diario:   R$ 620,50" -ForegroundColor Green
}

function Test-Cenario2-AltaFrequencia {
    Write-ColoredHeader "CENARIO 2: Alta Frequencia de Lancamentos"

    $comerciante = "COM002"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Simulando 10 lancamentos rapidos...`n" -ForegroundColor White

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

        Write-Host "`n  Lancamento $i/10:" -ForegroundColor Cyan
        Invoke-RegistrarLancamento -Tipo $tipo -Valor $valor -Descricao "Lancamento teste #$i" -Comerciante $comerciante
        Start-Sleep -Milliseconds 500
    }

    Write-Host "`n[CONSOLIDADO] AGUARDANDO CONSOLIDACAO..." -ForegroundColor Magenta
    Write-Host "  Aguardando processamento dos eventos (10 segundos)..." -ForegroundColor Gray
    Start-Sleep -Seconds 10

    Get-Consolidado -Comerciante $comerciante -Data $data

    Write-Host "`n  Totais esperados:" -ForegroundColor Yellow
    Write-Host "  - Creditos: R$ $($totalCreditos.ToString('N2'))" -ForegroundColor Green
    Write-Host "  - Debitos:  R$ $($totalDebitos.ToString('N2'))" -ForegroundColor Red
}

function Test-Cenario3-CachePerformance {
    Write-ColoredHeader "CENARIO 3: Performance de Cache"

    $comerciante = "COM001"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Testando cache em 3 camadas (L1, L2, L3)...`n" -ForegroundColor White

    Write-Host "`n  1a Requisicao (MISS - consulta banco):" -ForegroundColor Yellow
    $tempo1 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n  2a Requisicao (HIT L1 - memory cache):" -ForegroundColor Yellow
    $tempo2 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n  3a Requisicao (HIT L1 - memory cache):" -ForegroundColor Yellow
    $tempo3 = Measure-Command { Get-Consolidado -Comerciante $comerciante -Data $data }

    Write-Host "`n ANALISE DE PERFORMANCE:" -ForegroundColor Magenta
    Write-Host "  +- 1a requisicao (DB):    $($tempo1.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Red
    Write-Host "  +- 2a requisicao (Cache): $($tempo2.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Green
    Write-Host "  +- 3a requisicao (Cache): $($tempo3.TotalMilliseconds.ToString('N0')) ms" -ForegroundColor Green

    $melhoria = [math]::Round((($tempo1.TotalMilliseconds - $tempo2.TotalMilliseconds) / $tempo1.TotalMilliseconds) * 100, 2)
    Write-Host "`n  Melhoria com cache: $melhoria%" -ForegroundColor Cyan
}

function Test-Cenario4-ValidacaoErros {
    Write-ColoredHeader "CENARIO 4: Validacao de Erros"

    Write-Host "  Testando validacoes e tratamento de erros...`n" -ForegroundColor White

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
        Write-Success "Validacao funcionou corretamente"
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
        Write-Success "Validacao funcionou corretamente"
        Write-Info "Erro esperado: $($_.Exception.Message)"
    }
}

function Test-Cenario5-ConsultaLancamentos {
    Write-ColoredHeader "CENARIO 5: Consulta de Lancamentos"

    $comerciante = "COM003"
    $data = Get-Date -Format "yyyy-MM-dd"

    Write-Host "  Testando endpoints de consulta de lancamentos...`n" -ForegroundColor White

    # Criar alguns lancamentos para testar
    Write-Host "`n  [SETUP] Criando lancamentos de teste..." -ForegroundColor Magenta

    $lancamento1 = Invoke-RegistrarLancamento -Tipo "Credito" -Valor 100.00 -Descricao "Venda teste 1" -Comerciante $comerciante
    Start-Sleep -Seconds 1

    $lancamento2 = Invoke-RegistrarLancamento -Tipo "Debito" -Valor 50.00 -Descricao "Compra teste 1" -Comerciante $comerciante
    Start-Sleep -Seconds 1

    $lancamento3 = Invoke-RegistrarLancamento -Tipo "Credito" -Valor 200.00 -Descricao "Venda teste 2" -Comerciante $comerciante
    Start-Sleep -Seconds 2

    # Teste 1: Consultar lancamento por ID
    Write-Host "`n  [TESTE 1] GET /api/lancamentos/{id}" -ForegroundColor Yellow

    # Primeiro listar para pegar um ID valido
    $url = "$apiLancamentos`?comerciante=$comerciante&inicio=$data&fim=$data"
    $lancamentos = Invoke-RestMethod -Uri $url -Method Get

    if ($lancamentos -and $lancamentos.Count -gt 0) {
        $primeiroId = $lancamentos[0].id
        Get-LancamentoPorId -LancamentoId $primeiroId
    }

    # Teste 2: Listar lancamentos por periodo
    Write-Host "`n  [TESTE 2] GET /api/lancamentos (listar por periodo)" -ForegroundColor Yellow
    Get-LancamentosPorPeriodo -Comerciante $comerciante -Inicio $data -Fim $data

    # Teste 3: ID inexistente (deve retornar 404)
    Write-Host "`n  [TESTE 3] Consultar ID inexistente (deve falhar)" -ForegroundColor Yellow
    $idInexistente = "00000000-0000-0000-0000-000000000000"
    Get-LancamentoPorId -LancamentoId $idInexistente
}

function Test-Cenario6-ConsolidadoPorPeriodo {
    Write-ColoredHeader "CENARIO 6: Consolidado por Periodo"

    $comerciante = "COM004"
    $hoje = Get-Date
    $data1 = $hoje.AddDays(-2).ToString("yyyy-MM-dd")
    $data2 = $hoje.AddDays(-1).ToString("yyyy-MM-dd")
    $data3 = $hoje.ToString("yyyy-MM-dd")

    Write-Host "  Testando consolidado de multiplos dias...`n" -ForegroundColor White

    # Criar lancamentos em diferentes dias
    Write-Host "`n  [SETUP] Criando lancamentos em 3 dias diferentes..." -ForegroundColor Magenta

    # Dia 1
    Write-Host "`n  Dia 1 ($data1):" -ForegroundColor Cyan
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 100.00 -Descricao "Venda dia 1" -Comerciante $comerciante -DataLancamento $data1
    Start-Sleep -Seconds 1
    Invoke-RegistrarLancamento -Tipo "Debito" -Valor 30.00 -Descricao "Despesa dia 1" -Comerciante $comerciante -DataLancamento $data1
    Start-Sleep -Seconds 1

    # Dia 2
    Write-Host "`n  Dia 2 ($data2):" -ForegroundColor Cyan
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 200.00 -Descricao "Venda dia 2" -Comerciante $comerciante -DataLancamento $data2
    Start-Sleep -Seconds 1
    Invoke-RegistrarLancamento -Tipo "Debito" -Valor 50.00 -Descricao "Despesa dia 2" -Comerciante $comerciante -DataLancamento $data2
    Start-Sleep -Seconds 1

    # Dia 3
    Write-Host "`n  Dia 3 ($data3):" -ForegroundColor Cyan
    Invoke-RegistrarLancamento -Tipo "Credito" -Valor 150.00 -Descricao "Venda dia 3" -Comerciante $comerciante -DataLancamento $data3
    Start-Sleep -Seconds 1
    Invoke-RegistrarLancamento -Tipo "Debito" -Valor 40.00 -Descricao "Despesa dia 3" -Comerciante $comerciante -DataLancamento $data3

    # Aguardar processamento
    Write-Host "`n  Aguardando processamento dos eventos (10 segundos)..." -ForegroundColor Gray
    Start-Sleep -Seconds 10

    # Teste: Consultar consolidado do periodo
    Write-Host "`n  [TESTE] GET /api/consolidado/{comerciante}/periodo" -ForegroundColor Yellow
    Get-ConsolidadoPorPeriodo -Comerciante $comerciante -Inicio $data1 -Fim $data3

    Write-Host "`n  Totais esperados (3 dias):" -ForegroundColor Yellow
    Write-Host "  - Total Creditos: R$ 450,00" -ForegroundColor Green
    Write-Host "  - Total Debitos:  R$ 120,00" -ForegroundColor Red
    Write-Host "  - Saldo Periodo:  R$ 330,00" -ForegroundColor Green
}

function Test-TodosOsCenarios {
    Write-ColoredHeader "EXECUTANDO TODOS OS CENARIOS DE TESTE"

    Test-Cenario1-FluxoDiarioCompleto
    Start-Sleep -Seconds 2

    Test-Cenario2-AltaFrequencia
    Start-Sleep -Seconds 2

    Test-Cenario3-CachePerformance
    Start-Sleep -Seconds 2

    Test-Cenario4-ValidacaoErros
    Start-Sleep -Seconds 2

    Test-Cenario5-ConsultaLancamentos
    Start-Sleep -Seconds 2

    Test-Cenario6-ConsolidadoPorPeriodo

    Write-ColoredHeader "TESTES CONCLUIDOS!"
    Write-Host "  $($symbols.success) Todos os cenarios foram executados" -ForegroundColor Green
}

# Execucao principal
Clear-Host
Write-Host @"

=================================================================

           TESTE DO SISTEMA DE FLUXO DE CAIXA

  Sistema: CQRS + Event-Driven
  API: $BaseUrl

=================================================================

"@ -ForegroundColor Cyan

# Verificar conectividade
Write-Step "Verificando conectividade com a API..."
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5
    Write-Success "API esta respondendo - Status: Healthy"
}
catch {
    Write-Error "API nao esta respondendo"
    Write-Host "  Certifique-se de que a API esta rodando em $BaseUrl" -ForegroundColor Red
    exit 1
}

# Executar cenario especifico ou todos
switch ($Cenario) {
    1 { Test-Cenario1-FluxoDiarioCompleto }
    2 { Test-Cenario2-AltaFrequencia }
    3 { Test-Cenario3-CachePerformance }
    4 { Test-Cenario4-ValidacaoErros }
    5 { Test-Cenario5-ConsultaLancamentos }
    6 { Test-Cenario6-ConsolidadoPorPeriodo }
    default { Test-TodosOsCenarios }
}

Write-Host "`n"
