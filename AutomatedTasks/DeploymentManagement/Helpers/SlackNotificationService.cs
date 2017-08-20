using System.Diagnostics;
using Slack.Client;

namespace Helpers
{
    public class SlackNotificationService
    {
        public static void Notify(string message)
        {
            Trace.TraceInformation(message);

            var slackClient = new SlackClient(AppConfigurationSettings.Webhook);
            
            slackClient.Send(message);
        }
    }
}
