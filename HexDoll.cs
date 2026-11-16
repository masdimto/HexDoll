using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace HexDollBuffPlugin
{
    [ApiVersion(2, 1)]
    public class HexDollBuff : TerrariaPlugin
    {
        public override string Name => "HexDollBuff";
        public override System.Version Version => new System.Version(1, 1, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Membuat seluruh tentakel Shadowflame Hex Doll menembus tembok dan blok sepenuhnya.";

        private int[] castCooldown = new int[Main.maxPlayers];

        public HexDollBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);

            Commands.ChatCommands.Add(new Command("hexdoll.buff", CheckStatusCommand, "hexdoll", "hdbuff")
            {
                HelpText = "Mengecek status buff Shadowflame Hex Doll."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            }
            base.Dispose(disposing);
        }

        private void CheckStatusCommand(CommandArgs args)
        {
            args.Player.SendSuccessMessage("[HexDollBuff] Aktif! Semua tentakel Shadowflame Hex Doll menembus tembok 100%.");
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers)
                castCooldown[args.Who] = 0;
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (args.Who >= 0 && args.Who < Main.maxPlayers)
                castCooldown[args.Who] = 0;
        }

        private void OnGameUpdate(EventArgs args)
        {
            // 1. Paksa semua proyektil ShadowFlame yang aktif di server agar tidak hancur oleh dinding
            for (int p = 0; p < Main.maxProjectiles; p++)
            {
                Projectile proj = Main.projectile[p];
                if (proj != null && proj.active && proj.type == ProjectileID.ShadowFlame)
                {
                    if (proj.tileCollide)
                    {
                        proj.tileCollide = false;
                        proj.netUpdate = true;
                    }
                }
            }

            // 2. Saat pemain menembakkan Shadowflame Hex Doll, salin arah tembakannya menjadi proyektil server-side
            // agar tetap melaju tembus tembok meskipun proyektil asli client hancur menabrak blok
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (castCooldown[i] > 0)
                    castCooldown[i]--;

                TSPlayer tsPlayer = TShock.Players[i];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null)
                    continue;

                if (!tsPlayer.HasPermission("hexdoll.buff"))
                    continue;

                Player player = tsPlayer.TPlayer;
                Item heldItem = player.HeldItem;

                if (heldItem != null && heldItem.type == ItemID.ShadowFlameHexDoll && player.controlUseItem && castCooldown[i] <= 0)
                {
                    // Cari proyektil ShadowFlame terbaru milik pemain ini untuk meniru sudut & kecepatan tembakannya
                    int foundCount = 0;
                    for (int p = 0; p < Main.maxProjectiles; p++)
                    {
                        Projectile orig = Main.projectile[p];
                        if (orig != null && orig.active && orig.owner == i && orig.type == ProjectileID.ShadowFlame && orig.ai[2] != 99f)
                        {
                            // Tandai proyektil asli agar tidak disalin dua kali
                            orig.ai[2] = 99f;

                            int damage = player.GetWeaponDamage(heldItem);
                            int cloneIdx = Projectile.NewProjectile(
                                player.GetProjectileSource_Item(heldItem),
                                orig.Center,
                                orig.velocity,
                                ProjectileID.ShadowFlame,
                                damage,
                                heldItem.knockBack,
                                i,
                                orig.ai[0],
                                orig.ai[1]
                            );

                            if (cloneIdx >= 0 && cloneIdx < Main.maxProjectiles)
                            {
                                Projectile clone = Main.projectile[cloneIdx];
                                clone.tileCollide = false;
                                clone.ai[2] = 99f;
                                clone.penetrate = 4;
                                clone.timeLeft = 65;
                                NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, cloneIdx);
                            }

                            foundCount++;
                            if (foundCount >= 3)
                                break;
                        }
                    }

                    if (foundCount > 0)
                    {
                        castCooldown[i] = Math.Max(4, heldItem.useTime - 2);
                    }
                }
            }
        }
    }
}
