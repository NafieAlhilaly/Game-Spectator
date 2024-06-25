
using System.Net;
using System.Net.Sockets;
using System.Text;
using Backend;
using DotPulsar;
using DotPulsar.Extensions;

string[] PlayerStances = ["<", "^", ">"];
UdpClient udpClient = new();
udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 11000));
Dictionary<string, Player> Players = [];
bool IsMatchGoing = true;
const string TopicName = "persistent://public/default/fight";
const string SubscriptingName = "Logger";
Uri PulsarServiceUri = new("pulsar://localhost:6650");
await using var client = PulsarClient.Builder().ServiceUrl(PulsarServiceUri).Build();
await using var producer = client.NewProducer(Schema.String).Topic(TopicName).Create();
await using var consumer = client.NewConsumer(Schema.String)
    .SubscriptionName(SubscriptingName)
    .Topic(TopicName)
    .SubscriptionType(SubscriptionType.Exclusive)
    .InitialPosition(SubscriptionInitialPosition.Earliest)
    .Create();
Task ConsumeMessages = new(async () =>
{
    while (true)
    {
        await foreach (var message in consumer.Messages())
        {
            Console.WriteLine($"{DateTimeOffset.FromUnixTimeMilliseconds((long)message.EventTime)}: {message.Value()}");
            await consumer.Acknowledge(message);
        }
    }
});
ConsumeMessages.Start();
try
{
    IPEndPoint RemoteIpEndPoint = new(IPAddress.Any, 0);
    string message = string.Empty;
    do
    {
        byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
        message = Encoding.ASCII.GetString(receiveBytes);
        Players[RemoteIpEndPoint.Port.ToString()] = new Player(RemoteIpEndPoint, "Player " + (Players.Count + 1).ToString(), producer)
        {
            ConnectionInfo = RemoteIpEndPoint,
            Name = "Player " + (Players.Count + 1).ToString()
        };
        if (Players.Count > 0)
        {
            byte[] messageBytes1 = Encoding.ASCII.GetBytes("Waiting for other player");
            udpClient.Send(messageBytes1, messageBytes1.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
        }
    }
    while (Players.Count < 2);
    byte[] messageBytes;
    messageBytes = Encoding.ASCII.GetBytes("All players connected, starting match ... ");
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);

    Thread.Sleep(1000);
    messageBytes = Encoding.ASCII.GetBytes("3");
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);

    Thread.Sleep(1000);
    messageBytes = Encoding.ASCII.GetBytes("2");
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);

    Thread.Sleep(1000);
    messageBytes = Encoding.ASCII.GetBytes("1");
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);

    Thread.Sleep(1000);
    messageBytes = Encoding.ASCII.GetBytes("FIGHT!");
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
    udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);

    await producer.NewMessage().EventTime(DateTime.Now).Send("Match started.");
    while (IsMatchGoing)
    {
        byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
        message = Encoding.ASCII.GetString(receiveBytes);
        if (RemoteIpEndPoint.Port.ToString() != Players[Players.Keys.ToArray()[0]].ConnectionInfo.Port.ToString() && RemoteIpEndPoint.Port.ToString() != Players[Players.Keys.ToArray()[1]].ConnectionInfo.Port.ToString())
        {
            messageBytes = Encoding.ASCII.GetBytes("FULL");
            udpClient.Send(messageBytes, messageBytes.Length, RemoteIpEndPoint);
        }
        if (PlayerStances.Contains(message))
        {
            await producer.NewMessage().EventTime(DateTime.Now).Send(Players[RemoteIpEndPoint.Port.ToString()].Name + " changes stance to " + message);
            Players[RemoteIpEndPoint.Port.ToString()].BlockDirection = message;
            messageBytes = Encoding.ASCII.GetBytes(Players[RemoteIpEndPoint.Port.ToString()].Name + " changes stance to " + message);
            udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[0]].ConnectionInfo);
            udpClient.Send(messageBytes, messageBytes.Length, Players[Players.Keys.ToArray()[1]].ConnectionInfo);
        }
        if (message == "1")
        {
            await producer.NewMessage().EventTime(DateTime.Now).Send(Players[RemoteIpEndPoint.Port.ToString()].Name + " initiated attack on " + Players.First(x => x.Key != RemoteIpEndPoint.Port.ToString()).Value.Name);
            Players[RemoteIpEndPoint.Port.ToString()].Attack(Players.First(x => x.Key != RemoteIpEndPoint.Port.ToString()).Value);
            if (Players[Players.Keys.ToArray()[0]].State.GetType() == Players[Players.Keys.ToArray()[0]].DeathState.GetType()
            ||
            Players[Players.Keys.ToArray()[1]].State.GetType() == Players[Players.Keys.ToArray()[1]].DeathState.GetType())
            {
                IsMatchGoing = false;
            }
        }
    }
}
catch (Exception e)
{
    Console.WriteLine(e.ToString());
}