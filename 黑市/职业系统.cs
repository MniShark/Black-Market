using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace 黑市
{
    public class 职业系统
    {
        private 职业配置 _配置;
        private 货币数据 _货币数据;
        private ReaderWriterLockSlim _配置锁 = new ReaderWriterLockSlim();

        /// <summary>
        /// 初始加载时的错误报告
        /// </summary>
        public string 加载报告 { get; private set; } = "";

        public 职业系统(货币数据 货币数据)
        {
            _货币数据 = 货币数据 ?? throw new ArgumentNullException(nameof(货币数据));
            _配置 = 职业配置.加载(out string 报告);
            加载报告 = 报告 ?? "";
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-职业] {报告}");
            GeneralHooks.ReloadEvent += OnReload;
        }

        public void 卸载()
        {
            GeneralHooks.ReloadEvent -= OnReload;
            _配置锁.Dispose();
        }

        /// <summary>
        /// 重载配置，返回错误报告（空字符串表示无错误）
        /// </summary>
        public string 重载配置()
        {
            var 新配置 = 职业配置.加载(out string 报告);
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-职业] {报告}");
            _配置锁.EnterWriteLock();
            try { _配置 = 新配置; }
            finally { _配置锁.ExitWriteLock(); }
            return 报告 ?? "";
        }

        private void OnReload(ReloadEventArgs args)
        {
            string 报告 = 重载配置();
            if (string.IsNullOrEmpty(报告))
                args.Player?.SendSuccessMessage("[黑市] 职业配置已重载！");
        }

        public string 获取玩家当前职业(string 玩家名)
        {
            if (_货币数据 == null) return _配置.默认职业名称;
            return _货币数据.获取玩家职业(玩家名);
        }

        public string 获取玩家职业颜色(string 玩家名)
        {
            _配置锁.EnterReadLock();
            try
            {
                string 职业名 = 获取玩家当前职业(玩家名);
                var 职业 = _配置.获取职业(职业名);
                return 职业?.颜色 ?? "FFFFFF";
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 切换职业(TSPlayer player, string 职业名称, out string 结果)
        {
            if (_货币数据 == null)
            {
                结果 = "数据未初始化，请重试。";
                return false;
            }

            _配置锁.EnterReadLock();
            try
            {
                var 目标职业 = _配置.获取职业(职业名称);
                if (目标职业 == null)
                {
                    结果 = "职业不存在。";
                    return false;
                }

                string 当前职业名 = 获取玩家当前职业(player.Name);
                var 当前职业 = _配置.获取职业(当前职业名);

                if (当前职业 != null && !当前职业.可转职)
                {
                    结果 = $"您当前是「{当前职业名}」，该职业无法再次转职，请联系管理员重置。";
                    return false;
                }

                if (当前职业名.Equals(职业名称, StringComparison.OrdinalIgnoreCase))
                {
                    结果 = "您已经是该职业。";
                    return false;
                }

                _货币数据.设置玩家职业(player.Name, 职业名称);

                if (目标职业.转职奖励物品 != null && 目标职业.转职奖励物品.Count > 0)
                {
                    foreach (var 奖励 in 目标职业.转职奖励物品)
                    {
                        player.GiveItem(奖励.物品ID, 奖励.数量, 奖励.前缀);
                        string 物品名 = Lang.GetItemName(奖励.物品ID).ToString();
                        player.SendSuccessMessage($"[转职] 获得转职奖励：{物品名}×{奖励.数量}");
                    }
                }

                if (目标职业.转职执行指令 != null && 目标职业.转职执行指令.Count > 0)
                {
                    foreach (var 指令 in 目标职业.转职执行指令)
                    {
                        string 处理指令 = 指令.Replace("{0}", player.Name).Replace("{}", player.Name);
                        if (!处理指令.StartsWith("/")) 处理指令 = "/" + 处理指令;
                        Commands.HandleCommand(TSPlayer.Server, 处理指令);
                    }
                }

                结果 = $"已成功转职为「{职业名称}」。";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 管理员重置职业(string 玩家名, out string 结果)
        {
            if (_货币数据 == null)
            {
                结果 = "数据未初始化。";
                return false;
            }

            _配置锁.EnterReadLock();
            try
            {
                var 默认职业 = _配置.获取默认职业();
                string 重置职业名 = 默认职业?.名称 ?? "入门";
                _货币数据.设置玩家职业(玩家名, 重置职业名);
                结果 = $"已将 {玩家名} 的职业重置为「{重置职业名}」。";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 检查职业匹配(string 玩家名, List<string> 允许职业列表)
        {
            if (允许职业列表 == null || 允许职业列表.Count == 0)
                return true;
            string 当前职业 = 获取玩家当前职业(玩家名);
            return 允许职业列表.Contains(当前职业, StringComparer.OrdinalIgnoreCase);
        }

        public string 获取职业列表信息(TSPlayer player)
        {
            _配置锁.EnterReadLock();
            try
            {
                string 当前职业 = 获取玩家当前职业(player.Name);
                var 当前职业定义 = _配置.获取职业(当前职业);
                bool 可转职 = 当前职业定义?.可转职 ?? false;
                var 消息 = new List<string> { "[c/FFD700:可选择的职业：]" };

                foreach (var 职业 in _配置.职业列表)
                {
                    bool 已选 = 职业.名称.Equals(当前职业, StringComparison.OrdinalIgnoreCase);
                    string 标记 = 已选 ? " [c/00FF00:✔ 当前]" : "";
                    string 状态 = "";

                    if (!职业.可转职 && !已选)
                        状态 = " [c/808080:（不可选）]";

                    if (职业.可转职 && !已选 && !可转职)
                        状态 = " [c/808080:（需重置后选择）]";

                    string 奖励信息 = "";
                    if (职业.转职奖励物品 != null && 职业.转职奖励物品.Count > 0)
                    {
                        var 奖励列表 = 职业.转职奖励物品.Select(r => Lang.GetItemName(r.物品ID).ToString()).ToList();
                        奖励信息 = $" [c/FFD700:奖励:{string.Join(",", 奖励列表)}]";
                    }
                    if (职业.转职执行指令 != null && 职业.转职执行指令.Count > 0)
                    {
                        奖励信息 += " [c/FFD700:特效]";
                    }

                    消息.Add($"[c/87CEEB:{职业.名称}] [c/AAAAAA:（{职业.描述}）]{标记}{状态}{奖励信息}");
                }

                if (可转职)
                    消息.Add("[c/AAAAAA:使用 /转职 <职业名> 来切换职业。]");
                else
                    消息.Add("[c/FF5555:您已转职，无法再次更改，请联系管理员重置。]");

                return string.Join("\n", 消息);
            }
            finally { _配置锁.ExitReadLock(); }
        }
    }
}