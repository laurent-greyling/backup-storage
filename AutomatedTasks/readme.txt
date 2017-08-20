# Backup and Restore Azure Cloud Services

This is based on the Spike UST 5809 and describes how Azure cloud services will be
backed up and restored for each development team. For this purpose of backing up and
restoring, webjobs were created for:

* Backup Azure deployments into storage
* Delete the current Azure deployments
* Restore Azure deployments to its original state

For each environment there exist two webjobs under the webapp `DeploymentAutomation`:

1. `{environmentName}BackupDeploymentPackages`
2. `{environmentName}RestoreDeploymentPackages`

This was built in order to stop the active running of deployments in non working
hours.

##Backup and Delete Azure Deployments

The `BackupDeploymentPackages` runs every day at 7pm and does three things:

1. Get the current deployed packages and configuration files for each cloud service.
2. Save the packages in storage under `http://{environmentName}.blob.core.windows.net/{environmentName}service`
3. If the process of saving was succesful, it deletes the deployment

##Restore Azure Deployments

The `RestoreDeploymentPackages` runs every day at 7am and does four things:

1. Fetch Public and Private configuration for enabling diagnostics - this is:

  * Sitting in blobcontainers>{environmentname}service> Diagnostics public and private config
  * The private config should contain your environment and blob connection key
  * These files must not be removed, if it is removed you can find it in the solution on tfs and change private config to your environment

2. Add the public and private config to extensions for enabling diagnostics
3. Fetch the latest configuration and deployment package from storage
4. Restore deployment based on the latest package fetched above steps. This step will also upload diagnostics (step 1 and 2 only add 
   the extensions and make it available for upload)

## Configure Schedule

The initial scheduling of these webjobs are set to run daily at 7pm and 7am. This however can be managed per team, depending on team requirements. This can be done via the old portal:

1. Go to `DeploymentAutomation/jobs` under webapps
2. Find `team jobs`
3. Click on `schedule`
4. On the following screen click on the  job you wish to change
5. Under the heading Schedule, schedule the time required by team for job to run 
(see Figure 1).

Alternatively:

1. In old portal, go to `Scheduler`
2. Go into `jobs`
3. Find the job that need to be rescheduled
4. Under the heading Schedule, schedule the time required by team for job to run 
(see Figure 1).

Figure 1.
![image](https://cloud.githubusercontent.com/assets/7439999/11686378/7b80691c-9e80-11e5-8356-e83a02ef57e5.png)

## Running Times

As mentioned above, these jobs run daily. This means that deployments are removed every day at 7pm. When the job runs on friday, the deployments are backed up and removed and will not be restored until Monday morning at 7am.

If however it is necessary that deployments stay intact, e.g. for running tests during a night or over a weekend, there are two ways of doing this:

1. Go to old portal and in scheduler find the job that need to be disabled. Select this job and disable it (Disable button at bottom of page).
![image](https://cloud.githubusercontent.com/assets/7439999/11686978/72927d64-9e84-11e5-8445-7a7630a96000.png)

2. Go to the portal and delete the specific webjob. This however means that the job need to be redeployed when it needs to run again. This applies to `BackupDeploymentPackages` as the `RestoreDeploymentPackages` will not do anything if a deployment already exist.

__NOTE__ redeploying the job after deletion or enabling the job after disabling it, is the responsibility of the member who deleted / disabled it.
 
## Deploying the WebJobs

1. In the current branch open solution called `AutomatedTasks.TeamDeploymentManagement.sln`. In this solution there is a folder for the specific team that need to be deployed.

2. Right click on the job to be deployed and select `Publish as Azure WebJob...`.

3. Follow the instruction in the wizard and publish the webjob. If there is an error, this is likely because of incorrect signin credentials.

Webjobs were created per team to eanable individual team scheduling. This means that if we ever get a new team, this team needs to be added to the project.

## Slack Notifications and Logs

To not have each team check the Azure portal on a daily basis to make sure their deployments succeeded and are up and running, a slack integration was added. This is a basic webhook to send a message to the channel `#deploynotify`.

Only one channel was created in order not to create multiple webhooks, as Slack complains that we are reaching our integration limit. If however, a team would like a notification sent to their own team channel, the team can add a webhook and replace the current webhook in the App.config file for that team.

In this channel, for every notification the team name of the deployments appears first in caps, as below:
 
* TEAM: Start Process for Backup/Delete of Deployments
* TEAM: Finished backing up all deployments
* TEAM - Exception: Big fail here --- For more information see http://{environment}.blob.core.windows.net/deploymentlog RestoreDeploymentPackages
* TEAM: Start restoring Deployments
* TEAM: Finished restoring deployments 

This will give each team a fair indication if everything is okay. In the case where there is an error and it was not communicated correctly the logs can be checked at `http://{environmentName}.blob.core.windows.net/deploymentlog`.

## In Case of Failure
In case of failure for any reason, the following happens based on where in the process the webjob was:

### Saving and Deleting
If for some reason a package fails to save for the specific cloud service, the webjob retries one more time to save. If this also fails, the deployment is not deleted and left running.

If deleting a deploment fails, the webjob retries one more time, if this fails, the deployment is not deleted and left running.

Note that if the restore webjob encounters a deployment that is still running, it does not attempt to redeploy a backed up deployment as it might be older than the deployment currently running. This ensures that the team deployment stay at current deployment for that specific service.

__NOTE__ Backup and delete operations run for deployments in parrallel. Each service is saved and deleted seperately, this also ensures that if one fails not all will fail.

### Manually Restoring 
If for some reason the restoring of a deployment fails, the team deployment for a specific service will not be available. In this case you can manually run the webjob again on demand from azure portal, because the restore job ignores services that are already deployed an only attempt to deploy into the empty slots. 

![image](https://cloud.githubusercontent.com/assets/7439999/11709693/e2410a6e-9f19-11e5-8a26-cf4680afc676.png)

If this process also fails, it is recommended to manually republish and investigate the error.
