namespace roulette;

class Program
{
    private static volatile bool bettingOpen;
    private static volatile bool keepRunning;

    static void Main(string[] args)
    {
        paint_screen();

        const int rounds = 5;
        const decimal minBet = 5;
        const decimal maxBet = 25;
        const int maxBetsPerWindowPerPlayer = 2;

        var game = new Game();
        var playerSetups = new[]
        {
            (Player: new Player("Player 1", 100), Strategy: BettingStrategy.Conservative),
            (Player: new Player("Player 2", 100), Strategy: BettingStrategy.Balanced),
            (Player: new Player("Player 3", 100), Strategy: BettingStrategy.Aggressive)
        };
        var threads = new List<Thread>();
        var bettingOpenSignal = new ManualResetEventSlim(false);
        var roundClosedBarrier = new Barrier(participantCount: playerSetups.Length + 1);

        keepRunning = true;
        bettingOpen = false;

        foreach (var setup in playerSetups)
        {
            game.AddPlayer(setup.Player);

            var playerThread = new Thread(() =>
            {
                while (keepRunning)
                {
                    bettingOpenSignal.Wait();

                    if (!keepRunning)
                    {
                        break;
                    }

                    var betsPlacedThisWindow = 0;

                    while (bettingOpen)
                    {
                        if (betsPlacedThisWindow >= maxBetsPerWindowPerPlayer)
                        {
                            break;
                        }

                        var placedBet = game.TryPlaceSingleBet(setup.Player, setup.Strategy, minBet, maxBet);

                        if (placedBet)
                        {
                            betsPlacedThisWindow++;
                        }

                        Thread.Sleep(Random.Shared.Next(40, 100));
                    }

                    roundClosedBarrier.SignalAndWait();
                }
            })
            {
                Name = setup.Player.Name
            };

            threads.Add(playerThread);
        }

        foreach (var playerThread in threads)
        {
            playerThread.Start();
        }

        for (var round = 1; round <= rounds; round++)
        {
            Console.WriteLine($"--- Round {round} ---");
            Console.WriteLine("GAME: betting is open");

            bettingOpen = true;
            bettingOpenSignal.Set();
            Thread.Sleep(900);

            Console.WriteLine("GAME: no more bets");
            bettingOpen = false;
            bettingOpenSignal.Reset();

            roundClosedBarrier.SignalAndWait();

            game.RollBallAndPayout();

            foreach (var message in game.GetEventLog())
            {
                Console.WriteLine(message);
            }

            game.ClearEventLog();
            Console.WriteLine();
        }

        keepRunning = false;
        bettingOpenSignal.Set();

        foreach (var playerThread in threads)
        {
            playerThread.Join();
        }

        Console.WriteLine();
        Console.WriteLine("All player threads have finished.");
        Console.WriteLine();
        Console.WriteLine("Final standings:");

        foreach (var standing in game.GetStandings())
        {
            Console.WriteLine($"- {standing.Name}: {standing.Chips} chips");
        }
    }

    static void paint_screen()
    {
        Console.Clear();
        Console.WriteLine("========================");
        Console.WriteLine("      ROULETTE GAME     ");
        Console.WriteLine("========================");
        Console.WriteLine("Welcome to the table.");
        Console.WriteLine("Three players will play on three threads.");
        Console.WriteLine("Players keep running while the game controls betting windows.");
        Console.WriteLine("Each player can place up to 8 bets per open window.");
        Console.WriteLine("Supported bets: Straight, Red/Black, Even/Odd.");
        Console.WriteLine("Game says 'betting is open' and later 'no more bets' each round.");
        Console.WriteLine();
    }
}
