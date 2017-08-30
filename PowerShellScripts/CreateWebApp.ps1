Param(
    [Parameter (mandatory = $true)]
    [string]$userName,

    [Parameter (mandatory = $true)]
    [string]$tenantId,

    [Parameter (mandatory = $true)]
    [string]$resourceGroupName,

    [Parameter (mandatory = $true)]
    [string]$servicePlanName,

    [Parameter (mandatory = $true)]
    [string]$webAppName,

    [Parameter (mandatory = $true)]
    [string]$location
)

#Do this as sometimes you can get the error that credentials are expired or not added
Clear-AzureProfile –Force

Add-AzureAccount

#Get credentials need AAD credentials username=clientid and password=servicePrincipalPassword
$svcPrincipalCredentials = Get-Credential -UserName $userName

Login-AzureRmAccount -Credential $svcPrincipalCredentials -ServicePrincipal -TenantId $tenantId


$resourceGroups = Get-AzureRmResourceGroup | where-object -FilterScript{$_.Name -eq $resourceGroupName}
$servicePlans = Get-AzureRmAppServicePlan | where-object -FilterScript{$_.Name -eq $servicePlanName}


if (!$resourceGroups) 
{
   $result = New-AzureRmResourceGroup -Name $resourceGroupName -Location $location
   if($result)  
   { 
      Write-Output "Resource Group - $($resourceGroupName) created" 
   } 
   else 
   { 
      Write-Output "Resource Group - $($resourceGroupName) not created $($result)" 
   } 
}

if (!$servicePlans) 
{
   $result = New-AzureRmAppServicePlan -Name $servicePlanName -Location $location -ResourceGroupName $resourceGroupName -Tier Standard -WorkerSize Small -NumberofWorkers 1
   if($result)  
   { 
     Write-Output "Service Plan - $($servicePlanName)  created" 
   } 
   else 
   { 
      Write-Output "Service Plan - $($servicePlanName)  not created $($result)" 
   }
}

$websites = Get-AzureWebsite | where-object -FilterScript{$_.Name -eq $webAppName}
if (!$websites) 
{ 
   $result = New-AzureRmWebApp -Name $webAppName -ResourceGroupName $resourceGroupName -AppServicePlan $servicePlanName -Location $location
   if($result)  
   { 
     Write-Output "WebApp - $($webAppName)  created" 
   } 
   else 
   { 
     Write-Output "WebApp - $($webAppName)  not created $($result)" 
   }
}