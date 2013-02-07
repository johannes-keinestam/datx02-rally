﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lidgren.Network;
using System.Net;

namespace datx02_rally
{
    class ServerClient
    {
        NetClient client;
        readonly int PORT = 14242;

        public ServerClient()
        {
            NetPeerConfiguration config = new NetPeerConfiguration("DATX02");
            config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);

            client = new NetClient(config);
            client.Start();
        }

        public void Connect(IPAddress IP) {
            client.Connect(new IPEndPoint(IP, PORT));
        }

        public void SendTestData()
        {
            NetOutgoingMessage msg = client.CreateMessage("Hello world!");
            client.SendMessage(msg, NetDeliveryMethod.ReliableUnordered);
        }
    }
}
