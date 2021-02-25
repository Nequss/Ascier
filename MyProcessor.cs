using System.IO;

namespace Ascier
{
    public class MyProcessor : CLI_Sharp.CommandProcessor
    {
        Manager manager = new Manager();

        public override void processCommand(string cmd)
        {
            Program.Logger.info("Waiting for command");

            switch (cmd)
            {
                case "i":
                    manager.Import();
                    break;
                case "s":
                        manager.Show("kiana");
                    break;
            }
        }
    }
}