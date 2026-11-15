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
        public override System.Version Version => new System.Version(1, 1, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Membuat tentakel Shadowflame Hex Doll menembus blok dan menyerang musuh di balik tembok.";

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
            args.Player.SendInfoMessage("[Hex Doll Buff v1.1] Aktif! Tentakel Shadowflame Hex Doll menembus blok.");
        }

        private void OnProjectileSetDefaults(SetDefaultsEventArgs<Projectile, int> args)
        {
            Projectile proj = args.Object;
            if (proj == null)
                return;

            if (proj.type == ProjectileID.ShadowFlame)
            {
                proj.tileCollide = false;
                proj.penetrate = -1;
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj == null || !proj.active || proj.type != ProjectileID.ShadowFlame)
                    continue;

                if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
                    continue;

                TSPlayer tsPlayer = TShock.Players[proj.owner];
                if (tsPlayer == null || !tsPlayer.Active || !tsPlayer.HasPermission("hexdoll.buff"))
                    continue;

                // Paksa matikan tabrakan blok di server
                proj.tileCollide = false;

                // Cek apakah posisi depan tentakel menyentuh blok padat
                Vector2 nextPos = proj.position + proj.velocity;
                bool hittingWall = Collision.SolidCollision(nextPos, proj.width, proj.height);

                if (hittingWall && proj.velocity.Length() > 0.5f)
                {
                    // Dorong posisi tentakel melewati blok secara paksa di sisi server & sinkronkan ke client
                    proj.position += proj.velocity * 1.25f;
                    NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
                }

                // Cek apakah ada musuh di balik tembok dalam jalur tentakel agar tetap kena damage
                DamageEnemiesBehindWall(proj);
            }
        }

        private void DamageEnemiesBehindWall(Projectile proj)
        {
            Rectangle projHitbox = new Rectangle((int)proj.position.X - 8, (int)proj.position.Y - 8, proj.width + 16, proj.height + 16);

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (npc != null && npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                {
                    if (npc.immune[proj.owner] <= 0 && projHitbox.Intersects(npc.Hitbox))
                    {
                        // Berikan damage langsung & efek Shadowflame meski terhalang tembok
                        int hitDir = (proj.velocity.X >= 0) ? 1 : -1;
                        npc.StrikeNPC(proj.damage, proj.knockBack, hitDir, false, false, proj.owner);
                        npc.AddBuff(BuffID.ShadowFlame, 180);
                        npc.immune[proj.owner] = 10;

                        NetMessage.SendData((int)PacketTypes.NpcStrike, -1, -1, null, npc.whoAmI, proj.damage, proj.knockBack, hitDir);
                    }
                }
            }
        }
    }
}
