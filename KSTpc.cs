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
        CloseConnection
    }
    #endregion
    public struct KSTcpMessage
    {
        //Deklaracja stałych
        public const int KSTMessageSize = 3 * sizeof(int) + sizeof(long);

        //Definicja zmiennych
        bool Prepared;
        public int Remoteid;
        public int Command;
        public DateTime Time;
        public int Size;
        public byte[] Message;
        //Metody
        public void Send(Socket socket)
        {
            if (Prepared)
                socket.Send(ConvertMessageToByte(), KSTMessageSize + Size, SocketFlags.None);
        }
        public void Prepare(int remoteid, int command, int size, byte[] message)
        {
            Prepared = true;
            Remoteid = remoteid;
            Command = command;
            Time = DateTime.Now;
            Size = size;
            if (size > 0 && message != null)
                Message = message;
        }
        public void Receive(Socket socket)
        {
            byte[] buffer = new byte[KSTMessageSize];
            socket.Receive(buffer, KSTMessageSize, SocketFlags.None);
            ConvertByteToMessage(buffer);
            if (Size > 0)
            {
                Message = new byte[Size];
                socket.Receive(Message);
            }
        }
        byte[] ConvertMessageToByte()
        {
            MemoryStream ms = new MemoryStream(KSTMessageSize + Size);
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write(Remoteid);
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
        Task _TReceive;
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
            _TReceive = new Task(Receive);
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
                }
            }
            else if (_mode == (int)KSTpcMode.client)
            {
                await _socket.ConnectAsync(_ipaddr, _port);
                Console.WriteLine("[0x{0:x8}] Klient połączył się z serwerem", _socket.GetHashCode());
                //KSTcpMessage message = new KSTcpMessage();
                ReceiveMessages();
                //message.Receive(_socket);
                //_remoteid = message.Remoteid;
                Console.WriteLine(
                    "[0x{0:x8}] Zdalny numer 0x{1:x8} z serwera",
                    _socket.GetHashCode(),
                    _remoteid);
            }
        }
        public void Disconnect()
        {
            if (_mode == (int)KSTpcMode.server)
            {
                Console.WriteLine("[0x{0:x8}] Rozłączanie serwera", _socket.GetHashCode());
                lock (_lockvar)
                {
                    _work = false;
                    Console.WriteLine(
                        "[0x{0:x8}] Rozłączanie z klientami...",
                        _socket.GetHashCode());
                    lock (_lockvar)
                    {
                        foreach (KeyValuePair<int, Socket> client in _clients)
                            if (client.Value.Connected)
                                client.Value.Close();
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
                if (_socket.Connected)
                    _socket.Close();
            }
        }
        void SetRemoteId(KSTcpMessage message)
        {
            _remoteid = message.Remoteid;
        }
        #endregion
        #region Odbieranie wiadomości
        void Receive()
        {
            if (_work)
            {
                KSTcpMessage message = new KSTcpMessage();
                message.Receive(_socket);
                switch (message.Command)
                {
                    case (int)KSTCommand.CloseConnection:
                        Disconnect();
                        break;
                    case (int)KSTCommand.SetRemoteid:
                        SetRemoteId(message);
                        break;
                    case (int)KSTCommand.SendMessage:
                        AppendMessage(message);
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
        public void ReceiveMessages()
        {
            if (_work)
            {
                _TReceive.Start();
                _TReceive.Wait();
            }
        }
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
