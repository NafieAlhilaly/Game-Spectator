using System.Net;
using System.Net.Sockets;
using System.Text;
using DotPulsar;
using DotPulsar.Extensions;

string[] PlayerStances = ["<", "^", ">"];
const string TopicName = "persistent://public/default/fight";
Uri PulsarServiceUri = new("pulsar://localhost:6650");
var client = PulsarClient.Builder().ServiceUrl(PulsarServiceUri).Build();
var reader = client.NewReader(Schema.String)
                   .StartMessageId(MessageId.Earliest)
                   .Topic(TopicName)
                   .Create();

string PlayerStance = PlayerStances[0];
UdpClient udpClient = new();
udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
udpClient.Connect("localhost", 11000);
bool IsSpectating = false;
Task WatchServerMessages = new(() =>
      {
          string message = string.Empty;
          IPEndPoint RemoteIpEndPoint = new(IPAddress.Any, 0);
          while (true)
          {
              byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
              message = Encoding.ASCII.GetString(receiveBytes);
              Console.WriteLine(message);
          }
      });
Task t2 = new(async () =>
{
    while (true)
    {
        await foreach (var QMessage in reader.Messages())
        {
            Console.WriteLine($"{DateTimeOffset.FromUnixTimeMilliseconds((long)QMessage.EventTime)}: {QMessage.Value()}");
        }
    }
});
try
{
    string message = String.Empty;
    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
    Console.WriteLine("Connecting to server ... ");
    Byte[] sendBytes = Encoding.ASCII.GetBytes("hi");
    udpClient.Send(sendBytes, sendBytes.Length);
    Console.WriteLine("Connected");
    string firstMessage = String.Empty;
    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
    message = Encoding.ASCII.GetString(receiveBytes);
    if (message == "FULL")
    {
        Console.WriteLine("You are spectating");
        IsSpectating = true;
        t2.Start();
    }
    else
    {
        WatchServerMessages.Start();
    }
    while (!IsSpectating)
    {
        message = Console.ReadLine();
        if (PlayerStances.Contains(message))
        {
            PlayerStance = message;
            Console.WriteLine("Your stance now " + PlayerStance);
        }
        if (message == "1")
        {
            sendBytes = Encoding.ASCII.GetBytes(message);
            udpClient.Send(sendBytes, sendBytes.Length);
            Thread.Sleep(2000);
        }
        else
        {
            sendBytes = Encoding.ASCII.GetBytes(message);
            udpClient.Send(sendBytes, sendBytes.Length);
        }


    };
    while (IsSpectating)
    {

    };
}
catch (Exception e)
{
    Console.WriteLine(e.ToString());
}