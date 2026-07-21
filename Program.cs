using System.Net;
using System.Text;
using System.Net.Sockets;

namespace KSTPC
{
    internal class Program
    {
        KSTpc? klient, serwer;
        static void Main(string[] args)
        {
            try 
            {
                Program p = new Program();

                //Testowe uruchomienie serwera i klienta
                p.UtworzSerwer();
                p.UtworzKlient();

                Console.WriteLine("Naciśnij dowolny klawisz...");
                Console.ReadKey(true);

                //Testowanie
                switch(string args[0])
                {
                    case "single":
                        p.TestSingle();
                        break
                    case "multiple":
                        p.TestMultiple();
                        break;
                    case: "clients":
                        p.TestMultipleClients();
                        break;
                }
                Console.ReadKey(true);

                //Zamknięcie połączeń
                p.klient.Disconnect();
                p.serwer.Disconnect();

                Console.WriteLine("Naciśnij dowolny klawisz...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd: " + ex.Message);
                Console.ReadKey(true);
            }
        }
        void UtworzSerwer()
        {
            serwer = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.server, 5000);
            Task ts = new Task(serwer.Connect);
            ts.Start();
        }
        void UtworzKlient()
        {
            klient = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.client, 5000);
            Task tk = new Task(klient.Connect);
            tk.Start();
        }
        #region Test pojedynczej wiadomości
        void TestSingle()
        {
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
            while((testBSerwerOdebrana = serwer.GetMessageB()) == null)
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
        }
        #endregion
        #region Test wielu wiadomości
        void TestMultiple()
        {
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
            foreach (var message in testMessagesClient)
            {
                byte[] testBKlient = Encoding.UTF8.GetBytes(message);
                klient?.TakeMessage(testBKlient, klient.remoteID);
            }
            Console.WriteLine("Wysyłanie wiadomości do klienta...");
            foreach (var message in testMessagesServer)
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

        }
        #endregion
        #region Test wielu klientów
        void TestMultipleClients()
        {

        }
        #endregion
    }
}
