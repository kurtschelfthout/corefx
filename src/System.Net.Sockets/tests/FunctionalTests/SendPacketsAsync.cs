// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Test.Common;
using System.Runtime.InteropServices;
using System.Threading;

using Xunit;
using Xunit.Abstractions;

namespace System.Net.Sockets.Tests
{
    public partial class SendPacketsAsync
    {
        private readonly ITestOutputHelper _log;

        private IPAddress _serverAddress = IPAddress.IPv6Loopback;
        // Accessible directories for UWP app:
        // C:\Users\<UserName>\AppData\Local\Packages\<ApplicationPackageName>\
        private string TestFileName = Environment.GetEnvironmentVariable("LocalAppData") + @"\NCLTest.Socket.SendPacketsAsync.testpayload";
        private static int s_testFileSize = 1024;

        #region Additional test attributes

        public SendPacketsAsync(ITestOutputHelper output)
        {
            _log = TestLogging.GetInstance();

            byte[] buffer = new byte[s_testFileSize];

            for (int i = 0; i < s_testFileSize; i++)
            {
                buffer[i] = (byte)(i % 255);
            }

            try
            {
                _log.WriteLine("Creating file {0} with size: {1}", TestFileName, s_testFileSize);
                using (FileStream fs = new FileStream(TestFileName, FileMode.CreateNew))
                {
                    fs.Write(buffer, 0, buffer.Length);
                }
            }
            catch (IOException)
            {
                // Test payload file already exists.
                _log.WriteLine("Payload file exists: {0}", TestFileName);
            }
        }

        #endregion Additional test attributes


        #region Basic Arguments

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void Disposed_Throw(SocketImplementationType type)
        {
            int port;
            using (SocketTestServer.SocketTestServerFactory(type, _serverAddress, out port))
            {
                using (Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Connect(new IPEndPoint(_serverAddress, port));
                    sock.Dispose();

                    Assert.Throws<ObjectDisposedException>(() =>
                    {
                        sock.SendPacketsAsync(new SocketAsyncEventArgs());
                    });
                }
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "Bug in SendPacketsAsync that dereferences null SAEA argument")]
        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void NullArgs_Throw(SocketImplementationType type)
        {
            int port;
            using (SocketTestServer.SocketTestServerFactory(type, _serverAddress, out port))
            {
                using (Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Connect(new IPEndPoint(_serverAddress, port));
                    
                    AssertExtensions.Throws<ArgumentNullException>("e", () => sock.SendPacketsAsync(null));
                }
            }
        }

        [Fact]
        public void NotConnected_Throw()
        {
            Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            // Needs to be connected before send

            Assert.Throws<NotSupportedException>(() =>
            {
                socket.SendPacketsAsync(new SocketAsyncEventArgs { SendPacketsElements = new SendPacketsElement[0] });
            });
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "Bug in SendPacketsAsync that dereferences null m_SendPacketsElementsInternal array")]
        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void NullList_Throws(SocketImplementationType type)
        {
            AssertExtensions.Throws<ArgumentNullException>("e.SendPacketsElements", () => SendPackets(type, (SendPacketsElement[])null, SocketError.Success, 0));
        }

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void NullElement_Ignored(SocketImplementationType type)
        {
            SendPackets(type, (SendPacketsElement)null, 0);
        }

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void EmptyList_Ignored(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement[0], SocketError.Success, 0);
        }

        [OuterLoop] // TODO: Issue #11345
        [Fact]
        public void SocketAsyncEventArgs_DefaultSendSize_0()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            Assert.Equal(0, args.SendPacketsSendSize);
        }

        #endregion Basic Arguments

        #region Buffers

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void NormalBuffer_Success(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[10]), 10);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void NormalBufferRange_Success(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[10], 5, 5), 5);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void EmptyBuffer_Ignored(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[0]), 0);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void BufferZeroCount_Ignored(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[10], 4, 0), 0);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void BufferMixedBuffers_ZeroCountBufferIgnored(SocketImplementationType type)
        {
            SendPacketsElement[] elements = new SendPacketsElement[]
            {
                new SendPacketsElement(new byte[10], 4, 0), // Ignored
                new SendPacketsElement(new byte[10], 4, 4),
                new SendPacketsElement(new byte[10], 0, 4)
            };
            SendPackets(type, elements, SocketError.Success, 8);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void BufferZeroCountThenNormal_ZeroCountIgnored(SocketImplementationType type)
        {
            Assert.True(Capability.IPv6Support());

            EventWaitHandle completed = new ManualResetEvent(false);

            int port;
            using (SocketTestServer.SocketTestServerFactory(type, _serverAddress, out port))
            {
                using (Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Connect(new IPEndPoint(_serverAddress, port));
                    using (SocketAsyncEventArgs args = new SocketAsyncEventArgs())
                    {
                        args.Completed += OnCompleted;
                        args.UserToken = completed;

                        // First do an empty send, ignored
                        args.SendPacketsElements = new SendPacketsElement[]
                        {
                            new SendPacketsElement(new byte[5], 3, 0)
                        };

                        if (sock.SendPacketsAsync(args))
                        {
                            Assert.True(completed.WaitOne(TestSettings.PassingTestTimeout), "Timed out");
                        }
                        Assert.Equal(SocketError.Success, args.SocketError);
                        Assert.Equal(0, args.BytesTransferred);

                        completed.Reset();
                        // Now do a real send
                        args.SendPacketsElements = new SendPacketsElement[]
                        {
                            new SendPacketsElement(new byte[5], 1, 4)
                        };

                        if (sock.SendPacketsAsync(args))
                        {
                            Assert.True(completed.WaitOne(TestSettings.PassingTestTimeout), "Timed out");
                        }
                        Assert.Equal(SocketError.Success, args.SocketError);
                        Assert.Equal(4, args.BytesTransferred);
                    }
                }
            }
        }

        #endregion Buffers

        #region TransmitFileOptions
        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SocketDisconnected_TransmitFileOptionDisconnect(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[10], 4, 4), TransmitFileOptions.Disconnect, 4);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SocketDisconnectedAndReusable_TransmitFileOptionReuseSocket(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(new byte[10], 4, 4), TransmitFileOptions.Disconnect | TransmitFileOptions.ReuseSocket, 4);
        }
        #endregion

        #region Files

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_EmptyFileName_Throws(SocketImplementationType type)
        {
            AssertExtensions.Throws<ArgumentException>("path", null, () =>
            {
                SendPackets(type, new SendPacketsElement(string.Empty), 0);
            });
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        [PlatformSpecific(TestPlatforms.Windows)] // whitespace-only is a valid name on Unix
        public void SendPacketsElement_BlankFileName_Throws(SocketImplementationType type)
        {
            AssertExtensions.Throws<ArgumentException>("path", null, () =>
            {
                // Existence is validated on send
                SendPackets(type, new SendPacketsElement("   "), 0);
            });
        }

        [Theory]
        [ActiveIssue(27269)]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        [PlatformSpecific(TestPlatforms.Windows)] // valid filename chars on Unix
        public void SendPacketsElement_BadCharactersFileName_Throws(SocketImplementationType type)
        {
            AssertExtensions.Throws<ArgumentException>("path", null, () =>
            {
                // Existence is validated on send
                SendPackets(type, new SendPacketsElement("blarkd@dfa?/sqersf"), 0);
            });
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_MissingDirectoryName_Throws(SocketImplementationType type)
        {
            Assert.Throws<DirectoryNotFoundException>(() =>
            {
                // Existence is validated on send
                SendPackets(type, new SendPacketsElement(Path.Combine("nodir", "nofile")), 0);
            });
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_MissingFile_Throws(SocketImplementationType type)
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                // Existence is validated on send
                SendPackets(type, new SendPacketsElement("DoesntExit"), 0);
            });
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_File_Success(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(TestFileName), s_testFileSize); // Whole File
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileZeroCount_Success(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(TestFileName, 0, 0), s_testFileSize);  // Whole File
            SendPackets(type, new SendPacketsElement(TestFileName, 0L, 0), s_testFileSize);  // Whole File
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FilePart_Success(SocketImplementationType type)
        {
            SendPackets(type, new SendPacketsElement(TestFileName, 10, 20), 20);
            SendPackets(type, new SendPacketsElement(TestFileName, 10L, 20), 20);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileMultiPart_Success(SocketImplementationType type)
        {
            var elements = new[]
            {
                new SendPacketsElement(TestFileName, 10, 20),
                new SendPacketsElement(TestFileName, 30, 10),
                new SendPacketsElement(TestFileName, 0, 10),
                new SendPacketsElement(TestFileName, 10L, 20),
                new SendPacketsElement(TestFileName, 30L, 10),
                new SendPacketsElement(TestFileName, 0L, 10),
            };
            SendPackets(type, elements, SocketError.Success, 80);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileLargeOffset_Throws(SocketImplementationType type)
        {
            // Length is validated on Send
            SendPackets(type, new SendPacketsElement(TestFileName, 11000, 1), SocketError.InvalidArgument, 0);
            SendPackets(type, new SendPacketsElement(TestFileName, (long)uint.MaxValue + 11000, 1), SocketError.InvalidArgument, 0);
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileLargeCount_Throws(SocketImplementationType type)
        {
            // Length is validated on Send
            SendPackets(type, new SendPacketsElement(TestFileName, 5, 10000), SocketError.InvalidArgument, 0);
            SendPackets(type, new SendPacketsElement(TestFileName, 5L, 10000), SocketError.InvalidArgument, 0);
        }

        #endregion Files

        #region FileStreams

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStream_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync:true))
            {
                stream.Seek(s_testFileSize / 2, SeekOrigin.Begin);
                SendPackets(type, new SendPacketsElement(stream), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);

                SendPackets(type, new SendPacketsElement(stream), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamZeroCount_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                stream.Seek(s_testFileSize / 2, SeekOrigin.Begin);
                SendPackets(type, new SendPacketsElement(stream, 0, 0), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);

                SendPackets(type, new SendPacketsElement(stream, 0, 0), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamSizeCount_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                stream.Seek(s_testFileSize / 2, SeekOrigin.Begin);
                SendPackets(type, new SendPacketsElement(stream, 0, s_testFileSize), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);

                SendPackets(type, new SendPacketsElement(stream, 0, s_testFileSize), s_testFileSize); // Whole File
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize / 2, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamPart_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                stream.Seek(s_testFileSize - 10, SeekOrigin.Begin);
                SendPackets(type, new SendPacketsElement(stream, 0, 20), 20);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize - 10, stream.Position);

                SendPackets(type, new SendPacketsElement(stream, 10, 20), 20);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize - 10, stream.Position);

                SendPackets(type, new SendPacketsElement(stream, s_testFileSize - 20, 20), 20);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize - 10, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamMultiPart_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                var elements = new[]
                {
                    new SendPacketsElement(stream, 0, 20),
                    new SendPacketsElement(stream, s_testFileSize - 10, 10),
                    new SendPacketsElement(stream, 0, 10),
                    new SendPacketsElement(stream, 10, 20),
                    new SendPacketsElement(stream, 30, 10),
                };
                stream.Seek(s_testFileSize - 10, SeekOrigin.Begin);
                SendPackets(type, elements, SocketError.Success, 70);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize - 10, stream.Position);

                SendPackets(type, elements, SocketError.Success, 70);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Assert.Equal(s_testFileSize - 10, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamAsyncMultiPart_Success(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                var elements = new[]
                {
                    new SendPacketsElement(stream, 0, 20),
                    new SendPacketsElement(stream, s_testFileSize - 10, 10),
                    new SendPacketsElement(stream, 0, 10),
                    new SendPacketsElement(stream, 10, 20),
                    new SendPacketsElement(stream, 30, 10),
                };
                stream.Seek(s_testFileSize - 10, SeekOrigin.Begin);
                SendPackets(type, elements, SocketError.Success, 70);
                Assert.Equal(s_testFileSize - 10, stream.Position);

                SendPackets(type, elements, SocketError.Success, 70);
                Assert.Equal(s_testFileSize - 10, stream.Position);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamLargeOffset_Throws(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                stream.Seek(s_testFileSize / 2, SeekOrigin.Begin);
                // Length is validated on Send
                SendPackets(type, new SendPacketsElement(stream, (long)uint.MaxValue + 11000, 1), SocketError.InvalidArgument, 0);
            }
        }

        [Theory]
        [InlineData(SocketImplementationType.APM)]
        [InlineData(SocketImplementationType.Async)]
        public void SendPacketsElement_FileStreamLargeCount_Throws(SocketImplementationType type)
        {
            using (var stream = new FileStream(TestFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                stream.Seek(s_testFileSize / 2, SeekOrigin.Begin);
                // Length is validated on Send
                SendPackets(type, new SendPacketsElement(stream, 5, 10000),
                    SocketError.InvalidArgument, 0);
            }
        }

        #endregion FileStreams

        #region Helpers

        private void SendPackets(SocketImplementationType type, SendPacketsElement element, TransmitFileOptions flags, int bytesExpected)
        {
            Assert.True(Capability.IPv6Support());

            EventWaitHandle completed = new ManualResetEvent(false);

            int port;
            using (SocketTestServer.SocketTestServerFactory(type, _serverAddress, out port))
            {
                using (Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Connect(new IPEndPoint(_serverAddress, port));
                    using (SocketAsyncEventArgs args = new SocketAsyncEventArgs())
                    {
                        args.Completed += OnCompleted;
                        args.UserToken = completed;
                        args.SendPacketsElements = new[] { element };
                        args.SendPacketsFlags = flags;

                        if (sock.SendPacketsAsync(args))
                        {
                            Assert.True(completed.WaitOne(TestSettings.PassingTestTimeout), "Timed out");
                        }
                        Assert.Equal(SocketError.Success, args.SocketError);
                        Assert.Equal(bytesExpected, args.BytesTransferred);
                    }

                    switch (flags)
                    {
                        case TransmitFileOptions.Disconnect:
                            // Sending data again throws with socket shut down error.
                            Assert.Throws<SocketException>(() => { sock.Send(new byte[1] { 01 }); });
                            break;
                        case TransmitFileOptions.ReuseSocket & TransmitFileOptions.Disconnect:
                            // Able to send data again with reuse socket flag set.
                            Assert.Equal(1, sock.Send(new byte[1] { 01 }));
                            break;
                    }
                }
            }
        }

        private void SendPackets(SocketImplementationType type, SendPacketsElement element, int bytesExpected)
        {
            SendPackets(type, new[] {element}, SocketError.Success, bytesExpected);
        }

        private void SendPackets(SocketImplementationType type, SendPacketsElement element, SocketError expectedResut, int bytesExpected)
        {
            SendPackets(type, new[] {element}, expectedResut, bytesExpected);
        }

        private void SendPackets(SocketImplementationType type, SendPacketsElement[] elements, SocketError expectedResut, int bytesExpected)
        {
            Assert.True(Capability.IPv6Support());

            EventWaitHandle completed = new ManualResetEvent(false);

            int port;
            using (SocketTestServer.SocketTestServerFactory(type, _serverAddress, out port))
            {
                using (Socket sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Connect(new IPEndPoint(_serverAddress, port));
                    using (SocketAsyncEventArgs args = new SocketAsyncEventArgs())
                    {
                        args.Completed += OnCompleted;
                        args.UserToken = completed;
                        args.SendPacketsElements = elements;

                        if (sock.SendPacketsAsync(args))
                        {
                            Assert.True(completed.WaitOne(TestSettings.PassingTestTimeout), "Timed out");
                        }
                        Assert.Equal(expectedResut, args.SocketError);
                        Assert.Equal(bytesExpected, args.BytesTransferred);
                    }
                }
            }
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            EventWaitHandle handle = (EventWaitHandle)e.UserToken;
            handle.Set();
        }

        #endregion Helpers
    }
}
