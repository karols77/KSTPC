using System.Net;
using System.Net.Sockets;

namespace KSTPC
{
    internal class Program
    {
        KSTpc klient, serwer;
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
    }
}
