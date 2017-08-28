using System;
using IO.ToggleWebApp.AzureManagement;

namespace IO.ToggleWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new IoWebApp().Execute().Wait();
            Console.ReadKey();
        }
    }
}
