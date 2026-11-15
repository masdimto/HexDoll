using System;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace HexDollBuffPlugin
{
    [ApiVersion(2, 1)]
    public class HexDollBuff : TerrariaPlugin
    {
        public override string Name => "HexDollBuff";
        public override System.Version Version => new System.Version(1, 0, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Membuat tentakel Shadowflame Hex Doll bisa menembus blok.";

        public HexDollBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.ProjectileSetDefaults.Register(this, OnProjectileSetDefaults);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            Commands.ChatCommands.Add(new Command("hexdoll.buff", HexDollInfoCommand, "hexdoll", "hdbuff")
            {
                HelpText = "Melihat informasi buff Shadowflame Hex Doll."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ProjectileSetDefaults.Deregister(this, OnProjectileSetDefaults);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
        }

        private void HexDollInfoCommand(CommandArgs args)
        {
            args.Player.SendInfoMessage("[Hex Doll Buff] Aktif! Tentakel Shadowflame Hex Doll kini dapat menembus blok.");
        }

        private void OnProjectileSetDefaults(SetDefaultsEventArgs<Projectile, int> args)
        {
            Projectile proj = args.Object;
            if (proj == null)
                return;

            // ProjectileID.ShadowFlame adalah tentakel ungu dari Shadowflame Hex Doll
            if (proj.type == ProjectileID.ShadowFlame)
            {
                proj.tileCollide = false;
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj == null || !proj.active)
                    continue;

                if (proj.type == ProjectileID.ShadowFlame)
                {
                    if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
                        continue;

                    TSPlayer tsPlayer = TShock.Players[proj.owner];
                    if (tsPlayer == null || !tsPlayer.Active || !tsPlayer.HasPermission("hexdoll.buff"))
                        continue;

                    // Pastikan tileCollide tetap mati selama tentakel aktif (tidak tertimpa AI vanilla)
                    if (proj.tileCollide)
                    {
                        proj.tileCollide = false;
                        NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
                    }
                }
            }
        }
    }
}
