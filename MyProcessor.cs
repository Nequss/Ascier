using System.IO;

namespace Ascier
{
    public class MyProcessor : CLI_Sharp.CommandProcessor
    {
        Manager manager = new Manager();

        public override void processCommand(string cmd)
        {
            switch (cmd)
            {
                case "i":
                    manager.Import();
                    break;
                case "s": 
                    manager.Show("momo"); //name of file in input folder
                    break;
            }
        }
    }
}