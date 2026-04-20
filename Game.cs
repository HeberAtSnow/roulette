namespace roulette;

public enum BettingStrategy
{
    Conservative,
    Balanced,
    Aggressive
}

public enum BetKind
{
    Straight,
    Red,
    Black,
    Even,
    Odd
}

public readonly record struct BetSlot(BetKind Kind, int Number = -1);

public readonly record struct RouletteBet(Player Player, BetSlot Slot, decimal Amount, BettingStrategy Strategy);

public class Game
{
    private const int MinNumber = 0;
    private const int MaxNumber = 36;
    private const decimal StraightBonusMultiplier = 35m;
    private const decimal EvenMoneyBonusMultiplier = 1m;
    private static readonly HashSet<int> RedNumbers = new()
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18,
        19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    private readonly object boardLock = new();
    private readonly object eventsLock = new();
    private readonly object playersLock = new();
    private readonly Dictionary<BetSlot, List<RouletteBet>> betBoard = new();
    private readonly List<string> eventLog = new();
    private readonly List<Player> players = new();

    public Game()
    {
        for (var number = MinNumber; number <= MaxNumber; number++)
        {
            betBoard[new BetSlot(BetKind.Straight, number)] = new List<RouletteBet>();
        }

        betBoard[new BetSlot(BetKind.Red)] = new List<RouletteBet>();
        betBoard[new BetSlot(BetKind.Black)] = new List<RouletteBet>();
        betBoard[new BetSlot(BetKind.Even)] = new List<RouletteBet>();
        betBoard[new BetSlot(BetKind.Odd)] = new List<RouletteBet>();
    }

    public IReadOnlyList<Player> Players
    {
        get
        {
            lock (playersLock)
            {
                return players.ToList().AsReadOnly();
            }
        }
    }

    public void AddPlayer(Player player)
    {
        if (player is null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        lock (playersLock)
        {
            players.Add(player);
        }
    }

    public int SpinWheel()
    {
        return Random.Shared.Next(MinNumber, MaxNumber + 1);
    }

    public bool TryPlaceSingleBet(Player player, BettingStrategy strategy, decimal minBet, decimal maxBet)
    {
        if (!IsRegisteredPlayer(player))
        {
            throw new InvalidOperationException("Player must join the game before betting.");
        }

        if (player.Chips < minBet)
        {
            return false;
        }

        var betAmount = DetermineBetAmount(player, minBet, maxBet, strategy);

        if (!player.PlaceBet(betAmount))
        {
            return false;
        }

        var slot = DetermineBetSlot(strategy);
        var rouletteBet = new RouletteBet(player, slot, betAmount, strategy);
        var threadName = Thread.CurrentThread.Name ?? player.Name;

        lock (boardLock)
        {
            betBoard[slot].Add(rouletteBet);
        }

        AddEvent($"[{threadName}] ({strategy}) bet {rouletteBet.Amount} on {DescribeSlot(rouletteBet.Slot)}.");
        return true;
    }

    public int RollBallAndPayout()
    {
        var winningNumber = SpinWheel();
        var winningSlots = GetWinningSlots(winningNumber);
        var winningBets = new List<RouletteBet>();

        lock (boardLock)
        {
            foreach (var slot in winningSlots)
            {
                winningBets.AddRange(betBoard[slot]);
            }
        }

        AddEvent($"Ball rolled: {winningNumber} ({DescribeWinningAttributes(winningNumber)})");

        if (winningBets.Count == 0)
        {
            AddEvent("No winning bets this round.");
        }
        else
        {
            foreach (var winningBet in winningBets)
            {
                var bonus = winningBet.Amount * GetBonusMultiplier(winningBet.Slot.Kind);
                var payout = winningBet.Amount + bonus;

                winningBet.Player.Award(payout);
                AddEvent($"[{winningBet.Player.Name}] wins on {DescribeSlot(winningBet.Slot)} and receives {payout} ({winningBet.Amount} stake + {bonus} bonus).");
            }
        }

        ClearBoard();

        return winningNumber;
    }

    private void ClearBoard()
    {
        lock (boardLock)
        {
            foreach (var number in betBoard.Keys)
            {
                betBoard[number].Clear();
            }
        }
    }

    private bool IsRegisteredPlayer(Player player)
    {
        lock (playersLock)
        {
            return players.Contains(player);
        }
    }

    private BetSlot DetermineBetSlot(BettingStrategy strategy)
    {
        return strategy switch
        {
            BettingStrategy.Conservative => Random.Shared.Next(0, 2) == 0
                ? new BetSlot(BetKind.Red)
                : new BetSlot(BetKind.Black),
            BettingStrategy.Balanced => Random.Shared.Next(0, 2) == 0
                ? new BetSlot(BetKind.Even)
                : new BetSlot(BetKind.Odd),
            BettingStrategy.Aggressive => new BetSlot(BetKind.Straight, SpinWheel()),
            _ => new BetSlot(BetKind.Straight, SpinWheel())
        };
    }

    private static decimal DetermineBetAmount(Player player, decimal minBet, decimal maxBet, BettingStrategy strategy)
    {
        var upperBound = Math.Min(player.Chips, maxBet);

        return strategy switch
        {
            BettingStrategy.Conservative => Math.Min(upperBound, minBet),
            BettingStrategy.Balanced => Math.Min(upperBound, Math.Max(minBet, Math.Floor(player.Chips * 0.12m))),
            BettingStrategy.Aggressive => Math.Min(upperBound, Math.Max(minBet, Math.Floor(player.Chips * 0.20m))),
            _ => minBet
        };
    }

    private static decimal GetBonusMultiplier(BetKind kind)
    {
        return kind switch
        {
            BetKind.Straight => StraightBonusMultiplier,
            BetKind.Red => EvenMoneyBonusMultiplier,
            BetKind.Black => EvenMoneyBonusMultiplier,
            BetKind.Even => EvenMoneyBonusMultiplier,
            BetKind.Odd => EvenMoneyBonusMultiplier,
            _ => 0m
        };
    }

    private static List<BetSlot> GetWinningSlots(int winningNumber)
    {
        var winningSlots = new List<BetSlot> { new(BetKind.Straight, winningNumber) };

        if (winningNumber == 0)
        {
            return winningSlots;
        }

        winningSlots.Add(IsRed(winningNumber) ? new BetSlot(BetKind.Red) : new BetSlot(BetKind.Black));
        winningSlots.Add(winningNumber % 2 == 0 ? new BetSlot(BetKind.Even) : new BetSlot(BetKind.Odd));

        return winningSlots;
    }

    private static bool IsRed(int number)
    {
        return RedNumbers.Contains(number);
    }

    private static string DescribeSlot(BetSlot slot)
    {
        return slot.Kind == BetKind.Straight
            ? $"number {slot.Number}"
            : slot.Kind.ToString();
    }

    private static string DescribeWinningAttributes(int winningNumber)
    {
        if (winningNumber == 0)
        {
            return "Green";
        }

        var color = IsRed(winningNumber) ? "Red" : "Black";
        var parity = winningNumber % 2 == 0 ? "Even" : "Odd";
        return $"{color}, {parity}";
    }

    public IReadOnlyList<string> GetEventLog()
    {
        lock (eventsLock)
        {
            return eventLog.ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<(string Name, decimal Chips)> GetStandings()
    {
        return Players
            .OrderByDescending(currentPlayer => currentPlayer.Chips)
            .Select(currentPlayer => (currentPlayer.Name, currentPlayer.Chips))
            .ToList()
            .AsReadOnly();
    }

    private void AddEvent(string message)
    {
        lock (eventsLock)
        {
            eventLog.Add(message);
        }
    }

    public void ClearEventLog()
    {
        lock (eventsLock)
        {
            eventLog.Clear();
        }
    }
}