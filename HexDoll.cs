using System;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace AutoPlatformPlugin
{
    [ApiVersion(2, 1)]
    public class AutoPlatform : TerrariaPlugin
    {
        public override string Name => "AutoPlatform";
        public override System.Version Version => new System.Version(1, 3, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Auto-build platform 8 arah dengan pengurangan inventory yang sinkron.";

        private bool[] autoPlatEnabled = new bool[Main.maxPlayers];
        private int[] cooldown = new int[Main.maxPlayers];
        private int[] warnCooldown = new int[Main.maxPlayers];

        public AutoPlatform(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);

            Commands.ChatCommands.Add(new Command("autoplat.use", ToggleAutoPlat, "autoplat", "ap")
            {
                HelpText = "Mengaktifkan/menonaktifkan Auto Platform 8 arah."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            }
            base.Dispose(disposing);
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers)
            {
                autoPlatEnabled[args.Who] = false;
                cooldown[args.Who] = 0;
                warnCooldown[args.Who] = 0;
            }
        }

        private void ToggleAutoPlat(CommandArgs args)
        {
            int idx = args.Player.Index;
            autoPlatEnabled[idx] = !autoPlatEnabled[idx];

            if (autoPlatEnabled[idx])
                args.Player.SendSuccessMessage("[AutoPlatform] AKTIF! Arahkan kursor & tahan tombol serang sambil memegang platform.");
            else
                args.Player.SendInfoMessage("[AutoPlatform] NONAKTIF.");
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || !autoPlatEnabled[i])
                    continue;

                if (!tsPlayer.HasPermission("autoplat.use"))
                    continue;

                if (cooldown[i] > 0)
                    cooldown[i]--;

                if (warnCooldown[i] > 0)
                    warnCooldown[i]--;

                Player player = tsPlayer.TPlayer;
                if (player == null || player.dead || !player.controlUseItem)
                    continue;

                Item heldItem = player.HeldItem;
                if (heldItem == null || heldItem.IsAir || !IsPlatformItem(heldItem))
                    continue;

                if (cooldown[i] > 0)
                    continue;

                // Cek ketersediaan platform di inventory (minimal butuh 2: 1 yang dipasang client + 1 dari plugin)
                int totalPlatforms = CountPlatforms(player, heldItem.type);
                if (totalPlatforms <= 1)
                {
                    if (warnCooldown[i] <= 0)
                    {
                        tsPlayer.SendWarningMessage("[AutoPlatform] Platform di inventory hampir habis!");
                        warnCooldown[i] = 120; // Jeda peringatan 2 detik agar tidak spam
                    }
                    continue;
                }

                // Hitung arah 8 penjuru dari pemain ke kursor
                Vector2 dir = Main.MouseWorld - player.Center;
                if (dir.Length() < 8f)
                    continue;

                dir.Normalize();
                int stepX = (int)Math.Round(dir.X);
                int stepY = (int)Math.Round(dir.Y);

                if (stepX == 0 && stepY == 0)
                    continue;

                int startTileX = (int)(player.Center.X / 16f);
                int startTileY = (int)(player.Center.Y / 16f);

                // Pasang maksimal 4 platform berurutan ke arah kursor
                int placedCount = 0;
                for (int step = 1; step <= 4; step++)
                {
                    int targetX = startTileX + (stepX * step);
                    int targetY = startTileY + (stepY * step);

                    if (!WorldGen.InWorld(targetX, targetY, 10))
                        break;

                    Tile tile = Main.tile[targetX, targetY];
                    if (tile != null && !tile.active())
                    {
                        if (!ConsumeOnePlatform(tsPlayer, player, heldItem.type))
                            break;

                        WorldGen.PlaceTile(targetX, targetY, TileID.Platforms, mute: true, forced: false, plr: i, style: heldItem.placeStyle);
                        NetMessage.SendTileSquare(-1, targetX, targetY, 1);
                        placedCount++;
                    }
                }

                if (placedCount > 0)
                {
                    cooldown[i] = 6; // Jeda 6 tick (~0.1 detik) agar rapi dan hemat
                }
            }
        }

        private bool IsPlatformItem(Item item)
        {
            return item.createTile == TileID.Platforms;
        }

        private int CountPlatforms(Player player, int itemType)
        {
            int count = 0;
            for (int s = 0; s < 50; s++) // Slot 0-49 adalah inventory utama + hotbar
            {
                if (player.inventory[s] != null && player.inventory[s].type == itemType)
                {
                    count += player.inventory[s].stack;
                }
            }
            return count;
        }

        private bool ConsumeOnePlatform(TSPlayer tsPlayer, Player player, int itemType)
        {
            // Cari dari slot paling belakang (49 ke 0) agar tidak bentrok dengan slot yang sedang dipegang di hotbar (0-9)
            for (int s = 49; s >= 0; s--)
            {
                Item invItem = player.inventory[s];
                if (invItem != null && invItem.type == itemType && invItem.stack > 0)
                {
                    // Sisakan minimal 1 di slot yang sedang dipegang agar client tidak membatalkan animasi penggunaan
                    if (s == player.selectedItem && invItem.stack <= 1)
                        continue;

                    invItem.stack--;
                    if (invItem.stack <= 0)
                    {
                        invItem.TurnToAir();
                    }

                    // Sinkronkan slot spesifik ini ke client
                    NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, null, tsPlayer.Index, s);
                    return true;
                }
            }
            return false;
        }
    }
}
