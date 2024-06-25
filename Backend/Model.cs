using System.Net;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;

namespace Backend
{
    public class Player
    {
        public PlayerState BlockState = new BlockState();
        public PlayerState AttackState = new AttackState();
        public PlayerState DeathState = new DeathState();
        public string Name { get; set; }
        public int Health { get; set; } = 100;
        public required IPEndPoint ConnectionInfo { get; set; }
        public string BlockDirection { get; set; } = "<";
        public PlayerState State;
        private IProducer<string> Producer { get; set; }

        public Player(IPEndPoint ConnectionInfo, string Name, IProducer<string> Producer)
        {
            State = BlockState;
            this.Name = Name;
            this.ConnectionInfo = ConnectionInfo;
            this.Producer = Producer;
        }
        public void Attack(Player enemy)
        {
            if (State.GetType() != AttackState.GetType())
            {
                State = AttackState;
                new Task(async () =>
                {
                    Thread.Sleep(1000);
                    if (enemy.BlockDirection == this.BlockDirection)
                    {
                        await Producer.NewMessage().EventTime(DateTime.Now).Send($"Attack from {Name} blocked by {enemy.Name}");
                    }
                    else
                    {
                        await Producer.NewMessage().EventTime(DateTime.Now).Send($"Attack from {Name} landed on {enemy.Name}");
                        enemy.Health -= 20;
                        await Producer.NewMessage().EventTime(DateTime.Now).Send($"{enemy.Name}'s health is now {enemy.Health}");
                        if (enemy.Health <= 0)
                        {
                            enemy.State = DeathState;
                            await Producer.NewMessage().EventTime(DateTime.Now).Send($"{enemy.Name} defeated!");
                        }
                    }
                    State = BlockState;
                }).Start();
            }
        }
    }

    public abstract class PlayerState { }

    public class BlockState : PlayerState { }
    public class AttackState : PlayerState { }
    public class DeathState : PlayerState { }
}