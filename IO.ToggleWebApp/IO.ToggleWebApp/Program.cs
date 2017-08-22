using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO.ToggleWebApp.AzureManagement;

namespace IO.ToggleWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new IoWebApp().Execute().Wait();
        }
    }
}
