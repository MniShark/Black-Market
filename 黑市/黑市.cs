#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;

namespace 黑市
{
    public static class 黑市路径
    {
        public static readonly string 根目录 = Path.Combine(TShock.SavePath, "黑市");
        public static readonly string 配置路径 = Path.Combine(根目录, "黑市.json");
        public static readonly string 进度映射路径 = Path.Combine(根目录, "进度映射.json");
        public static readonly string 数据库路径 = Path.Combine(根目录, "黑市.sqlite");

        static 黑市路径()
        {
            if (!Directory.Exists(根目录))
                Directory.CreateDirectory(根目录);
        }
        public static void 确保目录存在() { }
    }

    [ApiVersion(2, 1)]
    public class 黑市 : TerrariaPlugin
    {
        public override string Name => "黑市";
        public override string Author => "Mni Shark";
        public override string Description => "兑换、购买、余额查询、NPC掉落、盲盒、职业、任务";
        public override Version Version => new Version(1, 2, 2);

        public static 黑市 黑市插件;

        private 黑市配置 _配置;
        private 货币数据 _货币数据;
        private 掉落处理器 _掉落处理器;
        private 职业系统 _职业系统;
        public 任务系统 _任务系统;

        private static Dictionary<string, FieldInfo> _字段缓存 = new Dictionary<string, FieldInfo>();
        private static Dictionary<string, PropertyInfo> _属性缓存 = new Dictionary<string, PropertyInfo>();
        private static readonly Random _随机 = new Random();

        public 黑市(Main game) : base(game)
        {
            黑市插件 = this;
        }

        public override void Initialize()
        {
            进度映射器.加载();
            _配置 = 黑市配置.加载(out string 黑市报告);
            _货币数据 = 货币数据.加载();
            _职业系统 = new 职业系统(_货币数据);
            _任务系统 = new 任务系统(_货币数据, _职业系统);
            _掉落处理器 = new 掉落处理器(_货币数据, _任务系统);

            // 汇总初始加载错误报告
            var 初始报告 = new List<string>();
            if (!string.IsNullOrEmpty(黑市报告))
                初始报告.Add($"[商店] {黑市报告}");

            if (!string.IsNullOrEmpty(_职业系统.加载报告))
                初始报告.Add($"[职业] {_职业系统.加载报告}");
            if (!string.IsNullOrEmpty(_任务系统.加载报告))
                初始报告.Add($"[任务] {_任务系统.加载报告}");
            if (!string.IsNullOrEmpty(_掉落处理器.加载报告))
                初始报告.Add($"[掉落] {_掉落处理器.加载报告}");

            if (初始报告.Count > 0)
            {
                string 完整报告 = string.Join("\n", 初始报告);
                TShock.Log.Warn($"[黑市] 初始加载配置错误报告：\n{完整报告}");
                TSPlayer.Server.SendErrorMessage($"[黑市] 配置加载完成，但发现以下错误：\n{完整报告}");
            }

            GeneralHooks.ReloadEvent += OnReload;
            ServerApi.Hooks.ServerChat.Register(this, OnChat);

            Commands.ChatCommands.Add(new Command(列表命令处理, "黑市.使用", "黑市"));
            Commands.ChatCommands.Add(new Command(购买命令处理, "黑市.购买", "购买", "buy"));
            Commands.ChatCommands.Add(new Command(兑换命令处理, "黑市.兑换", "兑换", "exchange"));
            Commands.ChatCommands.Add(new Command(余额命令处理, "黑市.余额", "余额", "balance"));
            Commands.ChatCommands.Add(new Command(转账命令处理, "黑市.转账", "转账"));
            Commands.ChatCommands.Add(new Command(转职命令处理, "黑市.转职", "转职"));

            Commands.ChatCommands.Add(new Command(任务列表命令处理, "黑市.任务", "任务"));
            Commands.ChatCommands.Add(new Command(接取任务命令处理, "黑市.接取任务", "接取"));
            Commands.ChatCommands.Add(new Command(提交任务命令处理, "黑市.提交任务", "提交"));
            Commands.ChatCommands.Add(new Command(放弃任务命令处理, "黑市.放弃任务", "放弃"));

            Commands.ChatCommands.Add(new Command(添加货币命令处理, "黑市.管理.添加货币", "添加货币"));
            Commands.ChatCommands.Add(new Command(扣除货币命令处理, "黑市.管理.扣除货币", "扣除货币"));
            Commands.ChatCommands.Add(new Command(设置货币命令处理, "黑市.管理.设置货币", "设置货币"));
            Commands.ChatCommands.Add(new Command(查询余额命令处理, "黑市.管理.查询余额", "查询余额"));
            Commands.ChatCommands.Add(new Command(重置职业命令处理, "黑市.管理.重置职业", "重置职业"));
            Commands.ChatCommands.Add(new Command(重置任务命令处理, "黑市.管理.重置任务", "任务重置"));

            TShock.Log.Info("[黑市] 已加载！");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _掉落处理器?.卸载();
                _职业系统?.卸载();
                _任务系统?.卸载();
                GeneralHooks.ReloadEvent -= OnReload;
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                _货币数据?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            var 总报告 = new List<string>();

            进度映射器.加载();

            _配置 = 黑市配置.加载(out string 黑市报告);
            if (!string.IsNullOrEmpty(黑市报告))
                总报告.Add($"[商店] {黑市报告}");

            _货币数据.清理过期冷却();

            // 收集子系统的错误报告
            string 掉落报告 = _掉落处理器?.重载配置();
            if (!string.IsNullOrEmpty(掉落报告))
                总报告.Add($"[掉落] {掉落报告}");

            string 职业报告 = _职业系统?.重载配置();
            if (!string.IsNullOrEmpty(职业报告))
                总报告.Add($"[职业] {职业报告}");

            string 任务报告 = _任务系统?.重载配置();
            if (!string.IsNullOrEmpty(任务报告))
                总报告.Add($"[任务] {任务报告}");


            if (总报告.Count > 0)
            {
                string 完整报告 = string.Join("\n", 总报告);
                TShock.Log.Warn($"[黑市] Reload 配置错误报告：\n{完整报告}");
                args.Player?.SendErrorMessage($"[黑市] 配置重载完成，但发现以下错误：\n{完整报告}");
            }
            else
            {
                args.Player?.SendSuccessMessage("[黑市] 配置已重新加载！");
            }
            TShock.Log.Info("[黑市] 配置已重新加载！");
        }

        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled) return;
            var player = TShock.Players[args.Who];
            if (player == null || !player.RealPlayer || !player.IsLoggedIn) return;
            string rawText = args.Text;
            if (rawText.StartsWith("/") || rawText.StartsWith(".")) return;

            string 职业名 = _职业系统.获取玩家当前职业(player.Name);
            if (string.IsNullOrEmpty(职业名)) 职业名 = "入门";
            string 颜色 = _职业系统.获取玩家职业颜色(player.Name);
            if (string.IsNullOrEmpty(颜色)) 颜色 = "FFFFFF";

            string newMessage = $"[{职业名}] {player.Name}: {rawText}";
            byte r = Convert.ToByte(颜色.Substring(0, 2), 16);
            byte g = Convert.ToByte(颜色.Substring(2, 2), 16);
            byte b = Convert.ToByte(颜色.Substring(4, 2), 16);
            Color color = new Color(r, g, b);

            args.Handled = true;
            TSPlayer.All.SendMessage(newMessage, color);
        }

        private static bool 检查进度(TSPlayer 玩家, string 条件)
        {
            return 进度映射器.检查进度表达式(条件);
        }

        private void 发送成功(TSPlayer 玩家, string 文本) => 玩家?.SendMessage($"[c/00FF00:✔ {文本}]", Color.White);
        private void 发送错误(TSPlayer 玩家, string 文本) => 玩家?.SendMessage($"[c/FF5555:✘ {文本}]", Color.White);
        private void 发送信息(TSPlayer 玩家, string 文本) => 玩家?.SendMessage($"[c/AAAAAA:{文本}]", Color.White);

        public static void 发送浮动文字(TSPlayer 玩家, string 文本, Color 颜色)
        {
            if (玩家 == null || !玩家.RealPlayer || !玩家.Active) return;
            var t玩家 = 玩家.TPlayer;
            玩家.SendData(PacketTypes.CreateCombatTextExtended, 文本, (int)颜色.PackedValue, t玩家.Center.X, t玩家.Center.Y - 30f);
        }

        private TSPlayer 查找玩家(string 玩家名)
        {
            return TShock.Players.FirstOrDefault(p => p != null && p.Name == 玩家名);
        }

        private void 添加货币命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 3) { 发送错误(执行者, "用法：/添加货币 <玩家名> <货币名> <数量>"); return; }
            if (!int.TryParse(args.Parameters[2], out int 数量) || 数量 <= 0) { 发送错误(执行者, "数量必须为正整数。"); return; }

            string 目标玩家名 = args.Parameters[0];
            string 货币名 = args.Parameters[1];

            _货币数据.增加货币(目标玩家名, 货币名, 数量);
            int 新余额 = _货币数据.获取余额(目标玩家名, 货币名);
            发送成功(执行者, $"已给 {目标玩家名} 添加 {数量} {货币名}，余额：{新余额}。");

            var 目标 = 查找玩家(目标玩家名);
            if (目标 != null && 目标.Name != 执行者.Name)
                目标.SendSuccessMessage($"[黑市] 管理员给您添加了 {数量} {货币名}。");
            TShock.Log.Info($"[黑市] {执行者.Name} 给 {目标玩家名} 添加 {数量} {货币名}");
        }

        private void 扣除货币命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 3) { 发送错误(执行者, "用法：/扣除货币 <玩家名> <货币名> <数量>"); return; }
            if (!int.TryParse(args.Parameters[2], out int 数量) || 数量 <= 0) { 发送错误(执行者, "数量必须为正整数。"); return; }

            string 目标玩家名 = args.Parameters[0];
            string 货币名 = args.Parameters[1];

            if (!_货币数据.扣除货币(目标玩家名, 货币名, 数量))
            {
                int 当前 = _货币数据.获取余额(目标玩家名, 货币名);
                发送错误(执行者, $"{目标玩家名} 的{货币名}不足，当前只有 {当前}。");
                return;
            }

            int 新余额 = _货币数据.获取余额(目标玩家名, 货币名);
            发送成功(执行者, $"已从 {目标玩家名} 扣除 {数量} {货币名}，余额：{新余额}。");

            var 目标 = 查找玩家(目标玩家名);
            if (目标 != null && 目标.Name != 执行者.Name)
                目标.SendWarningMessage($"[黑市] 管理员扣除了您 {数量} {货币名}。");
            TShock.Log.Info($"[黑市] {执行者.Name} 从 {目标玩家名} 扣除 {数量} {货币名}");
        }

        private void 设置货币命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 3) { 发送错误(执行者, "用法：/设置货币 <玩家名> <货币名> <数量>"); return; }
            if (!int.TryParse(args.Parameters[2], out int 数量) || 数量 < 0) { 发送错误(执行者, "数量必须为非负整数。"); return; }

            string 目标玩家名 = args.Parameters[0];
            string 货币名 = args.Parameters[1];

            _货币数据.设置货币(目标玩家名, 货币名, 数量);
            发送成功(执行者, $"已将 {目标玩家名} 的{货币名}设置为 {数量}。");

            var 目标 = 查找玩家(目标玩家名);
            if (目标 != null && 目标.Name != 执行者.Name)
                目标.SendWarningMessage($"[黑市] 管理员将您的{货币名}设置为 {数量}。");
            TShock.Log.Info($"[黑市] {执行者.Name} 设置 {目标玩家名} 的{货币名}为 {数量}");
        }

        private void 查询余额命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 1) { 发送错误(执行者, "用法：/查询余额 <玩家名>"); return; }

            string 目标玩家名 = args.Parameters[0];
            var 所有 = _货币数据.获取玩家所有货币(目标玩家名);
            if (所有.Count == 0) { 发送信息(执行者, $"{目标玩家名} 没有任何货币。"); return; }

            执行者.SendMessage($"[c/FFD700:{目标玩家名} 的货币余额：]", Color.White);
            foreach (var kv in 所有)
                执行者.SendMessage($"[c/FFD700:{kv.Key}]：[c/00FF00:{kv.Value}]", Color.White);
        }

        private void 重置职业命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 1) { 发送错误(执行者, "用法：/重置职业 <玩家名>"); return; }

            string 目标玩家名 = args.Parameters[0];
            if (_职业系统.管理员重置职业(目标玩家名, out string 结果))
            {
                发送成功(执行者, 结果);
                var 目标 = 查找玩家(目标玩家名);
                if (目标 != null && 目标.Name != 执行者.Name)
                    目标.SendWarningMessage("[黑市] 管理员已将您的职业重置。");
                TShock.Log.Info($"[黑市] {执行者.Name} 重置 {目标玩家名} 的职业");
            }
            else
            {
                发送错误(执行者, 结果);
            }
        }

        private void 转账命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            if (args.Parameters.Count < 3) { 发送错误(玩家, "用法：/转账 <玩家名> <货币名> <数量>"); return; }
            if (!int.TryParse(args.Parameters[2], out int 数量) || 数量 <= 0) { 发送错误(玩家, "数量必须为正整数。"); return; }

            string 目标名 = args.Parameters[0];
            string 货币名 = args.Parameters[1];

            if (玩家.Name == 目标名) { 发送错误(玩家, "不能转账给自己。"); return; }

            var 目标 = 查找玩家(目标名);
            if (目标 == null || !目标.RealPlayer) { 发送错误(玩家, $"玩家 {目标名} 不在线。"); return; }

            int 当前 = _货币数据.获取余额(玩家.Name, 货币名);
            if (当前 < 数量) { 发送错误(玩家, $"余额不足，当前只有 {当前} {货币名}。"); return; }

            if (!_货币数据.扣除货币(玩家.Name, 货币名, 数量)) { 发送错误(玩家, "转账失败。"); return; }
            _货币数据.增加货币(目标名, 货币名, 数量);

            发送成功(玩家, $"已成功转账 {数量} {货币名} 给 {目标名}。");
            目标.SendSuccessMessage($"[黑市] {玩家.Name} 给您转账了 {数量} {货币名}。");
            TShock.Log.Info($"[黑市] {玩家.Name} 转账 {数量} {货币名} 给 {目标名}");
        }

        private void 列表命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }

            var 商品列表 = _配置.商品完整列表;
            if (商品列表.Count == 0) { 发送信息(玩家, "当前商店没有商品。"); return; }

            int 页码 = 1;
            if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out 页码))
            {
                发送错误(玩家, "页码必须为数字。");
                return;
            }
            if (页码 < 1) 页码 = 1;

            int 每页行数 = _配置.每页显示行数;
            if (每页行数 < 1) 每页行数 = 10;
            int 列数 = _配置.显示列数;
            if (列数 < 1) 列数 = 1;
            if (列数 > 3) 列数 = 3;

            int 每页商品数 = 每页行数 * 列数;
            int 总商品数 = 商品列表.Count;
            int 总页数 = (总商品数 + 每页商品数 - 1) / 每页商品数;
            if (页码 > 总页数) 页码 = 总页数;

            int 起始索引 = (页码 - 1) * 每页商品数;
            int 取出数量 = Math.Min(每页商品数, 总商品数 - 起始索引);
            var 当前页商品 = 商品列表.Skip(起始索引).Take(取出数量).ToList();

            var 消息列表 = new List<string>
            {
                $"[c/AAAAAA:────────── 黑市商品 (第 {页码}/{总页数} 页) ──────────]"
            };

            if (列数 == 1)
            {
                int 序号 = 起始索引 + 1;
                foreach (var 商品 in 当前页商品)
                {
                    string 行 = 生成商品详情行(商品, 序号);
                    消息列表.Add(行);
                    序号++;
                }
            }
            else
            {
                int 序号 = 起始索引 + 1;
                for (int i = 0; i < 当前页商品.Count; i += 列数)
                {
                    var 行片段 = new List<string>();
                    for (int j = 0; j < 列数; j++)
                    {
                        if (i + j < 当前页商品.Count)
                            行片段.Add(生成商品简写(当前页商品[i + j], 序号 + j));
                        else
                            行片段.Add("");
                    }
                    消息列表.Add(string.Join("  |  ", 行片段.Where(s => !string.IsNullOrEmpty(s))));
                    序号 += 列数;
                }
            }

            消息列表.Add("[c/AAAAAA:────────────────────────────]");
            if (总页数 > 1)
                消息列表.Add($"[c/FFFF00:/黑市 <页码>] [c/AAAAAA:共 {总页数} 页，当前第 {页码} 页]");
            消息列表.Add("[c/FFFF00:/购买 <序号> [数量]] [c/AAAAAA:购买商品]");
            消息列表.Add("[c/FFFF00:/余额] [c/AAAAAA:查询余额、兑换、转账]");

            foreach (var 行 in 消息列表)
                玩家.SendMessage(行, Color.White);
        }

        private string 生成商品详情行(商品 商品, int 序号)
        {
            string 行 = $"[c/FFD700:<{序号}>] [i:{商品.物品}] [c/87CEEB:{商品.名称}]";
            if (商品.盲盒.Count > 0) 行 += " [c/FF69B4:（盲盒）]";
            行 += $" [c/00FF00:价格:{商品.价格}{商品.货币}]";
            if (商品.进度.Count > 0)
                行 += $" [c/808080:进度:{string.Join(",", 商品.进度)}]";
            if (商品.职业.Count > 0)
                行 += $" [c/FFA500:职业:{string.Join("/", 商品.职业)}]";
            if (商品.冷却 > 0)
                行 += $" [c/FFA500:冷却:{商品.冷却}s]";
            return 行;
        }

        private string 生成商品简写(商品 商品, int 序号)
        {
            string 行 = $"[c/FFD700:<{序号}>] [i:{商品.物品}] [c/87CEEB:{商品.名称}]";

            if (商品.盲盒.Count > 0)
                行 += " [c/FF69B4:（盲盒）]";

            if (商品.进度.Count > 0)
                行 += $" [c/808080:{string.Join(",", 商品.进度)}]";

            if (商品.职业.Count > 0)
                行 += $" [c/FFA500:职业:{string.Join("/", 商品.职业)}]";

            if (商品.冷却 > 0)
                行 += $" [c/FFA500:冷却:{商品.冷却}s]";

            行 += $" [c/00FF00:价格:{商品.价格}{商品.货币}]";
            return 行;
        }

        private void 购买命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            if (args.Parameters.Count < 1) { 发送错误(玩家, "用法：/购买 <序号> [数量]"); return; }
            if (!int.TryParse(args.Parameters[0], out int 序号)) { 发送错误(玩家, "序号必须是数字。"); return; }

            var 商品 = _配置.获取商品(序号);
            if (商品 == null) { 发送错误(玩家, "商品序号不存在。"); return; }

            if (!_职业系统.检查职业匹配(玩家.Name, 商品.职业))
            {
                发送错误(玩家, $"该商品需要以下职业之一：{string.Join("、", 商品.职业)}");
                return;
            }

            bool 带指令 = 商品.指令 != null && 商品.指令.Count > 0;
            int 数量 = 1;
            if (args.Parameters.Count >= 2)
            {
                if (!int.TryParse(args.Parameters[1], out 数量) || 数量 <= 0)
                {
                    发送错误(玩家, "数量必须为正整数。");
                    return;
                }
            }

            if (带指令 && 数量 > 1)
            {
                玩家.SendMessage("[c/FFA500:该商品带有执行指令，每次限购1个，已自动调整为1个。]", Color.White);
                数量 = 1;
            }

            if (商品.冷却 > 0)
            {
                var 结束时间 = _货币数据.获取冷却结束时间(玩家.Name, 序号);
                if (结束时间.HasValue && 结束时间.Value > DateTime.Now)
                {
                    var 剩余秒数 = (int)(结束时间.Value - DateTime.Now).TotalSeconds;
                    发送错误(玩家, $"此商品冷却中，剩余 {剩余秒数} 秒。");
                    return;
                }
            }

            if (商品.进度.Count > 0)
            {
                bool 满足 = false;
                foreach (var 条件表达式 in 商品.进度)
                {
                    if (进度映射器.检查进度表达式(条件表达式))
                    {
                        满足 = true;
                        break;
                    }
                }
                if (!满足) 
                { 
                    发送错误(玩家, $"需要进度之一：{string.Join("，", 商品.进度)}"); 
                    return; 
                }
            }

            int 总价 = 商品.价格 * 数量;
            int 余额 = _货币数据.获取余额(玩家.Name, 商品.货币);
            if (余额 < 总价)
            {
                玩家.SendMessage($"[c/FF5555:✘ 货币不足！需要 {总价} 个{商品.货币}，你只有 {余额} 个。]", Color.White);
                return;
            }
            if (!_货币数据.扣除货币(玩家.Name, 商品.货币, 总价)) { 发送错误(玩家, "扣除货币失败。"); return; }

            给予奖励(玩家, 商品, 数量, 总价);

            if (商品.冷却 > 0)
                _货币数据.设置冷却(玩家.Name, 序号, 商品.冷却);
        }

        private void 给予奖励(TSPlayer 玩家, 商品 商品, int 数量, int 总价)
        {
            // 数量为0时不给予物品
            if (商品.数量 > 0)
            {
                if (商品.盲盒.Count > 0)
                {
                    var 总权重 = 商品.盲盒.Sum(r => r.权重);
                    if (总权重 <= 0) { 发送错误(玩家, "盲盒权重错误。"); return; }
                    for (int i = 0; i < 数量; i++)
                    {
                        int 抽 = _随机.Next(总权重);
                        int 累加 = 0;
                        foreach (var 奖励 in 商品.盲盒)
                        {
                            累加 += 奖励.权重;
                            if (抽 < 累加) { 玩家.GiveItem(奖励.物品, 奖励.数量); break; }
                        }
                    }
                    if (_配置.购买聊天栏文本)
                        玩家.SendMessage($"[c/00FF00:成功购买 {数量} 个盲盒，花费 {总价} 个{商品.货币}。]", Color.White);
                    if (_配置.购买浮动文本)
                        发送浮动文字(玩家, $"已购买 盲盒×{数量}", Color.Green);
                }
                else
                {
                    玩家.GiveItem(商品.物品, 商品.数量 * 数量, 商品.前缀);
                    int 剩余 = _货币数据.获取余额(玩家.Name, 商品.货币);
                    if (_配置.购买聊天栏文本)
                        玩家.SendMessage($"[c/00FF00:购买成功，花费 {总价}{商品.货币} 还剩余 {剩余}{商品.货币}。]", Color.White);
                    if (_配置.购买浮动文本)
                        发送浮动文字(玩家, $"已购买 {商品.名称}×{数量}", Color.Green);
                }
            }
            else
            {
                // 数量为0，只执行指令，不给物品
                if (_配置.购买聊天栏文本)
                    玩家.SendMessage($"[c/00FF00:已执行指令，花费 {总价}{商品.货币}。]", Color.White);
                if (_配置.购买浮动文本)
                    发送浮动文字(玩家, $"指令已执行", Color.Green);
            }

            // 指令执行：绕过权限检查，让玩家可以无视权限使用命令
            if (商品.指令.Count > 0)
            {
                foreach (var 指令 in 商品.指令)
                {
                    string 处理 = 指令.Replace("{0}", 玩家.Name).Replace("{}", 玩家.Name);
                    if (!处理.StartsWith("/")) 处理 = "/" + 处理;
                    
                    // 临时赋予超级管理员权限执行指令，绕过所有权限检查
                    执行指令无视权限(玩家, 处理);
                }
            }
        }

        /// <summary>
        /// 以超级管理员权限执行指令，绕过所有权限检查
        /// </summary>
        private void 执行指令无视权限(TSPlayer 玩家, string 指令)
        {
            // 保存原始权限组
            string 原始组 = 玩家.Group.Name;
            var 超级管理员组 = TShock.Groups.GetGroupByName("superadmin");
            
            if (超级管理员组 == null)
            {
                // 如果没有superadmin组，回退到服务器执行
                Commands.HandleCommand(TSPlayer.Server, 指令);
                return;
            }

            try
            {
                // 临时切换到超级管理员权限组
                玩家.Group = 超级管理员组;
                // 执行指令
                Commands.HandleCommand(玩家, 指令);
            }
            finally
            {
                // 无论是否成功，都恢复原始权限组
                var 原组 = TShock.Groups.GetGroupByName(原始组);
                if (原组 != null)
                    玩家.Group = 原组;
                else
                    玩家.Group = TShock.Groups.GetGroupByName("default") ?? 超级管理员组;
            }
        }

        private void 兑换命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }

            var 手持 = 玩家.TPlayer.inventory[玩家.TPlayer.selectedItem];
            if (手持 == null || 手持.IsAir) { 发送错误(玩家, "请手持要兑换的物品。"); return; }
            int 来源ID = 手持.type;
            int 堆叠 = 手持.stack;
            if (堆叠 <= 0) { 发送错误(玩家, "你没有手持物品。"); return; }

            int 兑换数量 = 1;
            if (args.Parameters.Count >= 1)
            {
                if (!int.TryParse(args.Parameters[0], out 兑换数量) || 兑换数量 <= 0) { 发送错误(玩家, "兑换数量必须为正整数。"); return; }
                if (兑换数量 > 堆叠) { 发送错误(玩家, $"你只有 {堆叠} 个，无法兑换 {兑换数量} 个。"); return; }
            }

            var 规则 = _配置.兑换规则列表.FirstOrDefault(r => r.来源物品ID == 来源ID);
            if (规则 == null) { 发送错误(玩家, "当前手持物品没有配置兑换规则。"); return; }

            int 获得 = 兑换数量 * 规则.比例;
            if (兑换数量 == 堆叠) { 手持.stack = 0; 手持.TurnToAir(); }
            else { 手持.stack -= 兑换数量; }
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, "", 玩家.Index, 玩家.TPlayer.selectedItem);
            _货币数据.增加货币(玩家.Name, 规则.目标货币, 获得);

            if (_配置.兑换聊天栏文本)
            {
                string 消息 = $"[i:{来源ID}] 成功将 {规则.来源物品名称}×{兑换数量} 兑换为 {规则.目标货币}×{获得}";
                玩家.SendMessage($"[c/00FF00:✔ {消息}]", Color.White);
            }
            if (_配置.兑换浮动文本)
                发送浮动文字(玩家, $"兑换成功！+{获得}{规则.目标货币}", Color.Green);
        }

        private void 余额命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }

            var 所有 = _货币数据.获取玩家所有货币(玩家.Name);
            if (所有.Count == 0) { 发送信息(玩家, "您目前没有任何货币。"); return; }

            玩家.SendMessage("[c/FFD700:您的货币余额：]", Color.White);
            foreach (var kv in 所有)
                if (kv.Value > 0)
                    玩家.SendMessage($"[c/FFD700:{kv.Key}]：[c/00FF00:{kv.Value}]", Color.White);

            玩家.SendMessage("[c/AAAAAA:────────────────────────────]", Color.White);
            玩家.SendMessage("[c/FFFF00:/兑换 [数量]] [c/AAAAAA:手持物品兑换为货币]", Color.White);
            玩家.SendMessage("[c/FFFF00:/转账 <玩家> <货币> <数量>] [c/AAAAAA:转给其他玩家]", Color.White);
        }

        private void 转职命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }

            if (args.Parameters.Count == 0)
            {
                string 信息 = _职业系统.获取职业列表信息(玩家);
                玩家.SendMessage(信息, Color.White);
                return;
            }

            string 职业名 = args.Parameters[0];
            if (_职业系统.切换职业(玩家, 职业名, out string 结果))
                发送成功(玩家, 结果);
            else
                发送错误(玩家, 结果);
        }

        // ========== 任务命令处理 ==========

        private void 任务列表命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            string 信息 = _任务系统.获取任务列表信息(玩家.Name);
            玩家.SendMessage(信息, Color.White);
        }

        private void 接取任务命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            if (args.Parameters.Count < 1) { 发送错误(玩家, "用法：/接取 <序号>"); return; }
            if (!int.TryParse(args.Parameters[0], out int 序号)) { 发送错误(玩家, "序号必须是数字。"); return; }

            if (_任务系统.接取任务(玩家, 序号, out string 结果))
                发送成功(玩家, 结果);
            else
                发送错误(玩家, 结果);
        }

        private void 提交任务命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            if (args.Parameters.Count < 1) { 发送错误(玩家, "用法：/提交 <序号>"); return; }
            if (!int.TryParse(args.Parameters[0], out int 序号)) { 发送错误(玩家, "序号必须是数字。"); return; }

            if (_任务系统.提交任务(玩家, 序号, out string 结果))
                发送成功(玩家, 结果);
            else
                发送错误(玩家, 结果);
        }

        private void 放弃任务命令处理(CommandArgs args)
        {
            var 玩家 = args.Player;
            if (玩家 == null || !玩家.RealPlayer) { 发送错误(玩家, "只有玩家才能使用此命令。"); return; }
            if (args.Parameters.Count < 1) { 发送错误(玩家, "用法：/放弃 <序号>"); return; }
            if (!int.TryParse(args.Parameters[0], out int 序号)) { 发送错误(玩家, "序号必须是数字。"); return; }

            if (_任务系统.放弃任务(玩家, 序号, out string 结果))
                发送成功(玩家, 结果);
            else
                发送错误(玩家, 结果);
        }

        private void 重置任务命令处理(CommandArgs args)
        {
            var 执行者 = args.Player;
            if (args.Parameters.Count < 2) { 发送错误(执行者, "用法：/任务重置 <玩家名> <序号>"); return; }
            if (!int.TryParse(args.Parameters[1], out int 序号)) { 发送错误(执行者, "序号必须是数字。"); return; }

            string 目标玩家名 = args.Parameters[0];
            if (_任务系统.管理员重置任务(目标玩家名, 序号, out string 结果))
            {
                发送成功(执行者, 结果);
                var 目标 = 查找玩家(目标玩家名);
                if (目标 != null && 目标.Name != 执行者.Name)
                    目标.SendWarningMessage("[黑市] 管理员已重置您的任务进度。");
                TShock.Log.Info($"[黑市] {执行者.Name} 重置 {目标玩家名} 的任务 {序号}");
            }
            else
            {
                发送错误(执行者, 结果);
            }
        }
    }
}