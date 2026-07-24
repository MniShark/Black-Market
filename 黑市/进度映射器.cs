using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Terraria;

namespace 黑市
{
    public static class 进度映射器
    {
        // 英文 → 中文别名列表
        private static Dictionary<string, List<string>> _映射表 = new();
        // 中文 → 英文（反向查找）
        private static Dictionary<string, string> _反向映射 = new();

        public static void 加载()
        {
            黑市路径.确保目录存在();
            if (!File.Exists(黑市路径.进度映射路径))
            {
                var 默认映射 = new Dictionary<string, List<string>>
                {
                    { "downedBoss1", new List<string> { "克眼", "克苏鲁之眼" } },
                    { "downedBoss2", new List<string> { "克脑", "世吞", "世界吞噬者" } },
                    { "downedBoss3", new List<string> { "骷髅王" } },
                    { "hardMode", new List<string> { "肉山", "肉后", "困难模式" } },
                    { "downedQueenBee", new List<string> { "蜂王", "蜂后" } },
                    { "downedSlimeKing", new List<string> { "史莱姆王", "史王" } },
                    { "downedMechBossAny", new List<string> { "一王后", "任意机械Boss" } },
                    { "downedMechBoss1", new List<string> { "毁灭者" } },
                    { "downedMechBoss2", new List<string> { "双子魔眼" } },
                    { "downedMechBoss3", new List<string> { "机械骷髅王" } },
                    { "downedPlantBoss", new List<string> { "世纪之花", "花后" } },
                    { "downedGolemBoss", new List<string> { "石巨人", "石后" } },
                    { "downedAncientCultist", new List<string> { "邪教徒", "拜月教邪教徒" } },
                    { "downedMoonlord", new List<string> { "月亮领主", "月总" } },
                    { "downedGoblins", new List<string> { "哥布林入侵" } },
                    { "downedFrost", new List<string> { "雪人军团" } },
                    { "downedPirates", new List<string> { "海盗入侵" } },
                    { "downedMartians", new List<string> { "火星入侵" } },
                    { "expertMode", new List<string> { "专家模式" } },
                    { "savedTaxCollector", new List<string> { "税收官" } },
                    { "savedGoblin", new List<string> { "哥布林工匠" } },
                    { "savedWizard", new List<string> { "巫师" } },
                    { "savedMech", new List<string> { "机械师" } },
                    { "savedAngler", new List<string> { "渔夫" } },
                    { "savedStylist", new List<string> { "发型师" } },
                    { "savedBartender", new List<string> { "酒馆老板" } }
                };
                File.WriteAllText(黑市路径.进度映射路径, JsonConvert.SerializeObject(默认映射, Formatting.Indented));
                _映射表 = 默认映射;
            }
            else
            {
                try
                {
                    var json = File.ReadAllText(黑市路径.进度映射路径);
                    _映射表 = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json) ?? new();
                }
                catch { _映射表 = new Dictionary<string, List<string>>(); }
            }

            // 构建反向映射
            _反向映射.Clear();
            foreach (var kv in _映射表)
            {
                foreach (var 中文 in kv.Value)
                {
                    _反向映射[中文] = kv.Key;
                }
            }
        }

        /// <summary>
        /// 解析进度表达式，支持 & | ! 运算符
        /// 示例: "肉山&花后" "克眼|克脑" "!肉山"
        /// </summary>
        public static bool 检查进度表达式(string 表达式)
        {
            if (string.IsNullOrWhiteSpace(表达式)) return true;

            // 先处理括号（简单实现，不支持嵌套括号）
            if (表达式.Contains("("))
            {
                // 简化处理：去掉括号直接解析
                表达式 = 表达式.Replace("(", "").Replace(")", "");
            }

            // 先尝试 AND
            var andParts = 表达式.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (andParts.Length > 1)
            {
                return andParts.All(p => 检查单个条件(p.Trim()));
            }

            // 再尝试 OR
            var orParts = 表达式.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (orParts.Length > 1)
            {
                return orParts.Any(p => 检查单个条件(p.Trim()));
            }

            // 单个条件
            return 检查单个条件(表达式.Trim());
        }

        /// <summary>
        /// 检查单个条件（支持 ! 取反）
        /// </summary>
        private static bool 检查单个条件(string 条件)
        {
            bool 取反 = 条件.StartsWith("!");
            string 名称 = 取反 ? 条件.Substring(1).Trim() : 条件.Trim();

            // 查找英文字段名
            string 英文 = 转英文(名称);
            if (英文 == 名称 && !_反向映射.ContainsKey(名称))
            {
                // 未映射，尝试直接作为字段名
                英文 = 名称;
            }

            bool 结果 = 检查字段(英文);
            return 取反 ? !结果 : 结果;
        }

        /// <summary>
        /// 直接检查 NPC 静态字段/属性
        /// </summary>
        private static bool 检查字段(string 字段名)
        {
            var 字段 = typeof(NPC).GetField(字段名, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (字段 != null && 字段.FieldType == typeof(bool))
                return (bool)字段.GetValue(null);

            var 属性 = typeof(NPC).GetProperty(字段名, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (属性 != null && 属性.PropertyType == typeof(bool))
                return (bool)属性.GetValue(null);

            return false;
        }

        /// <summary>
        /// 中文 → 英文
        /// </summary>
        public static string 转英文(string 中文进度)
        {
            if (_反向映射.TryGetValue(中文进度, out string 英文))
                return 英文;
            return 中文进度; // 未映射则原样返回（兼容直接写英文）
        }

        /// <summary>
        /// 转换列表中的每个元素
        /// </summary>
        public static List<string> 转换列表(List<string> 中文列表)
        {
            return 中文列表.Select(x => 转英文(x)).ToList();
        }
    }
}