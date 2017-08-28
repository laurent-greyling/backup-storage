using System;
using IO.ToggleWebApp.AzureManagement;

namespace IO.ToggleWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //create a webapp and associated resourcegroup
            //new IoWebApp().CreateWebApp("FromConsoleApp", "somefancyappfromconsole", "West Europe", $"{Guid.NewGuid()}",
            //    "customer@email.com");

            //Switch WebApp on or off
            new IoWebApp().Execute().Wait();

            Console.ReadKey();
        }
    }
}
