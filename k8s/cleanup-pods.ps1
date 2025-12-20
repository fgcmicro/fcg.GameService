# Script para limpar pods travados (zumbis) no Kubernetes
# Uso: .\cleanup-pods.ps1 [-Namespace games] [-Force]

param(
    [string]$Namespace = "games",
    [switch]$Force
)

Write-Host "🧹 Iniciando limpeza de pods travados no namespace: $Namespace" -ForegroundColor Cyan

# Verificar se kubectl está disponível
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Host "❌ kubectl não encontrado. Por favor, instale o kubectl primeiro." -ForegroundColor Red
    exit 1
}

# Verificar se o namespace existe
$nsExists = kubectl get namespace $Namespace 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Namespace '$Namespace' não encontrado." -ForegroundColor Red
    exit 1
}

Write-Host "`n📊 Analisando pods no namespace '$Namespace'..." -ForegroundColor Yellow

# Listar pods em estados problemáticos
$problematicPods = kubectl get pods -n $Namespace -o json | ConvertFrom-Json | 
    Where-Object { 
        $_.items | Where-Object { 
            $_.status.phase -in @("Failed", "Unknown") -or
            $_.status.containerStatuses | Where-Object { 
                $_.state.waiting.reason -in @("CrashLoopBackOff", "ImagePullBackOff", "ErrImagePull") -or
                $_.state.terminated.reason -in @("Error", "OOMKilled")
            }
        }
    }

# Listar pods em estado Terminating há mais de 5 minutos
$terminatingPods = kubectl get pods -n $Namespace -o json | ConvertFrom-Json | 
    Where-Object { 
        $_.items | Where-Object { 
            $_.metadata.deletionTimestamp -and 
            (New-TimeSpan -Start ([DateTime]::Parse($_.metadata.deletionTimestamp)) -End (Get-Date)).TotalMinutes -gt 5
        }
    }

$allProblematicPods = @()

if ($problematicPods) {
    $allProblematicPods += $problematicPods.items | ForEach-Object { $_.metadata.name }
}

if ($terminatingPods) {
    $allProblematicPods += $terminatingPods.items | ForEach-Object { $_.metadata.name }
}

# Remover duplicatas
$allProblematicPods = $allProblematicPods | Select-Object -Unique

if ($allProblematicPods.Count -eq 0) {
    Write-Host "✅ Nenhum pod travado encontrado!" -ForegroundColor Green
    exit 0
}

Write-Host "`n⚠️  Encontrados $($allProblematicPods.Count) pod(s) travado(s):" -ForegroundColor Yellow
$allProblematicPods | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }

if (-not $Force) {
    $confirmation = Read-Host "`nDeseja forçar a exclusão desses pods? (s/N)"
    if ($confirmation -ne "s" -and $confirmation -ne "S") {
        Write-Host "❌ Operação cancelada." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "`n🗑️  Removendo pods travados..." -ForegroundColor Cyan

$successCount = 0
$failCount = 0

foreach ($podName in $allProblematicPods) {
    Write-Host "   Removendo pod: $podName" -ForegroundColor Gray
    
    # Tentar deletar normalmente primeiro
    kubectl delete pod $podName -n $Namespace --grace-period=0 --force 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Pod $podName removido com sucesso" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "   ⚠️  Pod $podName ainda está travado, tentando remoção forçada..." -ForegroundColor Yellow
        
        # Remoção forçada usando patch
        $patch = '{"metadata":{"finalizers":null}}'
        kubectl patch pod $podName -n $Namespace -p $patch --type=merge 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Pod $podName removido com sucesso (forçado)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "   ❌ Falha ao remover pod $podName" -ForegroundColor Red
            $failCount++
        }
    }
    
    Start-Sleep -Seconds 1
}

Write-Host "`n📊 Resumo da limpeza:" -ForegroundColor Cyan
Write-Host "   ✅ Removidos com sucesso: $successCount" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "   ❌ Falhas: $failCount" -ForegroundColor Red
}

Write-Host "`n✅ Limpeza concluída!" -ForegroundColor Green

# Mostrar status atual dos pods
Write-Host "`n📋 Status atual dos pods:" -ForegroundColor Cyan
kubectl get pods -n $Namespace
