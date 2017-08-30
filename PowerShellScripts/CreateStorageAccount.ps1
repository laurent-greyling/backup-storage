Param(
    [Parameter (mandatory = $true)]
    [string]$clientId,

    [Parameter (mandatory = $true)]
    [string]$tenantId,

    [Parameter (mandatory = $true)]
    [string]$storageName,

    [Parameter (mandatory = $true)]
    [string]$location
)

#Do this as sometimes you can get the error that credentials are expired or not added
Clear-AzureProfile –Force

Add-AzureAccount

#Get credentials need AAD credentials username=clientid and password=servicePrincipalPassword
$svcPrincipalCredentials = Get-Credential -UserName $clientId

Login-AzureRmAccount -Credential $svcPrincipalCredentials -ServicePrincipal -TenantId $tenantId

$storage = Get-AzureStorageAccount | where-object -FilterScript{$_.Name -eq $storageName}

if (!$storage)
{
    $result = New-AzureStorageAccount -StorageAccountName $storageName -Location $location
    if($result)  
    { 
      Write-Output "Resource Group - $($resourceGroupName) created" 
    } 
    else 
    { 
      Write-Output "Resource Group - $($resourceGroupName) not created $($result)" 
    } 
}