using CoreLib;
using Google.Protobuf;
using System.Text;
using UplayKit;
using UplayKit.Connection;

namespace DenuvoTicket;

internal class Program
{
    static void Main(string[] args)
    {
        string tokenFile = ParameterLib.GetParameter(args, "-tokenfile", string.Empty);
        string denuvoToken = ParameterLib.GetParameter(args, "-denuvotoken", string.Empty);
        string denuvoRequestToken = ParameterLib.GetParameter(args, "-denuvorequesttoken", string.Empty);
        uint appId = ParameterLib.GetParameter<uint>(args, "-denuvoappid", 0);
        var login = LoginLib.LoginArgs_CLI(args);
        if (login == null)
        {
            Console.WriteLine("Login was wrong! :(");
            Environment.Exit(1);
        }
        /*
        Logs.Log_Switch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
        Serilog.Log.Logger = Logs.CreateFileLog();
        */
        DemuxSocket demuxSocket = new();
        demuxSocket.VersionCheck();
        demuxSocket.PushVersion();
        if (!demuxSocket.Authenticate(login.Ticket))
        {
            Console.WriteLine("Authenticate false");
            Environment.Exit(1);
        }
        OwnershipConnection ownershipConnection = new(demuxSocket, login.Ticket, login.SessionId);
        var ownership = ownershipConnection.GetOwnedGames(true);
        DenuvoConnection denuvoConnection = new(demuxSocket);
        if (!string.IsNullOrEmpty(tokenFile))
        {
            var token_readed = File.ReadAllText(tokenFile).Split("|");
            denuvoRequestToken = token_readed[0];
            appId = uint.Parse(token_readed[1]);
        }

        if (appId == 0)
        {
            Console.WriteLine("Please enter your appId!");
            appId = uint.Parse(Console.ReadLine()!);
        }

        var ownerApp = ownership.Find(x => x.ProductId == appId);

        if (ownerApp == null)
        {
            Console.WriteLine("You are not owning this App!");
            Environment.Exit(1);
        }

        var ownedAssoc = ownership.Where(x => x.Owned == true && ownerApp.ProductAssociations.Contains(x.ProductId)).Select(x => x.ProductId).ToList();

        Console.WriteLine("Your owned product Associations: " + string.Join(", ", ownedAssoc));

        if (ParameterLib.HasParameter(args, "-nogen"))
        {
            Environment.Exit(1);
        }

        var (Token, _) = ownershipConnection.GetOwnershipToken(appId);
        if (string.IsNullOrEmpty(Token))
        {
            Console.WriteLine("you not own this appid");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(denuvoToken))
        {
            if (string.IsNullOrEmpty(denuvoRequestToken))
            {
                Console.WriteLine("Please enter your denuvo ticket request!");

                denuvoRequestToken = Console.ReadLine()!;
            }
            var base64token = ByteString.FromBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(denuvoRequestToken)));
            var gametoken = denuvoConnection.GetGameToken(Token, base64token);

            if (!gametoken.HasValue)
                Environment.Exit(1);

            if (gametoken.Value.result == Uplay.DenuvoService.Rsp.Types.Result.Success && gametoken.Value.response != null)
            {
                Console.WriteLine("GameToken:");
                denuvoToken = Encoding.UTF8.GetString(Convert.FromBase64String(gametoken.Value.response.GameToken.ToBase64()));
                Console.WriteLine(denuvoToken);
            }
            else
            {
                Console.WriteLine(gametoken.Value);
            }
        }

        var base64denuvotoken = ByteString.FromBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(denuvoToken)));

        var ownershiplist = denuvoConnection.GetOwnershipListToken(appId, base64denuvotoken, [.. ownedAssoc]);

        if (!ownershiplist.HasValue)
            Environment.Exit(1);

        if (ownershiplist.Value.result == Uplay.DenuvoService.Rsp.Types.Result.Success && ownershiplist.Value.response != null)
        {
            Console.WriteLine("OwnershipListToken:");
            Console.WriteLine(Encoding.UTF8.GetString(Convert.FromBase64String(ownershiplist.Value.response.OwnershipListToken.ToBase64())));

        }
        else
        {
            Console.WriteLine(ownershiplist.Value);
        }

        Console.ReadLine(); // users who doesn't use console to start it.
    }
}
