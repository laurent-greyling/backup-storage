# NeededResourcesForWebApp

This is a powershell module to:

### Create a service principle
if you do not already have a service principal in Azure Active Directory run

`Create-ServicePrincipal -displayName [displayname] -homePage [home page] -appUri [appUri] -pswyouwant [yourpassword] -applicationId [applicationId] -tenantId [tenantid]`

This will create your service principal you need to log in and run the other commands in this psm file.

### Login to Azure with AAD
First step before doing any of the other commands is to log into Azure with you AAD credentials.

Run `Login-To-Azure -clientId [clientid] -tenantId [tenantid] `

### Create a webapp
The following module will create

- ResourceGroup: based on the name you give the parameter resource group
- App service plan: based on the name you give the serviceplan parameter. It will create it on standard tier with a small workersize and 1 worker. if you need larger adapt psm script.
- Create Webapp: based on name given to the parameter web app name
- location: based on where you want to have resources situated, in my case this would be West Europe.

run `Create-WebApp -resourceGroupName [resource group name] -servicePlanName [service plan name] -webAppName [webapp name] -location [location]`

### Create Storage Account
This will create a storage account for you based on the name you give your storage

run `Create-Storage-Account -storageName [storagename] -location [location]`

### Start and Stop webapp
This will allow you to start or stop a web app to lower your costs on dev environments.

- stop is not mandatory. To start up your webapp simply omit this parameter 

run `Start-Stop-WebApp -stop [$true] -webAppName [webapp name]`

### Delete webapp resources
This is to delete any of the resources you created.

If a specific resource should not be deleted just omit that specific param and it will not be deleted.

run `Delete-WebApp-Resources -webAppName [webAppName] -servicePlanName [servicePlanName] -resourceGroupName [resourceGroupName] -storageAccountName [storageAccountName]`





