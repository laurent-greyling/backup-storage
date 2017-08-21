# microsoft-azure-apps
This repository hold some apps to conduct certain Azure Operations

Current operations and Apps

- Backing up Azure Table Storage and Blob Storage - See readme.txt in `Final.Backup` and `backup-storage`
  - __Final.Backup__: is a final solution to backup and restore table and blob storage
  - __backup-storage__: is a play ground where different options for backing up and restoring was tested and played with
  
- Switch on and off classic cloud services
 - AutomatedTasks.DeploymentManagement - this will basically save your configuration, packages and diagnostic configuration to blob storage     and delete your cloud service. Next morning it will restore all services by redeploying them for you. This is done for swiching off   
    services at night that is not needed and saving some costs. 
