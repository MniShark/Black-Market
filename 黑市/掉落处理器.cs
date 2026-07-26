using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace 黑市
{
    public class 掉落处理器
    {
        private 货币数据 _货币Data;
        private NPC掉落配置 _掉落配置;
        private 任务系统 _任务系统;
        private Dictionary<int, HashSet<int>> _伤害记录 = new Dictionary<int, HashSet<int>>();
        private Dictionary<int, NPC掉落规则> _规则缓存 = new Dictionary<int, NPC掉落规则>();

        private int _清理计数器 = 0;
        private const int 清理间隔 = 60;
        private const int 最大记录数 = 500;

        /// <summary>
        /// 初始加载时的错误报告
        /// </summary>
        public string 加载报告 { get; private set; } = "";

        public 掉落处理器(货币数据 货币数据, 任务系统 任务系统)
        {
            _货币Data = 货币数据;
            _任务系统 = 任务系统;
            _掉落配置 = NPC掉落配置.加载(out string 报告);
            加载报告 = 报告 ?? "";
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-掉落] {报告}");
            刷新规则缓存();

            ServerApi.Hooks.NpcKilled.Register(黑市.黑市插件, OnNpcKilled);
            ServerApi.Hooks.NpcStrike.Register(黑市.黑市插件, OnNpcStrike);
            ServerApi.Hooks.GameUpdate.Register(黑市.黑市插件, OnGameUpdate);

            TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
        }

        public void 卸载()
        {
            ServerApi.Hooks.NpcKilled.Deregister(黑市.黑市插件, OnNpcKilled);
            ServerApi.Hooks.NpcStrike.Deregister(黑市.黑市插件, OnNpcStrike);
            ServerApi.Hooks.GameUpdate.Deregister(黑市.黑市插件, OnGameUpdate);
            TShockAPI.Hooks.GeneralHooks.ReloadEvent -= OnReload;
        }

        /// <summary>
        /// 重载配置，返回错误报告（空字符串表示无错误）
        /// </summary>
        public string 重载配置()
        {
            _掉落配置 = NPC掉落配置.加载(out string 报告);
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-掉落] {报告}");
            刷新规则缓存();
            return 报告 ?? "";
        }

        private void 刷新规则缓存()
        {
            _规则缓存 = _掉落配置.掉落规则列表.ToDictionary(r => r.NPCID);
        }

        private void OnReload(TShockAPI.Hooks.ReloadEventArgs args)
        {
            string 报告 = 重载配置();
            if (string.IsNullOrEmpty(报告))
                args.Player?.SendSuccessMessage("[黑市] NPC掉落配置已重载！");
        }

        private void OnNpcStrike(NpcStrikeEventArgs args)
        {
            var player = args.Player;
            var npc = args.Npc;
            if (player == null || npc == null) return;

            int npcIndex = npc.whoAmI;
            int playerId = player.whoAmI;

            if (!_伤害记录.ContainsKey(npcIndex))
                _伤害记录[npcIndex] = new HashSet<int>();
            _伤害记录[npcIndex].Add(playerId);
        }

        private void OnNpcKilled(NpcKilledEventArgs args)
        {
            var npc = args.npc;
            if (npc == null) return;

            if (!_伤害记录.TryGetValue(npc.whoAmI, out var playerIds))
                return;

            if (!_规则缓存.TryGetValue(npc.netID, out var 规则))
            {
                _伤害记录.Remove(npc.whoAmI);
                return;
            }

            int 掉落数 = 规则.掉落数量;
            string 货币名 = 规则.货币名称;
            string npc名称 = 规则.NPC名称;
            if (掉落数 <= 0 || string.IsNullOrEmpty(货币名))
            {
                _伤害记录.Remove(npc.whoAmI);
                return;
            }

            bool 显示浮动 = _掉落配置.击杀浮动文本;
            bool 显示聊天 = _掉落配置.击杀聊天栏文本;

            foreach (int playerId in playerIds)
            {
                var player = TShock.Players[playerId];
                if (player == null || !player.RealPlayer || !player.Active)
                    continue;

                _货币Data.增加货币(player.Name, 货币名, 掉落数);

                // 使用配置中的NPC名称，如果没有则使用游戏内名称
                string 显示名称 = !string.IsNullOrEmpty(npc名称) ? npc名称 : npc.FullName;

                if (显示浮动)
                    黑市.发送浮动文字(player, $"击杀{显示名称} 获得{掉落数}{货币名}", Color.Green);
                if (显示聊天 && (掉落数 >= 5 || npc.boss))
                    player.SendSuccessMessage($"[黑市] 击杀 {显示名称} 获得 {掉落数} 个{货币名}！");

                _任务系统?.处理击杀进度(player.Name, npc.netID);
            }
            _伤害记录.Remove(npc.whoAmI);
        }

        private void OnGameUpdate(EventArgs args)
        {
            _清理计数器++;
            if (_清理计数器 < 清理间隔) return;
            _清理计数器 = 0;

            var 待清理 = new List<int>();

            if (_伤害记录.Count > 最大记录数)
            {
                var 最旧的 = _伤害记录.Keys.Take(_伤害记录.Count - 最大记录数).ToList();
                foreach (var k in 最旧的) _伤害记录.Remove(k);
            }

            foreach (var key in _伤害记录.Keys)
            {
                if (key < 0 || key >= Main.npc.Length)
                {
                    待清理.Add(key);
                    continue;
                }
                var npc = Main.npc[key];
                if (npc == null || !npc.active || npc.life <= 0)
                    待清理.Add(key);
            }

            foreach (var key in 待清理)
                _伤害记录.Remove(key);
        }
    }
}