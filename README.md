# Roulette

Simple console roulette demo in C# using three player threads.

## What it does

- Creates `3` players.
- Starts `1` thread per player.
- The `Game` keeps a board for all roulette numbers (`0-36`).
- Supports typed bets: `Straight`, `Red/Black`, and `Even/Odd`.
- Player threads stay alive for all rounds and keep betting while the window is open.
- Each player can place up to `8` successful bets per open window.
- Game thread announces `betting is open`, then later `no more bets`.
- Main thread waits for all players to stop betting, then rolls once per round.
- `Straight` winners are paid stake + `35x` bonus.
- `Red/Black` and `Even/Odd` winners are paid stake + `1x` bonus.
- All console I/O is emitted from the main thread only.

## Run

```bash
dotnet build
dotnet run
```# roulette
