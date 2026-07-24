using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace 黑市
{
    public class NPC掉落配置
    {
        public static readonly string 配置路径 = Path.Combine(黑市路径.根目录, "NPC掉落.json");

        [JsonProperty("击杀浮动文本")] public bool 击杀浮动文本 { get; set; } = true;
        [JsonProperty("击杀聊天栏文本")] public bool 击杀聊天栏文本 { get; set; } = true;

        [JsonProperty("掉落规则")]
        public List<NPC掉落规则> 掉落规则列表 { get; set; } = new();

        public static NPC掉落配置 加载(out string 错误报告)
        {
            错误报告 = "";
            黑市路径.确保目录存在();
            if (!File.Exists(配置路径))
            {
                var 默认 = 创建默认配置();
                默认.保存();
                return 默认;
            }
            try
            {
                var json = File.ReadAllText(配置路径);

                // 严格验证 JSON 格式完整性
                try { JToken.Parse(json); }
                catch (Exception jsonEx)
                {
                    错误报告 = "NPC掉落.json JSON格式错误：" + jsonEx.Message;
                    return new NPC掉落配置();
                }

                var 配置 = JsonConvert.DeserializeObject<NPC掉落配置>(json);
                if (配置 == null)
                {
                    错误报告 = "NPC掉落.json 反序列化失败，请检查JSON格式。";
                    return new NPC掉落配置();
                }

                bool 验证通过 = true;
                var 报告列表 = new List<string>();

                if (配置.掉落规则列表 == null)
                {
                    验证通过 = false;
                    报告列表.Add("掉落规则列表不能为空。");
                }
                else
                {
                    var id集合 = new HashSet<int>();
                    foreach (var 规则 in 配置.掉落规则列表)
                    {
                        if (规则 == null)
                        {
                            验证通过 = false;
                            报告列表.Add("发现空掉落规则。");
                            continue;
                        }
                        if (规则.NPCID <= 0)
                        {
                            验证通过 = false;
                            报告列表.Add($"发现非法NPCID：{规则.NPCID}。");
                            continue;
                        }
                        if (id集合.Contains(规则.NPCID))
                        {
                            验证通过 = false;
                            报告列表.Add($"发现重复NPCID：{规则.NPCID}。");
                            continue;
                        }
                        id集合.Add(规则.NPCID);

                        if (规则.掉落数量 < 0)
                        {
                            验证通过 = false;
                            报告列表.Add($"NPCID {规则.NPCID} 掉落数量不能为负。");
                        }
                        if (string.IsNullOrWhiteSpace(规则.货币名称))
                        {
                            验证通过 = false;
                            报告列表.Add($"NPCID {规则.NPCID} 货币名称不能为空。");
                        }
                    }
                }

                if (!验证通过)
                {
                    错误报告 = string.Join("\n", 报告列表);
                    return new NPC掉落配置();
                }

                // 验证通过，整理并保存
                配置.保存();
                return 配置;
            }
            catch (Exception ex)
            {
                错误报告 = $"NPC掉落.json 加载异常：{ex.Message}";
                return new NPC掉落配置();
            }
        }

        public static NPC掉落配置 加载()
        {
            return 加载(out _);
        }

        private static NPC掉落配置 创建默认配置()
        {
            var 默认 = new NPC掉落配置();
            默认.掉落规则列表.Add(new NPC掉落规则 { NPCID = 4, 货币名称 = "功勋", 掉落数量 = 10 });
            默认.掉落规则列表.Add(new NPC掉落规则 { NPCID = 1, 货币名称 = "声望", 掉落数量 = 3 });
            return 默认;
        }

        public void 保存()
        {
            File.WriteAllText(配置路径, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }

    public class NPC掉落规则
    {
        [JsonProperty("NPCID")] public int NPCID { get; set; }
        [JsonProperty("货币名称")] public string 货币名称 { get; set; } = "";
        [JsonProperty("掉落数量")] public int 掉落数量 { get; set; } = 1;
    }
}
