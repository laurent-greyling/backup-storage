Login-AzureRmAccount

$myAADApp = New-AzureRmADApplication 
    -DisplayName "<DisplayName>" 
    -HomePage "<Homepage of App>" 
    -IdentifierUris "<App uri>" 
    -Password "<Whatever apssword>"

New-AzureRmADServicePrincipal -ApplicationId "<your application id>"

New-AzureRmRoleAssignment  
    -RoleDefinitionName Contributor 
    -ServicePrincipalName "<application id>"

$svcPrincipalCredentials = Get-Credential

Login-AzureRmAccount  
    -Credential $svcPrincipalCredentials 
    -ServicePrincipal 
    -TenantId "<your tenant id guid>"

Get-AzureRmResourceGroup