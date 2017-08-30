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