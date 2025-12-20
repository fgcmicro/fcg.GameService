# Script rápido para limpar pods travados (sem confirmação)
# Uso: .\cleanup-pods-quick.ps1

$namespace = "games"

Write-Host "🧹 Limpando pods travados no namespace: $namespace" -ForegroundColor Cyan

# Remover pods em estados problemáticos
kubectl get pods -n $namespace -o json | ConvertFrom-Json | 
    ForEach-Object { $_.items } | 
    Where-Object { 
        $_.status.phase -in @("Failed", "Unknown") -or
        ($_.metadata.deletionTimestamp -and (New-TimeSpan -Start ([DateTime]::Parse($_.metadata.deletionTimestamp)) -End (Get-Date)).TotalMinutes -gt 5)
    } | 
    ForEach-Object { 
        Write-Host "Removendo pod: $($_.metadata.name)" -ForegroundColor Yellow
        kubectl delete pod $_.metadata.name -n $namespace --grace-period=0 --force 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            # Tentar remover finalizers
            $patch = '{"metadata":{"finalizers":null}}'
            kubectl patch pod $_.metadata.name -n $namespace -p $patch --type=merge 2>&1 | Out-Null
        }
    }

Write-Host "✅ Limpeza concluída!" -ForegroundColor Green
kubectl get pods -n $namespace
