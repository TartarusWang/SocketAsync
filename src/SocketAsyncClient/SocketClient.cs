// ***********************************************************************
// Assembly         : SocketAsyncClient
// Author           : fuchun
// Created          : 2016--09-19
// Last Modified By : fuchun
// Last Modified On : 2016--09-19
// ***********************************************************************
// <copyright file="SocketClient.cs" company="">
//     Copyright ©  2016
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace SocketAsyncClient
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    // Implements the connection logic for the socket client.
    internal sealed class SocketClient : IDisposable
    {
        // Constants for socket operations.
        private const Int32 ReceiveOperation = 1, SendOperation = 0;

        // The socket used to send/receive messages.
        private Socket clientSocket;

        // Flag for connected socket.
        private Boolean connected = false;

        // Listener endpoint.
        private IPEndPoint hostEndPoint;

        // Signals a connection.
        private static AutoResetEvent autoConnectEvent = new AutoResetEvent(false);

        // Signals the send/receive operation.
        private static AutoResetEvent[] autoSendReceiveEvents = new AutoResetEvent[]
        {
            new AutoResetEvent(false),
            new AutoResetEvent(false)
        };

        // Create an uninitialized client instance.
        // To start the send/receive processing call the
        // Connect method followed by SendReceive method.
        internal SocketClient(String hostName, Int32 port)
        {
            // Get host related information.
            IPHostEntry host = Dns.GetHostEntry(hostName);

            // Address of the host.
            IPAddress[] addressList = host.AddressList;

            // Instantiates the endpoint and socket.
            hostEndPoint = new IPEndPoint(addressList[addressList.Length - 1], port);
            clientSocket = new Socket(hostEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        // Connect to the host.
        internal void Connect()
        {
            SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();

            connectArgs.UserToken = clientSocket;
            connectArgs.RemoteEndPoint = hostEndPoint;
            connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnect);

            clientSocket.ConnectAsync(connectArgs);
            autoConnectEvent.WaitOne();

            SocketError errorCode = connectArgs.SocketError;
            if (errorCode != SocketError.Success)
            {
                throw new SocketException((Int32)errorCode);
            }
        }

        /// Disconnect from the host.
        internal void Disconnect()
        {
            clientSocket.Disconnect(false);
        }

        // Calback for connect operation
        private void OnConnect(object sender, SocketAsyncEventArgs e)
        {
            // Signals the end of connection.
            autoConnectEvent.Set();

            // Set the flag for socket connected.
            connected = (e.SocketError == SocketError.Success);
        }

        // Calback for receive operation
        private void OnReceive(object sender, SocketAsyncEventArgs e)
        {
            // Signals the end of receive.
            autoSendReceiveEvents[SendOperation].Set();
        }

        // Calback for send operation
        private void OnSend(object sender, SocketAsyncEventArgs e)
        {
            // Signals the end of send.
            autoSendReceiveEvents[ReceiveOperation].Set();
            if (e.SocketError == SocketError.Success)
            {
                if (e.LastOperation == SocketAsyncOperation.Send)
                {
                    // Prepare receiving.
                    Socket s = e.UserToken as Socket;

                    byte[] receiveBuffer = new byte[255];
                    e.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
                    e.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceive);
                    s.ReceiveAsync(e);
                }
            }
            else
            {
                ProcessError(e);
            }
        }

        // Close socket in case of failure and throws
        // a SockeException according to the SocketError.
        private void ProcessError(SocketAsyncEventArgs e)
        {
            Socket s = e.UserToken as Socket;
            if (s.Connected)
            {
                // close the socket associated with the client
                try
                {
                    s.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                {
                    // throws if client process has already closed
                }
                finally
                {
                    if (s.Connected)
                    {
                        s.Close();
                    }
                }
            }

            // Throw the SocketException
            throw new SocketException((Int32)e.SocketError);
        }

        // Exchange a message with the host.
        internal String SendReceive(String message)
        {
            if (connected)
            {
                // Create a buffer to send.
                Byte[] sendBuffer = Encoding.ASCII.GetBytes(message);

                // Prepare arguments for send/receive operation.
                SocketAsyncEventArgs completeArgs = new SocketAsyncEventArgs();
                completeArgs.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                completeArgs.UserToken = clientSocket;
                completeArgs.RemoteEndPoint = hostEndPoint;
                completeArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);

                // Start sending asynchronously.
                clientSocket.SendAsync(completeArgs);

                // Wait for the send/receive completed.
                AutoResetEvent.WaitAll(autoSendReceiveEvents);

                // Return data from SocketAsyncEventArgs buffer.
                return Encoding.ASCII.GetString(completeArgs.Buffer, completeArgs.Offset, completeArgs.BytesTransferred);
            }
            else
            {
                throw new SocketException((Int32)SocketError.NotConnected);
            }
        }

        #region IDisposable Members

        // Disposes the instance of SocketClient.
        public void Dispose()
        {
            autoConnectEvent.Close();
            autoSendReceiveEvents[SendOperation].Close();
            autoSendReceiveEvents[ReceiveOperation].Close();
            if (clientSocket.Connected)
            {
                clientSocket.Close();
            }
        }

        #endregion
    }
}
