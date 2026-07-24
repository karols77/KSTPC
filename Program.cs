using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Reflection;

namespace KSTPC
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //Inicjalizacja programu
                Program p = new Program();

                //Testowanie
                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        case "single":
                            p.TestSingle();
                            break;
                        case "multiple":
                            p.TestMultiple();
                            break;
                        case "clients":
                            p.TestMultipleClients();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd: " + ex.Message);
                Console.ReadKey(true);
            }
        }
        KSTpc UtworzSerwer()
        {
            KSTpc serwer = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.server, 5000);
            Task ts = new Task(serwer.Connect);
            ts.Start();
            return serwer;
        }
        KSTpc UtworzKlient()
        {
            KSTpc klient = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.client, 5000);
            Task tk = new Task(klient.Connect);
            tk.Start();
            return klient;
        }
        void CzekajNaNacisniecie()
        {
            Console.WriteLine("Naciśnij dowolny klawisz, aby kontynuować...");
            Console.ReadKey(true);
        }
        #region Test pojedynczej wiadomości
        void TestSingle()
        {
            //Inicjalizacja serwera i klienta
            KSTpc? serwer = UtworzSerwer();
            KSTpc? klient = UtworzKlient();

            //Test pojedynczej wiadomości
            Console.WriteLine("Test pojedyńczej wiadomości...");
            string testMKlient = "Wiadomość od klienta";
            string testMSerwer = "Wiadomość od serwera";
            byte[] testBKlient = Encoding.UTF8.GetBytes(testMKlient);
            byte[] testBSerwer = Encoding.UTF8.GetBytes(testMSerwer);
            //Wysłanie wiadomości do serwera
            Console.WriteLine("Wysyłanie wiadomości do serwera: {0}", testMKlient);
            klient?.TakeMessage(testBKlient, klient.remoteID);
            klient?.SendMessages();
            byte[] testBSerwerOdebrana;
            while ((testBSerwerOdebrana = serwer.GetMessageB()) == null)
                Thread.Sleep(100);
            if (testBSerwerOdebrana != null)
                Console.WriteLine("Odebrano wiadomość od klienta: {0}", Encoding.UTF8.GetString(testBSerwerOdebrana));
            else
                Console.WriteLine("Nie odebrano wiadomości od klienta.");
            Console.WriteLine("Wysyłanie wiadomości do klienta: {0}", testMSerwer);
            serwer?.TakeMessage(testBSerwer, klient.remoteID);
            serwer?.SendMessages();
            byte[] testBKlientaOdebrana;
            while ((testBKlientaOdebrana = klient.GetMessageB()) == null)
                Thread.Sleep(100);
            if (testBKlientaOdebrana != null)
                Console.WriteLine("Odebrano wiadomość od serwera: {0}", Encoding.UTF8.GetString(testBKlientaOdebrana));
            else
                Console.WriteLine("Nie odebrano wiadomości od serwera.");

            //Zamknięcie połączeń
            klient?.Disconnect();
            serwer?.Disconnect();
            CzekajNaNacisniecie();
        }
        #endregion
        #region Test wielu wiadomości
        void TestMultiple()
        {
            //Inicjalizacja serwera i klienta
            KSTpc? serwer = UtworzSerwer();
            KSTpc? klient = UtworzKlient();

            //Definicja wiadomości
            List<string> testMessagesClient = new List<string>
            {
                "Wiadomość 1 od klienta",
                "Wiadomość 2 od klienta",
                "Wiadomość 3 od klienta",
                "Wiadomość 4 od klienta",
                "Wiadomość 5 od klienta"
            };
            List<string> testMessagesServer = new List<string>
            {
                "Wiadomość 1 od serwera",
                "Wiadomość 2 od serwera",
                "Wiadomość 3 od serwera",
                "Wiadomość 4 od serwera",
                "Wiadomość 5 od serwera"
            };
            Console.WriteLine("Test wielu wiadomości...");

            //Wysłanie wiadomości do serwera i klienta
            Console.WriteLine("Wysyłanie wiadomości do serwera...");
            foreach (string message in testMessagesClient)
            {
                byte[] testBKlient = Encoding.UTF8.GetBytes(message);
                klient?.TakeMessage(testBKlient, klient.remoteID);
            }
            Console.WriteLine("Wysyłanie wiadomości do klienta...");
            foreach (string message in testMessagesServer)
            {
                byte[] testBServer = Encoding.UTF8.GetBytes(message);
                serwer?.TakeMessage(testBServer, klient.remoteID);
            }
            klient?.SendMessages();
            serwer?.SendMessages();

            //Odbieranie wiadomości z serwera i klienta
            List<byte[]> bMessagesClient = klient?.GetMessagesB();
            List<byte[]> bMessagesServer = serwer?.GetMessagesB();
            if (bMessagesClient != null)
                foreach (byte[] message in bMessagesClient)
                    Console.WriteLine(
                        "Odebrano wiadomość od serwera: {0}",
                        Encoding.UTF8.GetString(message));
            else
                Console.WriteLine("Nie odebrano wiadomości od klienta.");
            if (bMessagesServer != null)
                foreach (byte[] message in bMessagesServer)
                    Console.WriteLine(
                        "Odebrano wiadomość od serwera: {0}",
                        Encoding.UTF8.GetString(message));
            else
                Console.WriteLine("Nie odebrano wiadomości od klienta.");

            //Zamknięcie połączeń
            klient?.Disconnect();
            serwer?.Disconnect();
            CzekajNaNacisniecie();
        }
        #endregion
        #region Test wielu klientów
        void TestMultipleClients()
        {
            //Test wielu klientów
            KSTpc? serwer = UtworzSerwer();
            Console.WriteLine("Test wielu klientów...");
            KSTpc?[] clients = new KSTpc[5];
            for(int i = 0; i < clients.Length; i++)
            {
                clients[i] = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.client, 5000);
                Task tk = new Task(clients[i].Connect);
                tk.Start();
            }

            //Wysłanie wiadomości do serwera i klienta
            foreach (KeyValuePair<int, Socket> klient in serwer.Clients)
            {
                string message = $"Wiadomość do klienta {klient.Key} z serwera {serwer.id}";
                byte[] testBSerwer = Encoding.UTF8.GetBytes(message);
                serwer?.TakeMessage(testBSerwer, klient.Key);
            }
            serwer?.SendMessages();
            foreach (KSTpc klient in clients)
            {
                string message = $"Wiadomość do serwera {serwer.id} z klienta {klient.id}";
                byte[] testBKlient = Encoding.UTF8.GetBytes(message);
                klient?.TakeMessage(testBKlient, klient.remoteID);
                klient?.SendMessages();
            }

            //Odbieranie wiadomości z serwera i klienta
            List<byte[]> bMessagesServer = serwer?.GetMessagesB();
            if(bMessagesServer != null)
                foreach (byte[] message in bMessagesServer)
                    Console.WriteLine(
                        "Odebrano wiadomość od klienta: {0}",
                        Encoding.UTF8.GetString(message));
            else
                Console.WriteLine("Nie odebrano wiadomości od klientów.");

            foreach(KSTpc klient in clients)
            {
                List<byte[]> bMessagesClient = klient?.GetMessagesB();
                if (bMessagesClient != null)
                    foreach (byte[] message in bMessagesClient)
                        Console.WriteLine(
                            "Odebrano wiadomość od serwera: {0}",
                            Encoding.UTF8.GetString(message));
                else
                    Console.WriteLine("Nie odebrano wiadomości od serwera.");
            }

            //Zamknięcie połączeń
            foreach (KSTpc klient in clients)
                klient?.Disconnect();
            serwer?.Disconnect();
            CzekajNaNacisniecie();
        }
        #endregion
    }
}
