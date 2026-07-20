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

                p.TestSingle();

                //Zamknięcie połączeń
                p.klient.Disconnect();
                p.serwer.Disconnect();

                Console.WriteLine("Naciśnij dowolny klawisz...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd: " + ex.Message);
                Console.ReadKey(true);
            }
        }
        void UtworzSerwer()
        {
            serwer = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.server, 80);
            Thread ts = new Thread(serwer.Connect);
            ts.Start();
        }
        void UtworzKlient()
        {
            klient = new KSTpc(IPAddress.Loopback, (int)KSTpcMode.client, 80);
            Thread tk = new Thread(klient.Connect);
            tk.Start();
        }
        void TestSingle()
        {
            Console.WriteLine("Test pojedyńczej wiadomości...");
            string testMKlient = "Wiadomość od klienta";
            string testMSerwer = "Wiadomość od serwera";
            byte[] testBKlient = Encoding.UTF8.GetBytes(testMKlient);
            byte[] testBSerwer = Encoding.UTF8.GetBytes(testMSerwer);
            //Wysłanie wiadomości do serwera
            Console.WriteLine("Wysyłanie wiadomości do serwera: {0}", testMKlient);
            klient.TakeMessage(testBKlient, 0);
            klient.SendMessages();
            serwer.ReceiveMessages();
            Console.WriteLine("Odebrano wiadomość od klienta: {0}", Encoding.UTF8.GetString(serwer.GetMessageB()));
        }
    }
}
