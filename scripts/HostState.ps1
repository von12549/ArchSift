# Hash-only host baseline shared by package/Linux verification; never emits environment values.
function Get-ArchSiftHostHash {
    $values=[ordered]@{}
    foreach($scope in @('User','Machine')) {
        $items=[Environment]::GetEnvironmentVariables($scope)
        foreach($key in @($items.Keys)|Sort-Object){$values["$scope/$key"]=[string]$items[$key]}
    }
    $values['process-path']=[Environment]::GetEnvironmentVariable('Path')
    foreach($name in @('AllUsersAllHosts','AllUsersCurrentHost','CurrentUserAllHosts','CurrentUserCurrentHost')){
        $path=[string]$PROFILE.$name
        $values[$name]=if([IO.File]::Exists($path)){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{'absent'}
    }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($values|ConvertTo-Json -Compress)))).ToLowerInvariant()
}
