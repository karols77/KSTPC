/*
 * Utworzone przez SharpDevelop.
 * Użytkownik: k.szewczyk
 * Data: 14.07.2026
 * Godzina: 07:40
 * 
 * Do zmiany tego szablonu użyj Narzędzia | Opcje | Kodowanie | Edycja Nagłówków Standardowych.
 */
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace KSTPC
{
    #region Wyliczania
    enum KSTpcMode
    {
        server,
        client
    }
    enum KSTCommand
    {
        None,
        SetRemoteid,
        SendMessage,
        FwdMessage,
        CloseConnection
    }
    #endregion
    public struct KSTcpMessage
    {
        //Deklaracja stałych
        public const int KSTMessageSize = 4 * sizeof(int) + sizeof(long);

        //Definicja zmiennych
        bool Prepared;
        public int Remoteid;
        public int Fwdid;
        public int Command;
        public DateTime Time;
        public int Size;
        public byte[] Message;
        //Metody
        public bool Send(Socket socket)
        {
            if (Prepared)
            {
                try
                {
                    int sent = 0;
                    byte[] buffer = ConvertMessageToByte();
                    while (sent < KSTMessageSize + Size)
                    {
                        int n = socket.Send(buffer, sent, KSTMessageSize + Size - sent, SocketFlags.None);
                        if (n == 0) // połączenie zostało zamknięte
                            return false;
                        sent += n;
                    }
                    Console.WriteLine("[0x{0:x8}] Wysłano wiadomość do 0x{1:x8}", socket.GetHashCode(), Remoteid);
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
            return false;
        }
        public void Prepare(int remoteid, int command, int size, byte[] message)
        {
            Prepared = true;
            Remoteid = remoteid;
            Fwdid = 0;
            Command = command;
            Time = DateTime.Now;
            Size = size;
            if (size > 0 && message != null)
                Message = message;
        }
        public void Prepare(int remoteid, int fwdid, int command, int size, byte[] message)
        {
            Prepared = true;
            Remoteid = remoteid;
            Fwdid = fwdid;
            Command = command;
            Time = DateTime.Now;
            Size = size;
            if (size > 0 && message != null)
                Message = message;
        }
        public bool Receive(Socket socket)
        {
            try
            {
                byte[] buffer = new byte[KSTMessageSize];
                int received = 0;
                while (received < KSTMessageSize)
                {
                    int n = socket.Receive(buffer, received, KSTMessageSize - received, SocketFlags.None);
                    if (n == 0) // połączenie zostało zamknięte
                        return false;
                    received += n;
                }

                ConvertByteToMessage(buffer);

                if (Size > 0)
                {
                    Message = new byte[Size];
                    received = 0;
                    while (received < Size)
                    {
                        int n = socket.Receive(Message, received, Size - received, SocketFlags.None);
                        if (n == 0)
                            return false;
                        received += n;
                    }
                }
                Console.WriteLine("[0x{0:x8}] Odebrano wiadomość od 0x{1:x8}", socket.GetHashCode(), Remoteid);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }
        byte[] ConvertMessageToByte()
        {
            MemoryStream ms = new MemoryStream(KSTMessageSize + Size);
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(Remoteid);
            bw.Write(Fwdid);
            bw.Write(Command);
            bw.Write(Time.ToBinary());
            bw.Write(Size);
            if (Size > 0)
                bw.Write(Message, 0, Size);
            return ms.GetBuffer();
        }
        void ConvertByteToMessage(byte[] buffer)
        {
            MemoryStream ms = new MemoryStream(buffer);
            BinaryReader br = new BinaryReader(ms);
            Prepared = true;
            Remoteid = br.ReadInt32();
            Fwdid = br.ReadInt32();
            Command = br.ReadInt32();
            Time = DateTime.FromBinary(br.ReadInt64());
            Size = br.ReadInt32();
        }
    }
    public class KSTpc
    {
        #region Deklaracja zmiennych
        Socket _socket;
        IPEndPoint _endpoint;
        IPAddress _ipaddr;
        Dictionary<int, Socket> _clients;
        Task _TSend;
        public IReadOnlyDictionary<int, Socket> Clients
        {
            get
            {
                return _clients;
            }
        }
        int _port;
        int _mode;
        bool _work;
        object _lockvar;
        int _id;
        int _remoteid;
        bool _reading;
        public int remoteID => _remoteid;
        Queue<KSTcpMessage> _toRead;
        Queue<KSTcpMessage> _toWrite;
        #endregion
        #region Kostrukcja i inicjacja
        public KSTpc(IPAddress ipaddr, int mode, int port)
        {
            _work = true;
            _mode = mode;
            _ipaddr = ipaddr;
            _port = port;
            _clients = new Dictionary<int, Socket>();
            _lockvar = new object();
            _toRead = new Queue<KSTcpMessage>();
            _toWrite = new Queue<KSTcpMessage>();
            _TSend = new Task(Send);
            _reading = false;
            try
            {
                _socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);
                if (_mode == (int)KSTpcMode.server)
                    CreateServer();
                else if (_mode == (int)KSTpcMode.client)
                    CreateClient();
                else
                    throw new Exception("Niewłaściwy tryb");
            }
            catch (Exception e)
            {
                Console.WriteLine("Błąd: {0}", e.Message);
            }
        }
        void CreateServer()
        {
            Console.WriteLine("[Serwer] Tworzenie serwera...");
            _endpoint = new IPEndPoint(_ipaddr, _port);
            _socket.Bind(_endpoint);
            _socket.Listen(10);
        }
        void CreateClient()
        {
            Console.WriteLine("[Klient] Tworzenie klienta...");
        }
        #endregion
        #region Dodawanie klienta i zamykanie
        async public void Connect()
        {
            _id = this.GetHashCode();
            if (_mode == (int)KSTpcMode.server)
            {
                _remoteid = 0;
                while (_work)
                {
                    Socket client = await _socket.AcceptAsync();
                    Console.WriteLine(
                        "[0x{0:x8}] Serwer połączył klienta: 0x{1:x8}",
                        _socket.GetHashCode(),
                        client.GetHashCode());
                    KSTcpMessage message = new KSTcpMessage();
                    message.Prepare(client.GetHashCode(),
                        (int)KSTCommand.SetRemoteid, 0, null);
                    message.Send(client);
                    lock (_lockvar)
                    {
                        _clients.Add(client.GetHashCode(), client);
                    }
                    Task.Run(() => Receive(client));
                }
            }
            else if (_mode == (int)KSTpcMode.client)
            {
                await _socket.ConnectAsync(_ipaddr, _port);
                Console.WriteLine("[0x{0:x8}] Klient połączył się z serwerem", _socket.GetHashCode());
                if (!_reading)
                {
                    Task.Run(() => Receive(_socket));
                    _reading = true;
                }
            }
        }
        public void Disconnect(int remoteId = 0)
        {
            if (_mode == (int)KSTpcMode.server)
            {
                Console.WriteLine("[0x{0:x8}] Rozłączanie serwera", _socket.GetHashCode());
                lock (_lockvar)
                {
                    Console.WriteLine(
                        "[0x{0:x8}] Rozłączanie z klientami...",
                        _socket.GetHashCode());
                    if (remoteId != 0 && _clients.ContainsKey(remoteId))
                    {
                        try
                        {
                            Console.WriteLine(
                                "[0x{0:x8}] Rozłączanie klienta 0x{1:x8}",
                                _socket.GetHashCode(),
                                remoteId);
                            Socket client = _clients[remoteId];
                            if (client != null)
                            {
                                try { client.Shutdown(SocketShutdown.Both); } catch { }
                                client.Close();
                            }
                        }
                        catch { /* ignoruj wyjątki przy zamykaniu klienta */ }
                    }
                    else if (remoteId == 0)
                    {
                        foreach (KeyValuePair<int, Socket> client in _clients)
                        {
                            Console.WriteLine("Rozłączenie klientów...");
                            _work = false;
                            try
                            {
                                if (client.Value != null)
                                {
                                    try { client.Value.Shutdown(SocketShutdown.Both); } catch { }
                                    client.Value.Close();
                                    _clients.Remove(client.Key);
                                }
                            }
                            catch { /* ignoruj wyjątki przy zamykaniu klienta */ }
                        }
                    }
                }
            }
            else if (_mode == (int)KSTpcMode.client)
            {
                Console.WriteLine("[0x{0:x8}] Rozłączanie klienta", _remoteid);
                lock (_lockvar)
                {
                    _work = false;
                }
                try
                {
                    if (_socket != null)
                    {
                        try 
                        {
                            Console.WriteLine("[0x{0:x8}] Wysyłanie wiadomości zamknięcia połączenia", _socket.GetHashCode());
                            KSTcpMessage message = new KSTcpMessage();
                            message.Prepare(_remoteid, (int)KSTCommand.CloseConnection, 0, null);
                            message.Send(_socket);
                            _socket.Shutdown(SocketShutdown.Both);
                        }
                        catch { }
                        _socket.Close();
                    }
                }
                catch { /* ignoruj */ }
            }
        }
        void SetRemoteId(KSTcpMessage message)
        {
            _remoteid = message.Remoteid;
            Console.WriteLine(
                "[0x{0:x8}] Zdalny numer 0x{1:x8} z serwera",
                _socket.GetHashCode(),
                _remoteid);
        }
        #endregion
        #region Odbieranie wiadomości
        void Receive(Socket socket)
        {
            while (_work)
            {
                KSTcpMessage message = new KSTcpMessage();
                if (!socket.Connected || !message.Receive(socket))
                {
                    Thread.Sleep(100);
                    continue;
                }
                switch (message.Command)
                {
                    case (int)KSTCommand.CloseConnection:
                        Disconnect(message.Remoteid);
                        break;
                    case (int)KSTCommand.SetRemoteid:
                        SetRemoteId(message);
                        break;
                    case (int)KSTCommand.SendMessage:
                        AppendMessage(message);
                        break;
                    case (int)KSTCommand.FwdMessage:
                        if (_mode == (int)KSTpcMode.server)
                            FwdMessage(message);
                        else
                            SendMessage(message);
                        break;
                }
            }
        }
        void AppendMessage(KSTcpMessage message)
        {
            lock (_toRead)
            {
                _toRead.Enqueue(message);
            }
        }
        void FwdMessage(KSTcpMessage message)
        {
                if (_clients.ContainsKey(message.Fwdid))
                {
                    //Skorygować Remoteid na Fwdid
                    message.Send(_clients[message.Fwdid]);
                }
        }
        void Send()
        {
            if (!_work || _toWrite.Count == 0)
                return;
            while (_work && _toWrite.Count > 0)
            {
                KSTcpMessage message;
                lock (_toWrite)
                {
                    message = _toWrite.Dequeue();
                }
                SendMessage(message);
            }
            return;
        }
        void SendMessage(KSTcpMessage message)
        {

            //Umieścić obsługę serwera i klienta				
            if (_mode == (int)KSTpcMode.server)
                message.Send(_clients[message.Remoteid]);
            else if (_mode == (int)KSTpcMode.client)
                message.Send(_socket);
        }
        //public void ReceiveMessages()
        //{
        //    if (_work)
        //    {
        //        _TReceive.Start();
        //        _TReceive.Wait();
        //    }
        //}
        public void SendMessages()
        {
            if (_work)
            {
                _TSend.Start();
                _TSend.Wait();
            }
        }
        #endregion
        #region Dodawanie i pobieranie wiadomości
        public List<KSTcpMessage> GetMessages()
        {
            if (_toRead.Count == 0)
                return null;
            List<KSTcpMessage> messages = new List<KSTcpMessage>();
            lock (_toRead)
            {
                while (_toRead.Count > 0)
                    messages.Add(_toRead.Dequeue());
            }
            return messages;
        }
        public List<byte[]> GetMessagesB()
        {
            if (_toRead.Count == 0)
                return null;
            List<byte[]> messages = new List<byte[]>();
            List<KSTcpMessage> lMessages = GetMessages();
            foreach (KSTcpMessage message in lMessages)
                messages.Add(message.Message);
            return messages;
        }
        public byte[] GetMessageB()
        {
            if (_toRead.Count == 0)
                return null;
            KSTcpMessage message;
            lock (_toRead)
            {
                message = _toRead.Dequeue();
            }
            return message.Message;
        }
        public int TakeMessages(List<KSTcpMessage> lMessages)
        {
            int count = 0;
            lock (_toWrite)
            {
                foreach (KSTcpMessage message in lMessages)
                    _toWrite.Enqueue(message);
                count = _toWrite.Count;
            }
            return count;
        }
        public int TakeMessages(List<byte[]> messages, int remoteId)
        {
            int count = 0;
            List<KSTcpMessage> lMessages = new List<KSTcpMessage>();
            foreach (byte[] message in messages)
            {
                KSTcpMessage lMessage = new KSTcpMessage();
                lMessage.Prepare(remoteId, (int)KSTCommand.SendMessage, message.Length, message);
                lMessages.Add(lMessage);
            }
            return TakeMessages(lMessages);
        }
        public int TakeMessage(byte[] message, int remoteId)
        {
            KSTcpMessage lMessage = new KSTcpMessage();
            lMessage.Prepare(remoteId, (int)KSTCommand.SendMessage, message.Length, message);
            List<KSTcpMessage> lMessages = new List<KSTcpMessage>();
            lMessages.Add(lMessage);
            return TakeMessages(lMessages);
        }
        #endregion
    }
}
