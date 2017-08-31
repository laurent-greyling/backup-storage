function Create-ServicePrincipal
{
    Param(
        [Parameter (mandatory = $true)]
        [string]$displayName,

        [Parameter (mandatory = $true)]
        [string]$homePage,

        [Parameter (mandatory = $true)]
        [string]$appUri,

        [Parameter (mandatory = $true)]
        [string]$pswyouwant,

        [Parameter (mandatory = $true)]
        [string]$applicationId,

        [Parameter (mandatory = $true)]
        [string]$tenantId
    )

    Login-AzureRmAccount

    $myAADApp = New-AzureRmADApplication 
        -DisplayName $displayName 
        -HomePage $homePage
        -IdentifierUris $appUri 
        -Password $pswyouwant

    New-AzureRmADServicePrincipal -ApplicationId $applicationId

    New-AzureRmRoleAssignment  
        -RoleDefinitionName Contributor 
        -ServicePrincipalName $applicationId

    $svcPrincipalCredentials = Get-Credential

    Login-AzureRmAccount  
        -Credential $svcPrincipalCredentials 
        -ServicePrincipal 
        -TenantId $tenantId

    Get-AzureRmResourceGroup
}

function Login-To-Azure
{
    Param(
        [Parameter (mandatory = $true)]
        [string]$clientId,

        [Parameter (mandatory = $true)]
        [string]$tenantId
    )

    #Do this as sometimes you can get the error that credentials are expired or not added
    Clear-AzureProfile –Force

    Add-AzureAccount

    #Get credentials need AAD credentials username=clientid and password=servicePrincipalPassword
    $svcPrincipalCredentials = Get-Credential -UserName $clientId

    Login-AzureRmAccount -Credential $svcPrincipalCredentials -ServicePrincipal -TenantId $tenantId
}

function Create-WebApp
{
    Param(
        [Parameter (mandatory = $true)]
        [string]$resourceGroupName,

        [Parameter (mandatory = $true)]
        [string]$servicePlanName,

        [Parameter (mandatory = $true)]
        [string]$webAppName,

        [Parameter (mandatory = $true)]
        [string]$location
    )

    $resourceGroups = Get-AzureRmResourceGroup | where-object -FilterScript{$_.Name -eq $resourceGroupName}
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

    $servicePlans = Get-AzureRmAppServicePlan | where-object -FilterScript{$_.Name -eq $servicePlanName}
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
}

function Create-Storage-Account
{
    Param(

        [Parameter (mandatory = $true)]
        [string]$storageName,

        [Parameter (mandatory = $true)]
        [string]$location
    )

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
}

function Start-Stop-WebApp
{
    Param(

        #Leaving this empty will result in false and will always try to start webapp
        [Parameter (mandatory = $false)]
        [bool]$stop,

        [Parameter (mandatory = $true)]
        [string]$webAppName
    )

    $status = 'Stopped'
    if ($stop)
    {
        $status = 'Running'
    }

    #Get Running WebApps (Websites)
    if ($webAppName) 
    {
        $websites = Get-AzureWebsite | where-object -FilterScript{$_.state -eq $status -and $_.Name -eq $webAppName}
    }
    else
    {
        $websites = Get-AzureWebsite | where-object -FilterScript{$_.state -eq $status}
    }


    foreach ($website In $websites) 
    {
        if ($stop)
        {
            $result = Stop-AzureWebsite $website.Name
            if($result)  
            { 
               Write-Output "- $($website.Name) did not shutdown successfully" 
            } 
            else 
            { 
               Write-Output "+ $($website.Name) shutdown successfully" 
            } 
         } 
         else 
         { 
            $result = Start-AzureWebsite $website.Name 
            if($result) 
            { 
                Write-Output "- $($website.Name) did not start successfully" 
            } 
            else 
            { 
                Write-Output "+ $($website.Name) started successfully" 
            } 
         }  
    }
}

function Delete-WebApp-Resources
{
     Param(
            [Parameter (mandatory = $false)]
            [string]$webAppName,
            [Parameter (mandatory = $false)]
            [string]$servicePlanName,
            [Parameter (mandatory = $false)]
            [string]$resourceGroupName,
            [Parameter (mandatory = $false)]
            [string]$storageAccountName
        )
     
     if($webAppName)
     {
         $result = Remove-AzureRmWebApp -Name $webAppName
         if($result)  
         { 
            Write-Output "+ $($webAppName) webapp failed to remove"               
         } 
         else 
         { 
             Write-Output "- $($webAppName) webapp removed"
         } 
     }
     
     if($servicePlanName)
     {
         $result = Remove-AzureRmAppServicePlan -Name $servicePlanName
         if($result)  
         { 
             Write-Output "+ $($servicePlanName) service plan failed to remove" 
         } 
         else 
         { 
            Write-Output "- $($servicePlanName) service plan removed"              
         } 
     }

     if($resourceGroupName)
     {
         $result = Remove-AzureRmResourceGroup -Name $resourceGroupName
         if($result)  
         { 
             Write-Output "- $($resourceGroupName) resource group removed" 
         } 
         else 
         { 
             Write-Output "+ $($resourceGroupName) resource group failed to remove" 
         } 
     }

     if($storageAccountName)
     {
         $result = Remove-AzureStorageAccount -StorageAccountName $storageAccountName
         if($result)  
         { 
             Write-Output "- $($storageAccountName) storage account removed" 
         } 
         else 
         { 
             Write-Output "+ $($storageAccountName) storage account failed to remove" 
         } 
     }

}

export-modulemember -function Create-ServicePrincipal
export-modulemember -function Login-To-Azure
export-modulemember -function Create-WebApp
export-modulemember -function Create-Storage-Account
export-modulemember -function Start-Stop-WebApp
export-modulemember -function Delete-WebApp-Resources