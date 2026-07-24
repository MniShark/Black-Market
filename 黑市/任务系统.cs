using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;

namespace 黑市
{
    public class 任务系统
    {
        private 任务配置 _配置;
        private 货币数据 _货币数据;
        private 职业系统 _职业系统;
        private ReaderWriterLockSlim _配置锁 = new ReaderWriterLockSlim();

        /// <summary>
        /// 初始加载时的错误报告
        /// </summary>
        public string 加载报告 { get; private set; } = "";

        public 任务系统(货币数据 货币数据, 职业系统 职业系统)
        {
            _货币数据 = 货币数据 ?? throw new ArgumentNullException(nameof(货币数据));
            _职业系统 = 职业系统 ?? throw new ArgumentNullException(nameof(职业系统));
            _配置 = 任务配置.加载(out string 报告);
            加载报告 = 报告 ?? "";
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-任务] {报告}");
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
            var 新配置 = 任务配置.加载(out string 报告);
            if (!string.IsNullOrEmpty(报告))
                TShock.Log.Warn($"[黑市-任务] {报告}");
            _配置锁.EnterWriteLock();
            try { _配置 = 新配置; }
            finally { _配置锁.ExitWriteLock(); }
            return 报告 ?? "";
        }

        private void OnReload(ReloadEventArgs args)
        {
            string 报告 = 重载配置();
            if (string.IsNullOrEmpty(报告))
                args.Player?.SendSuccessMessage("[黑市] 任务配置已重载！");
        }

        public string 获取任务列表信息(string 玩家名)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 消息 = new List<string> { $"[c/FFD700:任务列表 (最多接取 {_配置.最大接取数量} 个)：]" };
                foreach (var 任务 in _配置.任务列表)
                {
                    var 状态 = _货币数据.获取任务状态(玩家名, 任务.序号);
                    string 标记;
                    string 状态文本;
                    switch (状态)
                    {
                        case 任务状态.未接取:
                            标记 = "[c/AAAAAA:○]";
                            状态文本 = "未接取";
                            break;
                        case 任务状态.进行中:
                            int 当前 = _货币数据.获取任务进度(玩家名, 任务.序号);
                            标记 = "[c/FFFF00:▶]";
                            状态文本 = $"进行中 {当前}/{任务.目标数量}";
                            break;
                        case 任务状态.已完成:
                            标记 = "[c/00FF00:✔]";
                            状态文本 = "已完成（可提交）";
                            break;
                        case 任务状态.已提交:
                            标记 = "[c/808080:✓]";
                            状态文本 = "已提交";
                            break;
                        default:
                            标记 = "[c/AAAAAA:○]";
                            状态文本 = "未接取";
                            break;
                    }

                    string 限制信息 = "";
                    if (!string.IsNullOrEmpty(任务.进度条件))
                        限制信息 += $" [c/808080:进度:{任务.进度条件}]";
                    if (任务.允许职业.Count > 0)
                        限制信息 += $" [c/FFA500:职业:{string.Join("/", 任务.允许职业)}]";

                    消息.Add($"{标记} [c/87CEEB:<{任务.序号}> {任务.名称}] [c/AAAAAA:{状态文本}]{限制信息}");
                    消息.Add($"   [c/AAAAAA:{任务.描述}]");
                }
                消息.Add("[c/AAAAAA:使用 /接取 <序号> 接取任务，/提交 <序号> 提交已完成任务，/放弃 <序号> 放弃任务。]");
                return string.Join("\n", 消息);
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 接取任务(TSPlayer player, int 序号, out string 结果)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 任务 = _配置.获取任务(序号);
                if (任务 == null)
                {
                    结果 = "任务序号不存在。";
                    return false;
                }

                var 当前状态 = _货币数据.获取任务状态(player.Name, 序号);
                if (当前状态 != 任务状态.未接取)
                {
                    结果 = "该任务已接取或已完成。";
                    return false;
                }

                // 检查最大接取数量
                int 已接取数量 = _货币数据.获取玩家已接取任务数量(player.Name);
                if (已接取数量 >= _配置.最大接取数量)
                {
                    结果 = $"您已接取 {_配置.最大接取数量} 个任务，达到上限。请先提交或放弃现有任务。";
                    return false;
                }

                if (!string.IsNullOrEmpty(任务.进度条件))
                {
                    if (!进度映射器.检查进度表达式(任务.进度条件))
                    {
                        结果 = $"需要进度：{任务.进度条件}";
                        return false;
                    }
                }

                if (任务.允许职业.Count > 0)
                {
                    if (!_职业系统.检查职业匹配(player.Name, 任务.允许职业))
                    {
                        结果 = $"该任务需要以下职业之一：{string.Join("、", 任务.允许职业)}";
                        return false;
                    }
                }

                _货币数据.设置任务状态(player.Name, 序号, 任务状态.进行中, 0);
                结果 = $"已接取任务「{任务.名称}」。";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 放弃任务(TSPlayer player, int 序号, out string 结果)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 任务 = _配置.获取任务(序号);
                if (任务 == null)
                {
                    结果 = "任务序号不存在。";
                    return false;
                }

                var 当前状态 = _货币数据.获取任务状态(player.Name, 序号);
                if (当前状态 == 任务状态.未接取)
                {
                    结果 = "该任务尚未接取。";
                    return false;
                }

                _货币数据.删除任务进度(player.Name, 序号);
                结果 = $"已放弃任务「{任务.名称}」，进度已清空。";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 提交任务(TSPlayer player, int 序号, out string 结果)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 任务 = _配置.获取任务(序号);
                if (任务 == null)
                {
                    结果 = "任务序号不存在。";
                    return false;
                }

                var 当前状态 = _货币数据.获取任务状态(player.Name, 序号);
                if (当前状态 != 任务状态.已完成)
                {
                    int 当前进度 = _货币数据.获取任务进度(player.Name, 序号);
                    结果 = $"任务尚未完成，当前进度 {当前进度}/{任务.目标数量}。";
                    return false;
                }

                // 发放货币奖励
                if (任务.货币奖励 != null && 任务.货币奖励.Count > 0)
                {
                    foreach (var 奖励 in 任务.货币奖励)
                    {
                        _货币数据.增加货币(player.Name, 奖励.货币名, 奖励.数量);
                        player.SendSuccessMessage($"[任务] 获得 {奖励.数量} {奖励.货币名}。");
                    }
                }

                // 执行指令
                if (任务.执行指令 != null && 任务.执行指令.Count > 0)
                {
                    foreach (var 指令 in 任务.执行指令)
                    {
                        string 处理指令 = 指令.Replace("{0}", player.Name).Replace("{}", player.Name);
                        if (!处理指令.StartsWith("/")) 处理指令 = "/" + 处理指令;
                        Commands.HandleCommand(TSPlayer.Server, 处理指令);
                    }
                }

                _货币数据.设置任务状态(player.Name, 序号, 任务状态.已提交, 任务.目标数量);
                结果 = $"任务「{任务.名称}」提交成功！";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public void 处理击杀进度(string 玩家名, int npcID)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 进行中任务 = _货币数据.获取玩家进行中任务(玩家名);
                foreach (var (序号, 进度) in 进行中任务)
                {
                    var 任务 = _配置.获取任务(序号);
                    if (任务 == null) continue;
                    if (任务.目标ID != npcID) continue;

                    int 新进度 = 进度 + 1;
                    if (新进度 >= 任务.目标数量)
                    {
                        _货币数据.设置任务状态(玩家名, 序号, 任务状态.已完成, 任务.目标数量);
                        var player = TShock.Players.FirstOrDefault(p => p != null && p.Name == 玩家名 && p.RealPlayer);
                        if (player != null)
                        {
                            player.SendSuccessMessage($"[任务] 「{任务.名称}」已完成！使用 /提交 {序号} 领取奖励。");
                            黑市.发送浮动文字(player, $"任务完成：{任务.名称}", Color.Gold);
                        }
                    }
                    else
                    {
                        _货币数据.设置任务状态(玩家名, 序号, 任务状态.进行中, 新进度);
                    }
                }
            }
            finally { _配置锁.ExitReadLock(); }
        }

        public bool 管理员重置任务(string 玩家名, int 序号, out string 结果)
        {
            _配置锁.EnterReadLock();
            try
            {
                var 任务 = _配置.获取任务(序号);
                if (任务 == null)
                {
                    结果 = "任务序号不存在。";
                    return false;
                }

                _货币数据.删除任务进度(玩家名, 序号);
                结果 = $"已将 {玩家名} 的任务「{任务.名称}」重置。";
                return true;
            }
            finally { _配置锁.ExitReadLock(); }
        }
    }

    public enum 任务状态
    {
        未接取 = 0,
        进行中 = 1,
        已完成 = 2,
        已提交 = 3
    }
}