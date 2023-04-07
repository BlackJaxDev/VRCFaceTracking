﻿using Rug.Osc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VRCFaceTracking.OSC
{
    public static class OSCUtils
    {
        public static byte[] EnsureCompliance(this byte[] inputArr)
        {
            var nullTerm = new byte[inputArr.Length + 1];
            Array.Copy(inputArr, nullTerm, inputArr.Length);

            int n = nullTerm.Length + 3;
            int m = n % 4;
            int closestMult = n - m;
            int multDiff = closestMult - nullTerm.Length;

            // Construct new array of zeros with the correct length + 1 for null terminator
            byte[] newArr = new byte[nullTerm.Length + multDiff];
            Array.Copy(nullTerm, newArr, nullTerm.Length);
            return newArr;
        }
    }

    public class OscMain
    {
        private OscSender OscSenderClient;
        //private static readonly Socket SenderClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private static readonly Socket ReceiverClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private static Thread receiveThread;
        
        public OscMain(string address, int outPort, int inPort)
        {
            var addr = IPAddress.Parse(address);
            OscSenderClient = new OscSender(addr, 0, outPort);
            OscSenderClient.Connect();
            //SenderClient.Connect(new IPEndPoint(addr, outPort));
            ReceiverClient.Bind(new IPEndPoint(addr, inPort));
            ReceiverClient.ReceiveTimeout = 1000;

            receiveThread = new Thread(() =>
            {
                while (!MainStandalone.MasterCancellationTokenSource.IsCancellationRequested)
                    Recv();
            });
            receiveThread.Start();
        }

        private void Recv()
        {
            try
            {
                byte[] buffer = new byte[2048];
                ReceiverClient.Receive(buffer, buffer.Length, SocketFlags.None);
                var newMsg = new OscMessageVRCFT(buffer);
                if (newMsg.Address == "/avatar/change")
                    ConfigParser.ParseNewAvatar((string) newMsg.Value);
            }
            catch (SocketException)
            {
                // Ignore as this is most likely a timeout exception
            }
        }

        //public void Send(byte[] data)
        //    => SenderClient.Send(data, data.Length, SocketFlags.None);
        public void Send(OscMessage message)
            => OscSenderClient.Send(message);
    }
}