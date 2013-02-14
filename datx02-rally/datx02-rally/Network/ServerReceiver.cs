﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lidgren.Network;

namespace datx02_rally
{
    class ServerReceiver
    {
        NetClient ServerThread;
        ServerClient ServerHandler;

        public ServerReceiver(NetClient serverThread, ServerClient handler)
        {
            this.ServerThread = serverThread;
            this.ServerHandler = handler;
        }

        public void ReceiveMessages()
        {
            List<NetIncomingMessage> messages = new List<NetIncomingMessage>();
            ServerThread.ReadMessages(messages);
            foreach (var message in messages)
            {
                switch (message.MessageType)
                {
                    case NetIncomingMessageType.Data:
                        ParseDataPackage(message);
                        break;
                    default:
                        Console.WriteLine("Received unknown network message: " + message.MessageType);
                        Console.WriteLine(message.ReadString());
                        break;
                }
            }
        }

        private void ParseDataPackage(NetIncomingMessage msg) 
        {
            MessageType type = (MessageType)msg.ReadByte();
            Dictionary<byte, Player> PlayerList = ServerHandler.Players;
            Player player = null;
            if (type != MessageType.LobbyUpdate && !PlayerList.TryGetValue(msg.ReadByte(), out player))
            {
                Console.WriteLine("Received message from unknown discoveredPlayer, discarding...");
                return;
            }
            switch (type)
            {
                case MessageType.PlayerPos:
                    player.SetPosition(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
                    break;
                case MessageType.Chat:
                    Console.WriteLine("{0}: {1}", player.PlayerName, msg.ReadString());
                    break;
                case MessageType.Debug:
                    break;
                case MessageType.LobbyUpdate:
                    byte playerCount = msg.ReadByte();
                    for (int i = 0; i < playerCount; i++)
                    {
                        byte discoveredPlayerId = msg.ReadByte();
                        string discoveredPlayerName = msg.ReadString();
                        Player discoveredPlayer;
                        if (!PlayerList.TryGetValue(discoveredPlayerId, out discoveredPlayer)) 
                        {
                            PlayerList[discoveredPlayerId] = new Player(Game1.GetInstance(), discoveredPlayerId, discoveredPlayerName);
                        }
                        else if (discoveredPlayer.PlayerName != discoveredPlayerName) //changed player name
                        {
                            discoveredPlayer.PlayerName = discoveredPlayerName;
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
