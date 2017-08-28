## Switch WebApp On and Off

This app is for switch on and off your webapps in order to save some cost. This is mostly intended for your development environments.

### What is needed

First off you need to create an Azure Active Directory (AAD) application. This can easily be done by using the `CreateServicePrincipalAAD.ps1` powershell cmdlet.

Replace the given fields with your own credentials and ids. Run this cmdlet and it will create everything for you.

The other option is to create the AAD via the azure portal. 

After you have create the AAD you can use the credentials to fill in the values in the `App.config` file

```

<add key="clientId" value="" />
<add key="servicePrincipalPassword" value=""/>
<add key="subscriptionId" value="" />
<add key="azureTenantId" value="" />
<add key="webappName" value="" />
<add key="resourceGroupName" value="" />
<add key="IO" value="" /> <!-- value of on is needed to switch on webapp, anything else will switch it off-->

```

After you have set the values, run the app and your webapp will either be switched on or off.

##### Some additional Notes

When an AAD is set up you can basically use this app as well to login and add the code to do some other stuff like creating webapps and monitor the status of your apps.



