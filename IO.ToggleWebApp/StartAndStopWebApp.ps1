
Param(
    [Parameter (mandatory = $true)]
    [string]$userName,

    #Leaving this empty will result in false and will always try to start webapp
    [Parameter (mandatory = $false)]
    [bool]$stop,

    [Parameter (mandatory = $true)]
    [string]$tenantId,

    [Parameter (mandatory = $false)]
    [string]$webAppName
)

#Do this as sometimes you can get the error that credentials are expired or not added
Clear-AzureProfile –Force

Add-AzureAccount

#Get credentials need AAD credentials username=clientid and password=servicePrincipalPassword
$svcPrincipalCredentials = Get-Credential -UserName $userName

Login-AzureRmAccount -Credential $svcPrincipalCredentials -ServicePrincipal -TenantId $tenantId


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




