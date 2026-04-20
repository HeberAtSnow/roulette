namespace roulette;

public class Player
{
    private readonly object chipsLock = new();

    public Player(string name, decimal startingChips)
    {
        Name = name;
        Chips = startingChips;
    }

    public string Name { get; }

    public decimal Chips { get; private set; }

    public bool CanCoverBet(decimal amount)
    {
        lock (chipsLock)
        {
            return amount > 0 && Chips >= amount;
        }
    }

    public bool PlaceBet(decimal amount)
    {
        lock (chipsLock)
        {
            if (amount <= 0 || Chips < amount)
            {
                return false;
            }

            Chips -= amount;
            return true;
        }
    }

    public void Award(decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        lock (chipsLock)
        {
            Chips += amount;
        }
    }
}