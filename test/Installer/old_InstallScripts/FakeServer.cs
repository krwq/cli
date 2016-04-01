// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Microsoft.DotNet.Tests.InstallScripts
{
    public class FakeServer : IDisposable
    {
        private Socket _server;
        private HashSet<Socket> _clients;
        
        public string Address { get; private set; }
        public int Port { get; private set; }
        
        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
        
        public FakeServer()
        {
            Port = GetFreePort();
            Address = $"http://localhost:{Port}";
        }
        
        public void WaitForConnection()
        {
        
        }
        
        public void Dispose()
        {
            _server?.Dispose();
            
        }
    }
}*/