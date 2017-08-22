Create certificate to upload for authentication purpose

start powershell admin
PS C:\> New-SelfSignedCertificate -DnsName "http://sillytestforwebapp.azurewebsites.net" -CertStoreLocation cert:\localmachine\my

run>mmc>add snapin certificates, export to whereever

upload to azure portal web app to subscription ID

when busy creating credentials and to find certificate use Microsoft.WindowsAzure.Management.Compute version 5. Not latest

